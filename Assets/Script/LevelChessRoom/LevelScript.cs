using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using System.Threading.Tasks;
using UnityEngine.UI;
using UnityEngine.Rendering;
using System.Linq;
using System;
using DG.Tweening;
using RoomConfig;

public class LevelScript : MonoBehaviour
{
    public GameObject dice;
    public GameObject dice_anchor;
    public Button roll_dice_button;
    public Button pawn_confirm_button;
    public ChessBoardMgr chess_board_mgr;
    public Camera main_camera;
    public Camera uiCamera;
    private CameraController main_camera_controller;
    public GameObject diceGirl;

    // 交互完成事件
    private TaskCompletionSource<int> pawnChoseTCS;
    private TaskCompletionSource<bool> rollDiceButtonClickTCS;

    // 被选中的棋子
    private GameObject choosen_pawn = null;

    // Current player and his available pawn
    List<int> cur_avai_pawnidx;
    public RawImage cur_player_image;
    private List<Texture> player_textures = new List<Texture>();

    bool is_choosing_pawn = false;

    // 粒子特效
    public ParticleSystem diceButtonParticle;
    public ParticleSystem playerIconParticle;

    // 切换玩家图片
    public GameObject switchPlayerImage;

    // 骰子永远为6
    public bool diceSixForever = true;

    // 游戏结束排名面板
    
    public List<Text> rankTexts;
    public List<RawImage> rankPlayerIcons;
    public List<Text> rankStepTexts;
    public GameObject gameEndPanel;

    private void ChangePlayer()
    {
        int curPlayerIdx = RoomHost.roomInfo.ChangeCharactor();
        cur_player_image.texture = player_textures[curPlayerIdx];
    }

    private async Task PlaySwitchNote()
    {
        Image image = switchPlayerImage.GetComponentInChildren<Image>();
        Text text = switchPlayerImage.GetComponentInChildren<Text>();
        Task t3 = switchPlayerImage.transform.DOLocalMoveX(-792, 0.5f).From().SetEase(Ease.OutSine).AsyncWaitForCompletion();
        Task t1 = image.DOFade(1, 1f).AsyncWaitForCompletion();
        Task t2 = text.DOFade(1, 1f).AsyncWaitForCompletion();
        await Task.WhenAll(t1, t2, t3);

        await Task.Delay(1000);

        t1 = image.DOFade(0, 1f).AsyncWaitForCompletion();
        t2 = text.DOFade(0, 1f).AsyncWaitForCompletion();
        t3 = switchPlayerImage.transform.DOLocalMoveX(792, 0.5f).SetEase(Ease.InSine).AsyncWaitForCompletion();
        await Task.WhenAll(t1, t2, t3);

        Vector3 pos = switchPlayerImage.transform.localPosition;
        pos.x = 0;
        switchPlayerImage.transform.localPosition = pos;

    }

    private bool CheckGameEnd()
    {
        int remain = 0;
        
        foreach (int idx in RoomHost.roomInfo.playerIdxes)
        {
            remain += RoomHost.roomInfo.charactorPawnCount[idx];
        }
        if (remain != 0)
        {
            return false;
        }
        List<List<int>> playerSteps = RoomHost.roomInfo.charactorSteps;
        playerSteps.Sort((List<int> a, List<int> b) =>
        {
            return a[1] - b[1];
        });

        gameEndPanel.SetActive(true);
        int rank = 0;
        foreach(List<int> p in playerSteps)
        {
            if (!RoomHost.roomInfo.playerIdxes.Contains(p[0]))
            {
                continue;
            }
            rankPlayerIcons[rank].texture = player_textures[p[0]];
            rankTexts[rank].text = (rank+1).ToString();
            rankStepTexts[rank].text = p[1].ToString();
            rankPlayerIcons[rank].gameObject.SetActive(true);
            rank++;
        }
        RectTransform panelTrans = gameEndPanel.GetComponent<RectTransform>();
        panelTrans.localPosition = new Vector3(0f, 0f, 0f);

        playerIconParticle.gameObject.SetActive(false);
        return true;
    }

    private async Task<int> CharactorRollDiceOffline()
    {
        int dice_value;
        if (RoomHost.roomInfo.playerIdxes.Contains(RoomHost.roomInfo.curActCharactorIdx))
        {
            RollDiceInfo dice_info = await PlayerRollDice();
            dice_value = dice_info.dice_value;
        }
        else
        {
            RollDiceInfo dice_info = await AIRollDice();
            dice_value = dice_info.dice_value;
        }
        return dice_value;
    }

