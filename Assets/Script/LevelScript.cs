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

    private List<int> human_player_index = new List<int>();
    private List<PlayerColor> players = new List<PlayerColor>();
    private List<int> playerPawnCount = new List<int>();
    private int cur_player_index = 0;

    // ��������¼�
    private TaskCompletionSource<int> pawnChoseTCS;
    private TaskCompletionSource<bool> rollDiceButtonClickTCS;

    // ��ѡ�е�����
    private GameObject choosen_pawn = null;

    // Current player and his available pawn
    List<int> cur_avai_pawnidx;
    public RawImage cur_player_image;
    private List<Texture> player_textures = new List<Texture>();

    bool is_choosing_pawn = false;

    // ������Ч
    public ParticleSystem diceButtonParticle;
    public ParticleSystem playerIconParticle;

    // �л����ͼƬ
    public GameObject switchPlayerImage;

    // ������ԶΪ6
    public bool diceSixForever = true;

    // ��Ϸ�����������
    private List<List<int>> playerSteps = new List<List<int>>();
    public List<Text> rankTexts;
    public List<RawImage> rankPlayerIcons;
    public List<Text> rankStepTexts;
    public GameObject gameEndPanel;

    private void ChangePlayer()
    {
        ++cur_player_index;
        if (cur_player_index >= players.Count)
        {
            cur_player_index = 0;
        }
        cur_player_image.texture = player_textures[cur_player_index];
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
        foreach (int idx in human_player_index)
        {
            remain += playerPawnCount[idx];
        }
        if (remain != 0)
        {
            return false;
        }

        playerSteps.Sort((List<int> a, List<int> b) =>
        {
            return a[1] - b[1];
        });

        gameEndPanel.SetActive(true);
        int rank = 0;
        foreach(List<int> p in playerSteps)
        {
            if (!human_player_index.Contains(p[0]))
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
    async private Task GameMainLoop()
    {
        chess_board_mgr.Debug_PawnPlaceTo(3, 11);
        for(int i=0; i<3; ++i)
        {
            chess_board_mgr.PawnRemoveFromBoard(i);
            playerPawnCount[cur_player_index] -= 1;
        }

        bool first = true;
        while (true)
        {
            // �����Ϸ�Ƿ����
            if (CheckGameEnd())
            {
                break;
            }
            
            // �л����
            if (!first)
            {
                ChangePlayer();
                if (playerPawnCount[cur_player_index] == 0)
                {
                    continue;
                }
                await PlaySwitchNote();
            }
            else
            {
                first = false;
            }
            


            // ҡ����
            int dice_value;
            if (human_player_index.Contains(cur_player_index))
            {
                dice_value = await PlayerRollDice();
            }
            else
            {
                dice_value = await AIRollDice();
            }

            // ���ҡ���ӵĴ�����һ
            playerSteps[cur_player_index][1] += 1;

            // ��ȡ����ܹ�ѡ�������
            cur_avai_pawnidx = chess_board_mgr.GetAvailablePawnIdx(players[cur_player_index], dice_value);
            int pawn_id;
            if (cur_avai_pawnidx.Count != 0)
            {
                // ��ȡ���ѡ�������
                if(human_player_index.Contains(cur_player_index))
                {
                    pawn_id = await PlayerChoosePawn();
                }
                else
                {
                    pawn_id = AIChoosePawn(cur_avai_pawnidx);
                }
            }
            else
            {
                continue;
            }

            // ���������ƶ�
            bool pawn_on_walkpath = chess_board_mgr.IsPawnOnWalkPath(pawn_id);
            if(!pawn_on_walkpath)
            {
                await chess_board_mgr.PawnMoveToStart(pawn_id);
                if(human_player_index.Contains(cur_player_index))
                {
                    dice_value = await PlayerRollDice();
                }
                else
                {
                    dice_value = await AIRollDice();
                }
            }
            await chess_board_mgr.PawnMoveForward(pawn_id, dice_value);

            // ���������ƶ�����ͬ��ɫ����Ծ
            if (chess_board_mgr.PawnCheckSkip(pawn_id))
            {
                await chess_board_mgr.PawnDoSkip(pawn_id);
            }

            // ���������������
            List<int> kick_back_list = chess_board_mgr.PawnCheckBeatOther(pawn_id);
            if(kick_back_list.Count > 0)
            {
                await chess_board_mgr.PawnKickBackToStandby(kick_back_list);
            }

            // ���������ƶ����յ�
            if (chess_board_mgr.PawnCheckToEnd(pawn_id))
            {
                await Task.Delay(1000);
                chess_board_mgr.PawnRemoveFromBoard(pawn_id);
                playerPawnCount[cur_player_index] -= 1;
            }

            

        }
    }

    // Start is called before the first frame update
    void Start()
    {
        int playerCount=1, aiCount=1;
        if (PlayerPrefs.HasKey("playerCount") && PlayerPrefs.HasKey("aiCount"))
        {
            playerCount = PlayerPrefs.GetInt("playerCount");
            aiCount = PlayerPrefs.GetInt("aiCount");
        }

        List<string> text_name = new List<string> { "red_pawn", "green_pawn", "blue_pawn", "yellow_pawn" };
        for(int i=0; i<4; ++i)
        {
            Texture texture = Resources.Load<Texture>("images/" + text_name[i]);
            player_textures.Add(texture);
        }
        
        
        for (int i=0; i<playerCount+aiCount; ++i)
        {
            players.Add((PlayerColor)i);
            playerPawnCount.Add(4);
            playerSteps.Add(new List<int> { i, 0 });
        }
        for(int i=0; i<playerCount; ++i)
        {
            human_player_index.Add(i);
        }
        
        chess_board_mgr.InitGameMode(players);
        pawn_confirm_button.gameObject.SetActive(false);
        main_camera_controller = main_camera.GetComponent<CameraController>();
        cur_player_image.texture = player_textures[cur_player_index];
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

    public async Task<int> AIRollDice()
    {
        int dice_value = await RollADice();
        await main_camera_controller.LookAtDiceValue(dice);
        return dice_value;
    }

    public async Task<int> PlayerRollDice()
    {
        await dice.GetComponent<DiceController>().SetDiceAppear(GameObject.Find("dice_init_pos").transform.position);
        Vector3 target_pos = dice.transform.position;
        target_pos.z += 1.8f;
        await diceGirl.GetComponent<DiceGrilController>().MoveToPositionXZ(target_pos, 2);
        main_camera_controller.FocusOnTarget(dice);
        await main_camera_controller.MoveToPosition(GameObject.Find("throw_dice_camera_pos"), 1.5f);
        

        // ���ð�ť�ɽ���״̬
        diceButtonParticle.gameObject.SetActive(true);
        diceButtonParticle.Play();
        roll_dice_button.interactable = true;
        rollDiceButtonClickTCS = new TaskCompletionSource<bool>();
        await rollDiceButtonClickTCS.Task;

        diceGirl.GetComponent<DiceGrilController>().DoThrowAction();
        await Task.Delay(400);

        // ���ð�ť���ɽ���״̬
        roll_dice_button.interactable = false;
        diceButtonParticle.Pause();
        diceButtonParticle.gameObject.SetActive(false);
        
        // ������
        int dice_value = await RollADice();
        await main_camera_controller.LookAtDiceValue(dice);
        diceGirl.GetComponent<DiceGrilController>().MoveToInitTrans(2);
        return dice_value;
    }

    private async Task<int> RollADice()
    {
        // ��ȡ���ӵĵ���
        //dice.transform.position = dice_anchor.transform.position;
        DiceController dice_con = dice.GetComponent<DiceController>();
        dice_con.StartToRoll();
        int dice_value = await dice_con.GetDiceValue();
        Debug.Log(dice_value);
        dice_value = diceSixForever ? 6 : dice_value;
        return dice_value;
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
            // ����ѡ�е�����ȡ��ѡ��
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

                // ѡ������
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
                // ȡ��ѡ��
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
            // ȡ��ѡ��
            if (choosen_pawn != null)
            {
                PawnController temp_con = choosen_pawn.GetComponent<PawnController>();
                temp_con.StopLighting();
            }
        }
    }
}
