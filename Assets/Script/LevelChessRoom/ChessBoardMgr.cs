using JetBrains.Annotations;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Serialization;
using UnityEditor;
using UnityEngine;

public enum PlayerColor
{
    Red = 0, Green = 1, Blue = 2, Yellow = 3, None = 4
}
public class PathPlane
{
    public Vector3 position;
    public PlayerColor color;
    public int index = -1;
    public int index_skip_to = -1;
    public int index_end_to = -1;
} 

public class ChessBoardMgr : MonoBehaviour
{
    private Dictionary<int, GameObject> pawn_ref = new Dictionary<int, GameObject> ();

    // Chess Board Setting
    private static float height_offset = 2.6f, horizon_offset = 3f;
    private float[,] standby_offset = new float[4,2]{ { horizon_offset, horizon_offset }, { -horizon_offset, horizon_offset }, 
                                                    {horizon_offset, -horizon_offset }, {-horizon_offset, -horizon_offset } };
    Dictionary<int, int> special_skip_to = new Dictionary<int, int> { { 33, 45 }, { 19, 31 }, { 5, 17 }, { 47, 3 }, { 53, -1 }, { 39, -1 }, { 25, -1 }, {11, -1} };
    Dictionary<int, int> index_end_to = new Dictionary<int, int> { { 11, 56 }, { 53, 61 }, { 39, 66 }, { 25, 71 } };
    private readonly int[] start_idx = { 77, 78, 79, 80 };
    private readonly int[] start_next_idx = { 14, 0, 42, 28 };
    private readonly int[] last_end_plane = { 60, 65, 70, 75 };
    const int end_idx = 76;

    private GameObject chess_board;
    private List<PathPlane> walk_path = new List<PathPlane>();
    
    // standby position
    private List<Vector3> standby_pos = new List<Vector3>(16);
    

    private List<GameObject> pawn_prefabs = new List<GameObject>();

    private int GetStandbyIdxByColorAndIdx(PlayerColor color, int idx)
    {
        return (int)color * 4 + idx;
    }

    private int GetPlaneIdxByStandbyIdx(int standby_idx)
    {
        return -standby_idx - 1;
    }
    private int GetStandbyIdxByPlaneIdx(int plane_idx)
    {
        return -(plane_idx + 1);
    }