    string test(RollDiceInfo dice_info)
    {
        string ret = string.Empty;
        for(int i=0; i<3; ++i)
        {
            ret += dice_info.rotation_power[i] + ", ";
        }
        for(int i=0; i<3; ++i)
        {
            ret += dice_info.compaction_power[i] + ", ";
        }
        return ret;
    }

    private async Task<int> CharactorRollDiceOnline()
    {
        int dice_value=0;
        if (RoomHost.roomInfo.localIdx == RoomHost.roomInfo.curActCharactorIdx)
        {
            RollDiceInfo dice_info = await PlayerRollDice();
            dice_value = dice_info.dice_value;
            Debug.Log("send: " + test(dice_info));
            await RoomHost.SendRollDiceInfoToOthers(dice_info);
        }
        else if(RoomHost.roomInfo.isRoomHost && RoomHost.roomInfo.aiIdxes.Contains(RoomHost.roomInfo.curActCharactorIdx))
        {
            RollDiceInfo dice_info = await AIRollDice();
            dice_value = dice_info.dice_value;
            Debug.Log("send: " + test(dice_info));
            await RoomHost.SendRollDiceInfoToOthers(dice_info);
        }
        else
        {
            RollDiceInfo dice_info = await RoomHost.GetRollDiceInfoFromOthers();
            Debug.Log("received: " + test(dice_info));
            dice_value = dice_info.dice_value;
            await FakeRollDice(dice_value);
        }
        return dice_value;
    }

    private async Task<int> OfflineChoosePawn()
    {
        int pawnId;
        if (RoomHost.roomInfo.playerIdxes.Contains(RoomHost.roomInfo.curActCharactorIdx))
        {
            pawnId = await PlayerChoosePawn();
        }
        else
        {
            pawnId = AIChoosePawn(cur_avai_pawnidx);
        }
        return pawnId;
    }

    private async Task<int> OnlineChoosePawn()
    {
        int pawnId;
        if (RoomHost.roomInfo.localIdx == RoomHost.roomInfo.curActCharactorIdx)
        {
            pawnId = await PlayerChoosePawn();
            await RoomHost.SendPawnChooseInfoToOthers(pawnId);
        }
        else if (RoomHost.roomInfo.isRoomHost && RoomHost.roomInfo.aiIdxes.Contains(RoomHost.roomInfo.curActCharactorIdx))
        {
            pawnId = AIChoosePawn(cur_avai_pawnidx);
            await RoomHost.SendPawnChooseInfoToOthers(pawnId);
        }
        else
        {
            PawnChooseInfo pawnChooseInfo = await RoomHost.GetPawnChooseInfoFromOthers();
            pawnId = pawnChooseInfo.choosenPawnID;
        }

        return pawnId;
    }

