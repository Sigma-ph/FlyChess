using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml.Serialization;
using UnityEditor.UI;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

public struct JumpInfo
{
    public Vector3 jump_target;
    public float jump_height;
    public float jump_duration;
    public float interval_duration;

    public JumpInfo(Vector3 jump_target, float jump_height = 2, float jump_duration = 0.4f, float interval_duration = 0.1f)
    {
        this.jump_target = jump_target;
        this.jump_height = jump_height; 
        this.jump_duration = jump_duration;
        this.interval_duration = interval_duration;
    }
}

public class PawnController : MonoBehaviour
{
    public int pawn_id = -1;
    public int plane_idx;
    public PlayerColor color;
    public Vector3 world_pos
    {
        get => this.transform.position;
    }

    [Header("Jump Settings")]

    // 连续跳跃的多个轨迹点
         

    // 记录一次跳跃的状态
    private JumpInfo jump_target;       // 跳跃的目标位置
    private bool isJumping = false;       // 是否正在跳跃
    private float jumpStartTime;          // 跳跃开始时间
    private Vector3 jump_start_pos;

    private bool isBlinking = false;
    private bool isLighting = false;

    private TaskCompletionSource<bool> jumpTCS;

    // 棋子渲染组件
    MeshRenderer render;
    MaterialPropertyBlock block;

    public void Start()
    {
        this.render = this.gameObject.GetComponentInChildren<MeshRenderer>();
        block = new MaterialPropertyBlock();
        
    }
    public void Init(int id, PlayerColor color, Vector3 pos)
    {
        this.pawn_id = id;
        this.color = color;
        this.transform.position = pos;

        
    }

    public async Task KickBackToPoint(JumpInfo info)
    {
        await JumpToPoint(info);
    }

    public async Task JumpToPoint(JumpInfo info)
    {
        jumpTCS = new TaskCompletionSource<bool>();
        jumpStartTime = Time.time;
        jump_start_pos = this.transform.position;
        jump_target = info;
        isJumping = true;
        await jumpTCS.Task;
        isJumping = false;
        await Task.Delay((int)info.jump_duration * 1000);
    } 

    public void StartToBlink()
    {
        isBlinking = true;
    }

    public void StopBlinking()
    {
        isBlinking = false;
        block.SetColor("_EmitLight", new Vector4(0.0f, 0.0f, 0.0f, 1));
        render.SetPropertyBlock(block);
    }

    public void StarToLight()
    {
        block.SetColor("_EmitLight", new Vector4(0.9f, 0.9f, 0.9f, 1));
        render.SetPropertyBlock(block);
        isLighting = true;
    }

    public void StopLighting()
    {
        block.SetColor("_EmitLight", new Vector4(0.0f, 0.0f, 0.0f, 1));
        render.SetPropertyBlock(block);
        isLighting = false;
    }

    void Update()
    {
        // 处理一次跳跃过程
        if (isJumping)
        {
            float timeSinceJumpStarted = Time.time - jumpStartTime;
            float normalizedTime = Mathf.Clamp01(timeSinceJumpStarted / jump_target.jump_duration);
            float yOffset = Mathf.Sin(normalizedTime * Mathf.PI) * jump_target.jump_height;
            yOffset += (jump_target.jump_target.y - jump_start_pos.y) * normalizedTime;
            float zOffset = (jump_target.jump_target.z - jump_start_pos.z) * normalizedTime;
            float xOffset = (jump_target.jump_target.x - jump_start_pos.x) * normalizedTime;

            // 根据抛物线计算新的Y坐标
            float newX = jump_start_pos.x + xOffset;
            float newY = jump_start_pos.y + yOffset;
            float newZ = jump_start_pos.z + zOffset;

            // 计算新的位置
            Vector3 newPosition = new Vector3(newX, newY, newZ);
            transform.position = newPosition;

            // 判断跳跃是否结束
            if (timeSinceJumpStarted >= jump_target.jump_duration)
            {
                jump_start_pos = transform.position; // 设置新的棋子位置
                jumpTCS.TrySetResult(true);
            }

            
        }

        if (!isLighting && isBlinking)
        {
            float e_light = Mathf.Abs(Mathf.Sin(Time.time) / 1.4f);
            block.SetColor("_EmitLight", new Vector4(e_light, e_light, e_light, 1));
            render.SetPropertyBlock(block);
        }
    }
}