    public int GetEmptyStandbyIdx(PlayerColor color)
    {
        List<int> cur_standby = new List<int>();
        for(int i=0; i<4; ++i)
        {
            cur_standby.Add(GetStandbyIdxByColorAndIdx(color, i));
        }
        foreach(var k_v in pawn_ref)
        {
            int plane_idx = k_v.Value.GetComponent<PawnController>().plane_idx;
            for(int i=0; i<4; ++i)
            {
                if (cur_standby[i] == GetStandbyIdxByPlaneIdx(plane_idx))
                {
                    cur_standby[i] = -1;
                }
            }
            
        }
        for (int i = 0; i < 4; ++i)
        {
            if (cur_standby[i] != -1)
            {
                return cur_standby[i];
            }
        }
        return -1;
    } 
    public void InitGameMode(List<PlayerColor> players)
    {
        // 初始化77个棋盘路径的信息
        for (int i=0; i<77; ++i)
        {
            PathPlane plane = new PathPlane();
            GameObject board_plane = GameObject.Find("ChessBoard/Plane" + (i + 1).ToString());
            if(board_plane == null)
            {
                print(i + "not found");
                return;
            }
            Vector3 pos = board_plane.transform.position;
            pos.y += height_offset;
            plane.position = pos;
            plane.index = i;
            
            Renderer render = board_plane.GetComponent<Renderer>();
            string mat_name = render.material.name;
            switch (mat_name)
            {
                case "Blue (Instance)":
                    plane.color = PlayerColor.Blue; break;
                case "Green (Instance)":
                    plane.color = PlayerColor.Green; break;
                case "Red (Instance)":
                    plane.color = PlayerColor.Red; break;
                case "Yellow (Instance)":
                    plane.color = PlayerColor.Yellow; break;
                default:
                    plane.color = PlayerColor.None; break;
            }
            walk_path.Add(plane);
        }

        // 初始化4个玩家起飞位置的信息
        List<string> start_names = new List<string> { "start_red", "start_green", "start_blue", "start_yellow" };
        for (int i = 0; i < 4; ++i)
        {
            GameObject start = GameObject.Find(start_names[i]);
            Vector3 pos = start.transform.position;
            pos.y += height_offset;
            PathPlane plane = new PathPlane();
            plane.position = pos;
            plane.index = 77 + i;
            plane.color = PlayerColor.None;
            
            walk_path.Add(plane);
        }

        // 初始化每个方格跳跃至下一方格的信息
        Dictionary<PlayerColor, int> next_pos_ref = new Dictionary<PlayerColor, int>{
            {PlayerColor.Red, -1 }, {PlayerColor.Blue, -1 }, {PlayerColor.Green, -1 }, {PlayerColor.Yellow, -1 }
        };
        for (int plane_idx = 55; plane_idx >= 0; --plane_idx)
        {
            PlayerColor plane_color = walk_path[plane_idx].color;
            if (plane_color != PlayerColor.None)
            {
                if (next_pos_ref[plane_color] == -1)
                {
                    next_pos_ref[plane_color] = plane_idx;
                }
                else
                {
                    walk_path[plane_idx].index_skip_to = next_pos_ref[plane_color];
                    next_pos_ref[plane_color] = plane_idx;
                }
            }
        }
        foreach(var tunple in special_skip_to)
        {
            walk_path[tunple.Key].index_skip_to = tunple.Value;
        } 
        foreach(var tunple in index_end_to)
        {
            walk_path[tunple.Key].index_end_to = tunple.Value;
        }

        // 载入每种棋子的预制体
        pawn_prefabs.Add((GameObject)Resources.Load("Prefabs/Castle_Red"));
        pawn_prefabs.Add((GameObject)Resources.Load("Prefabs/Castle_Green"));
        pawn_prefabs.Add((GameObject)Resources.Load("Prefabs/Castle_Blue"));
        pawn_prefabs.Add((GameObject)Resources.Load("Prefabs/Castle_Yellow"));

        List<string> strings = new List<string> { "Standby_red", "Standby_green", "Standby_blue", "Standby_yellow" };
        for(int s_idx=0; s_idx < 4; ++s_idx)
        {
            for (int offset_idx = 0; offset_idx < 4; ++offset_idx)
            {
                GameObject standby_plane = GameObject.Find(strings[s_idx]);
                Vector3 pos = standby_plane.transform.position;
                pos.y += height_offset;
                pos.x += standby_offset[offset_idx,0];
                pos.z += standby_offset[offset_idx,1];
                standby_pos.Add(pos);
            }
        }

        foreach (var pawn in pawn_ref)
        {
            Destroy(pawn.Value);
        }
        pawn_ref.Clear();

        int pawn_id = 0;
        foreach(var player_color in players)
        {
            for(int idx = 0; idx<4; ++idx)
            {
                GameObject pawn = Instantiate(pawn_prefabs[(int)player_color]);
                PawnController controller = pawn.GetComponent<PawnController>();
                int standby_idx = GetStandbyIdxByColorAndIdx(player_color, idx);
                controller.Init(pawn_id, player_color, standby_pos[standby_idx]);
                controller.plane_idx = GetPlaneIdxByStandbyIdx(standby_idx);
                pawn_ref[pawn_id] = pawn;
                pawn_id++;
            }
        }
    }

    public bool IsPawnOnWalkPath(int pawn_id)
    {
        return pawn_ref[pawn_id].GetComponent<PawnController>().plane_idx >= 0;
    }

    public async Task PawnMoveToStart(int pawn_id)
    {
        GameObject pawn = pawn_ref[pawn_id];
        PawnController pawn_con = pawn.GetComponent<PawnController>();
        PlayerColor pawn_color = pawn_con.color;
        pawn_con.plane_idx = start_idx[(int)pawn_color];
        JumpInfo jp = new JumpInfo(walk_path[pawn_con.plane_idx].position, 4, 0.5f, 0.1f);
        await pawn_con.JumpToPoint(jp);

    } 

    public List<int> GetAvailablePawnIdx(PlayerColor color, int dice_value)
    {
        List<int> ret = new List<int>();
        foreach(var k_v in pawn_ref)
        {
            if(k_v.Value.GetComponent<PawnController>().color == color)
            {
                if(dice_value == 6)
                {
                    ret.Add(k_v.Key);
                }
                else if(k_v.Value.GetComponent<PawnController>().plane_idx > 0)
                {
                    ret.Add(k_v.Key);
                }
                
            }
        }
        return ret;
    } 

    public async Task PawnMoveForward(int pawn_id, int step)
    {
        GameObject pawn = pawn_ref[pawn_id];
        PawnController controller = pawn.GetComponent<PawnController>();
        if(controller == null)
        {
            Debug.LogError("Given object is not a pawn");
            return;
        }
        int direction = 1;
        while (step != 0)
        {
            int next_plane_idx = controller.plane_idx;
            PathPlane cur_plane = walk_path[controller.plane_idx];
            if (controller.plane_idx == 55)  // 棋盘转角处
            {
                next_plane_idx = 0;
            }
            else if (cur_plane.color == controller.color && cur_plane.index_end_to != -1) // 终点道路转角处
            {
                next_plane_idx = cur_plane.index_end_to;
            }
            else if(last_end_plane.Contains(cur_plane.index) && direction == 1)  // 终点道路的最后一个落点
            {
                next_plane_idx = end_idx;
            }
            else if(cur_plane.index == end_idx)  // 达终点位置
            {
                next_plane_idx = last_end_plane[(int)controller.color];
                direction = -1;
            }
            else if (start_idx.Contains(cur_plane.index))
            {
                next_plane_idx = start_next_idx[(int)controller.color];
            }
            else  // 正常前进
            {
                next_plane_idx += direction;
            }
            controller.plane_idx = next_plane_idx;
            await controller.JumpToPoint(new JumpInfo(walk_path[next_plane_idx].position));
            --step;
        }
    }

