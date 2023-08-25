using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using System.Threading.Tasks;
using UnityEngine.UI;
using UnityEngine.Rendering;

public class LevelScript : MonoBehaviour
{
    public GameObject dice;
    public GameObject dice_anchor;
    public Button roll_dice_button;
    public Button pawn_confirm_button;
    public ChessBoardMgr chess_board_mgr;
    public Camera main_camera;
    public PlayerColor human_player_color = PlayerColor.Red;

    private CameraController main_camera_controller;

    private List<PlayerColor> players = new List<PlayerColor> { PlayerColor.Red, PlayerColor.Blue, PlayerColor.Green, PlayerColor.Yellow };
    private int cur_player_index = 0;
    private TaskCompletionSource<int> pawnChoseTCS;
    private TaskCompletionSource<bool> rollDiceButtonClickTCS;

    private GameObject choosen_pawn = null;

    // Current player and his available pawn
    List<int> cur_avai_pawnidx;

    bool is_choosing_pawn = false;

    private void ChangePlayer()
    {
        ++cur_player_index;
        if (cur_player_index >= players.Count)
        {
            cur_player_index = 0;
        }
    }

    async private Task GameMainLoop()
    {
        while (true)
        {
            //await main_camera.GetComponent<CameraController>().SlideToTarget(chess_board_mgr.GetPawnObject(0), 1f);

            // 摇骰子
            int dice_value;
            if (players[cur_player_index] == human_player_color)
            {
                dice_value = await PlayerRollDice();
            }
            else
            {
                dice_value = await AIRollDice();
            }

            // 获取玩家能够选择的棋子
            cur_avai_pawnidx = chess_board_mgr.GetAvailablePawnIdx(players[cur_player_index], dice_value);
            int pawn_id;
            if (cur_avai_pawnidx.Count != 0)
            {
                // 获取玩家选择的棋子
                if(players[cur_player_index] == human_player_color)
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
                ChangePlayer();
                continue;
            }

            // 处理棋子移动
            bool pawn_on_walkpath = chess_board_mgr.IsPawnOnWalkPath(pawn_id);
            if(!pawn_on_walkpath)
            {
                await chess_board_mgr.PawnMoveToStart(pawn_id);
                if(players[cur_player_index] == human_player_color)
                {
                    dice_value = await PlayerRollDice();
                }
                else
                {
                    dice_value = await AIRollDice();
                }
            }
            await chess_board_mgr.PawnMoveForward(pawn_id, dice_value);

            // 处理棋子移动到终点
            if (chess_board_mgr.PawnCheckToEnd(pawn_id))
            {
                chess_board_mgr.PawnRemoveFromBoard(pawn_id);
            }

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

            // 切换玩家
            ChangePlayer();

        }
    }

    // Start is called before the first frame update
    void Start()
    {
        
        chess_board_mgr.InitGameMode(players);
        pawn_confirm_button.gameObject.SetActive(false);
        main_camera_controller = main_camera.GetComponent<CameraController>();

        //chess_board_mgr.Debug_PawnPlaceTo(0, 10);
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
        is_choosing_pawn = true;
        pawn_confirm_button.interactable = false;
        pawn_confirm_button.gameObject.SetActive(true);

        pawnChoseTCS = new TaskCompletionSource<int>();
        int choosen_pawnid = await pawnChoseTCS.Task;
        Debug.Log("Choosen Pawn ID:" + choosen_pawnid.ToString());

        is_choosing_pawn = false;
        pawn_confirm_button.gameObject.SetActive(false);
        pawn_confirm_button.interactable = false;
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
        roll_dice_button.interactable = true;
        rollDiceButtonClickTCS = new TaskCompletionSource<bool>();
        await rollDiceButtonClickTCS.Task;
        roll_dice_button.interactable = false;
        int dice_value = await RollADice();

        await main_camera_controller.LookAtDiceValue(dice);
        dice_value = 6;
        return dice_value;
    }

    private async Task<int> RollADice()
    {
        // 获取骰子的点数
        dice.transform.position = dice_anchor.transform.position;
        DiceController dice_con = dice.GetComponent<DiceController>();
        dice_con.StartToRoll();
        int dice_value = await dice_con.GetDiceValue();
        Debug.Log(dice_value);
        dice_value = 6;
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
            GameObject hit_obj = hit.collider.gameObject;
            PawnController pawn_con = hit_obj.gameObject.GetComponent<PawnController>();
            if (pawn_con != null && cur_avai_pawnidx.Contains(pawn_con.pawn_id))
            {
                this.choosen_pawn = hit_obj;
                pawn_confirm_button.interactable = true;
                //pawnChoseTCS.TrySetResult(pawn_con.pawn_id);
            }
            else if(pawn_confirm_button.IsActive())
            {
                RectTransform confirm_btn_rect = pawn_confirm_button.GetComponent<RectTransform>();
                Vector3 mousePosition = Input.mousePosition;
                Vector3 worldMousePosition = main_camera.ScreenToWorldPoint(mousePosition);
                if (!RectTransformUtility.RectangleContainsScreenPoint(confirm_btn_rect, mousePosition))
                {
                    pawn_confirm_button.interactable = false;
                }

            }
        }
        else
        {
            pawn_confirm_button.interactable = false;
        }
    }
}