    async private Task GameMainLoop()
    {
        //chess_board_mgr.Debug_PawnPlaceTo(3, 11);
        //chess_board_mgr.Debug_PawnPlaceTo(4, 53);
        //for(int i=0; i<3; ++i)
        //{
        //    chess_board_mgr.PawnRemoveFromBoard(i);
        //    RoomHost.roomInfo.charactorPawnCount[0] -= 1;
        //}
        //for (int i = 5; i < 8; ++i)
        //{
        //    chess_board_mgr.PawnRemoveFromBoard(i);
        //    RoomHost.roomInfo.charactorPawnCount[1] -= 1;
        //}

        bool first = true;
        while (true)
        {

            // 检查游戏是否结束
            if (CheckGameEnd())
            {
                break;
            }
            
            // 切换玩家
            if (!first)
            {
                ChangePlayer();
                if (RoomHost.roomInfo.charactorPawnCount[RoomHost.roomInfo.curActCharactorIdx] == 0)
                {
                    continue;
                }
                await PlaySwitchNote();
            }
            else
            {
                first = false;
            }

            // 摇骰子
            int dice_value;
            if(RoomHost.roomInfo.roomType == RoomType.OfflineRoom)
            {
                dice_value = await CharactorRollDiceOffline();
            }
            else
            {
                dice_value = await CharactorRollDiceOnline();
            }


            // 玩家摇骰子的次数加一
            RoomHost.roomInfo.charactorSteps[RoomHost.roomInfo.curActCharactorIdx][1] += 1;

            // 获取玩家能够选择的棋子
            cur_avai_pawnidx = chess_board_mgr.GetAvailablePawnIdx(RoomHost.roomInfo.charactorPawnColor[RoomHost.roomInfo.curActCharactorIdx], dice_value);
            int pawn_id;
            if (cur_avai_pawnidx.Count != 0)
            {
                // 获取玩家选择的棋子
                if(RoomHost.roomInfo.roomType == RoomType.OfflineRoom)
                {
                    pawn_id = await OfflineChoosePawn();
                }
                else
                {
                    pawn_id = await OnlineChoosePawn();
                }
            }
            else
            {
                continue;
            }

            // 处理棋子移动
            bool pawn_on_walkpath = chess_board_mgr.IsPawnOnWalkPath(pawn_id);
            if(!pawn_on_walkpath)
            {
                await chess_board_mgr.PawnMoveToStart(pawn_id);
                // 摇骰子
                if (RoomHost.roomInfo.roomType == RoomType.OfflineRoom)
                {
                    dice_value = await CharactorRollDiceOffline();
                }
                else
                {
                    dice_value = await CharactorRollDiceOnline();
                }
            }
            await chess_board_mgr.PawnMoveForward(pawn_id, dice_value);

            // 处理棋子移动到相同颜色的跳跃
            if (chess_board_mgr.PawnCheckSkip(pawn_id))
            {
                await chess_board_mgr.PawnDoSkip(pawn_id);
            }

            // 处理击中其它棋子
            List<int> kick_back_list = chess_board_mgr.PawnCheckBeatOther(pawn_id);
            if(kick_back_list.Count > 0)
            {
                await chess_board_mgr.PawnKickBackToStandby(kick_back_list);
            }

            // 处理棋子移动到终点
            if (chess_board_mgr.PawnCheckToEnd(pawn_id))
            {
                await Task.Delay(1000);
                chess_board_mgr.PawnRemoveFromBoard(pawn_id);
                RoomHost.roomInfo.charactorPawnCount[RoomHost.roomInfo.curActCharactorIdx] -= 1;
            }
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        int playerCount=2, aiCount=1;
        if (!RoomHost.roomInfo.params_set)
        {
            RoomHost.roomInfo.CreateOfflineRoom(playerCount, aiCount);
        }

        List<string> text_name = new List<string> { "red_pawn", "green_pawn", "blue_pawn", "yellow_pawn" };
        for(int i=0; i<4; ++i)
        {
            Texture texture = Resources.Load<Texture>("images/" + text_name[i]);
            player_textures.Add(texture);
        }
       
        
        chess_board_mgr.InitGameMode(RoomHost.roomInfo.charactorPawnColor);
        pawn_confirm_button.gameObject.SetActive(false);
        main_camera_controller = main_camera.GetComponent<CameraController>();
        cur_player_image.texture = player_textures[0];
        GameMainLoop();
        
    }

    public void OnChoosePawnBtnClick()
    {
        PawnController pawn_con = choosen_pawn.GetComponent<PawnController>();
        if (pawn_con == null) return;
        pawnChoseTCS.TrySetResult(pawn_con.pawn_id);
    }

    public int AIChoosePawn(List<int> pawn_list)
    {
        int choosen_idx = UnityEngine.Random.Range(0, pawn_list.Count);
        return pawn_list[choosen_idx];
    }
    public async Task<int> PlayerChoosePawn()
    {
        foreach (int id in cur_avai_pawnidx)
        {
            chess_board_mgr.PawnStartToBlink(id);
        }

        is_choosing_pawn = true;
        pawn_confirm_button.interactable = false;
        pawn_confirm_button.gameObject.SetActive(true);

        pawnChoseTCS = new TaskCompletionSource<int>();
        int choosen_pawnid = await pawnChoseTCS.Task;
        Debug.Log("Choosen Pawn ID:" + choosen_pawnid.ToString());

        is_choosing_pawn = false;
        pawn_confirm_button.gameObject.SetActive(false);
        pawn_confirm_button.interactable = false;

        foreach (int id in cur_avai_pawnidx)
        {
            chess_board_mgr.PawnStopBlinking(id);
        }
        return choosen_pawnid;
    } 

    public async Task<RollDiceInfo> AIRollDice()
    {
        await dice.GetComponent<DiceController>().SetDiceAppear(GameObject.Find("dice_init_pos").transform.position);
        RollDiceInfo dice_info = await RollADice();
        await main_camera_controller.LookAtDiceValue(dice);
        return dice_info;
    }

    public async Task FakeRollDice(int dice_value)
    {
        await dice.GetComponent<DiceController>().SetDiceAppear(GameObject.Find("dice_init_pos").transform.position);
        Vector3 target_pos = dice.transform.position;
        target_pos.z += 1.8f;
        await diceGirl.GetComponent<DiceGrilController>().MoveToPositionXZ(target_pos, 2);

        // 播放女孩抛骰子动画
        diceGirl.GetComponent<DiceGrilController>().DoThrowAction();
        await Task.Delay(400);

        // 播放扔骰子动画
        DiceController dice_con = dice.GetComponent<DiceController>();
        dice_con.FakeRollDice(dice_value);
        await dice_con.GetDiceValue();

        diceGirl.GetComponent<DiceGrilController>().MoveToInitTrans(2);
    }

    public async Task<RollDiceInfo> PlayerRollDice()
    {
        await dice.GetComponent<DiceController>().SetDiceAppear(GameObject.Find("dice_init_pos").transform.position);
        Vector3 target_pos = dice.transform.position;
        target_pos.z += 1.8f;
        await diceGirl.GetComponent<DiceGrilController>().MoveToPositionXZ(target_pos, 2);
        main_camera_controller.FocusOnTarget(dice);
        await main_camera_controller.MoveToPosition(GameObject.Find("throw_dice_camera_pos"), 1.5f);
        

        // 设置按钮可交互状态
        diceButtonParticle.gameObject.SetActive(true);
        diceButtonParticle.Play();
        roll_dice_button.interactable = true;
        rollDiceButtonClickTCS = new TaskCompletionSource<bool>();
        await rollDiceButtonClickTCS.Task;

        // 设置按钮不可交互状态
        roll_dice_button.interactable = false;
        diceButtonParticle.Pause();
        diceButtonParticle.gameObject.SetActive(false);

        // 播放抛骰子动画
        diceGirl.GetComponent<DiceGrilController>().DoThrowAction();
        await Task.Delay(400);

        // 抛骰子
        RollDiceInfo dice_info = await RollADice();
        await main_camera_controller.LookAtDiceValue(dice);
        diceGirl.GetComponent<DiceGrilController>().MoveToInitTrans(2);
        return dice_info;
    }

    private async Task<RollDiceInfo> RollADice()
    {
        // 获取骰子的点数
        //dice.transform.position = dice_anchor.transform.position;
        DiceController dice_con = dice.GetComponent<DiceController>();
        List<Vector3> ret = dice_con.StartToRoll();
        int dice_value = await dice_con.GetDiceValue();
        dice_value = diceSixForever ? 6 : dice_value;

        RollDiceInfo dice_info = new RollDiceInfo();
        dice_info.rotation_power = new List<float> { ret[0].x, ret[0].y, ret[0].z };
        dice_info.compaction_power = new List<float> { ret[1].x, ret[1].y, ret[1].z };
        dice_info.dice_value = dice_value;
        dice_info.player_id = RoomHost.roomInfo.curActCharactorIdx;
        Debug.Log(dice_value);
        
        return dice_info;
    }

    public void OnRollDiceBtnClick()
    {
        rollDiceButtonClickTCS.SetResult(true);
    }

    // Update is called once per frame
    void Update()
    {
        if (is_choosing_pawn && Input.GetMouseButtonDown(0))
        {
            CheckChoosenPawn();
        }
    }

    void CheckChoosenPawn()
    { 
        Ray ray = main_camera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit))
        {
            // 将已选中的棋子取消选中
            if (choosen_pawn != null)
            {
                PawnController temp_con = choosen_pawn.GetComponent<PawnController>();
                temp_con.StopLighting();
            }
            GameObject hit_obj = hit.collider.gameObject;
            PawnController pawn_con = hit_obj.gameObject.GetComponent<PawnController>();
            if (pawn_con != null && cur_avai_pawnidx.Contains(pawn_con.pawn_id))
            {
                this.choosen_pawn = hit_obj;
                pawn_confirm_button.interactable = true;

                // 选中棋子
                pawn_con.StarToLight();
                //pawnChoseTCS.TrySetResult(pawn_con.pawn_id);
            }
            else if(pawn_confirm_button.interactable)
            {
                RectTransform confirm_btn_rect = pawn_confirm_button.GetComponent<RectTransform>();
                
                Vector3 mousePosition = Input.mousePosition;
                Vector3 worldMousePosition = uiCamera.ScreenToWorldPoint(mousePosition);
                if (!RectTransformUtility.RectangleContainsScreenPoint(confirm_btn_rect, worldMousePosition))
                {
                    pawn_confirm_button.interactable = false;
                }
                // 取消选中
                if (choosen_pawn != null)
                {
                    PawnController temp_con = choosen_pawn.GetComponent<PawnController>();
                    temp_con.StopLighting();
                }
            }
        }
        else
        {
            pawn_confirm_button.interactable = false;
            // 取消选中
            if (choosen_pawn != null)
            {
                PawnController temp_con = choosen_pawn.GetComponent<PawnController>();
                temp_con.StopLighting();
            }
        }
    }
}