    public void PawnStartToBlink(int pawn_id)
    {
        GameObject pawn = pawn_ref[pawn_id];
        PawnController controller = pawn.GetComponent<PawnController>();
        controller.StartToBlink();
    }

    public void PawnStopBlinking(int pawn_id)
    {
        GameObject pawn = pawn_ref[pawn_id];
        PawnController controller = pawn.GetComponent<PawnController>();
        controller.StopBlinking();
    }

    public void PawnStartLight(int pawn_id)
    {
        GameObject pawn = pawn_ref[pawn_id];
        PawnController controller = pawn.GetComponent<PawnController>();
        controller.StarToLight();
    }

    public void PawnStopLighting(int pawn_id)
    {
        GameObject pawn = pawn_ref[pawn_id];
        PawnController controller = pawn.GetComponent<PawnController>();
        controller.StopLighting();
    }

    public GameObject GetPawnObject(int pawn_id)
    {
        return pawn_ref[pawn_id];
    }

    public bool PawnCheckToEnd(int pawn_id)
    {
        PawnController pawn_con = pawn_ref[pawn_id].GetComponent<PawnController>();
        return pawn_con.plane_idx == end_idx;
    }

    public void PawnRemoveFromBoard(int pawn_id)
    {
        Destroy(pawn_ref[pawn_id]);
        pawn_ref.Remove(pawn_id);
    }

    public List<int> PawnCheckBeatOther(int pawn_id)
    {
        GameObject pawn = pawn_ref[pawn_id];
        PawnController pawn_con = pawn.GetComponent<PawnController>();
        int checked_plane_idx = pawn_con.plane_idx;
        List<int> beat_side = new List<int>();
        List<int> beated_side = new List<int>();

        foreach(var k_v in pawn_ref)
        {
            PawnController temp_pawn_con = k_v.Value.GetComponent<PawnController>();
            if(temp_pawn_con.plane_idx == checked_plane_idx)
            {
                if(temp_pawn_con.color == pawn_con.color)
                {
                    beat_side.Add(k_v.Key);
                }
                else
                {
                    beated_side.Add(k_v.Key);
                }
            }
        }
        if (beat_side.Count >= beated_side.Count && beated_side.Count != 0)
        {
            return beated_side;
        }
        return new List<int>();

    }

    public async Task PawnKickBackToStandby(List<int> pawn_ids)
    {
        Task[] all_pawn_task = new Task[pawn_ids.Count];
        for(int i=0; i<pawn_ids.Count; ++i)
        {
            int pawn_id = pawn_ids[i];
            PawnController temp_pawn_con = pawn_ref[pawn_id].GetComponent<PawnController>();
            int standby_idx = GetEmptyStandbyIdx(temp_pawn_con.color);
            Vector3 target_point = standby_pos[standby_idx];
            all_pawn_task[i] = temp_pawn_con.KickBackToPoint(new JumpInfo(target_point, 20, 4f, 0.2f));
            temp_pawn_con.plane_idx = GetPlaneIdxByStandbyIdx(standby_idx);
        }
        await Task.WhenAll(all_pawn_task);
    }

    public bool PawnCheckSkip(int pawn_id)
    {
        GameObject pawn = pawn_ref[pawn_id];
        PawnController controller = pawn.GetComponent<PawnController>();
        int target_plane_idx = walk_path[controller.plane_idx].index_skip_to;
        return controller.color == walk_path[controller.plane_idx].color && target_plane_idx != -1;
    }

    public async Task<bool> PawnDoSkip(int pawn_id)
    {
        GameObject pawn = pawn_ref[pawn_id];
        PawnController controller = pawn.GetComponent<PawnController>();
        int target_plane_idx = walk_path[controller.plane_idx].index_skip_to;
        if (PawnCheckSkip(pawn_id))
        {
            await controller.JumpToPoint(new JumpInfo(walk_path[target_plane_idx].position));
            controller.plane_idx = target_plane_idx;
            return true;
        }
        return false;
    } 

    public void Debug_PawnPlaceTo(int pawn_id, int path_idx)
    {
        GameObject pawn = pawn_ref[pawn_id];
        PawnController controller = pawn.GetComponent<PawnController>();
        if (controller == null)
        {
            Debug.LogError("Object is not a pawn");
        }
        pawn.transform.position = walk_path[path_idx].position;
        controller.plane_idx = path_idx;
        
    }

    // Update is called once per frame
    void Update()
    {

    }
}
