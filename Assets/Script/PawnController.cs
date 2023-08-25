using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
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

    // ������Ծ�Ķ���켣��
         

    // ��¼һ����Ծ��״̬
    private JumpInfo jump_target;       // ��Ծ��Ŀ��λ��
    private bool isJumping = false;       // �Ƿ�������Ծ
    private float jumpStartTime;          // ��Ծ��ʼʱ��
    private Vector3 jump_start_pos;


    private TaskCompletionSource<bool> jumpTCS;

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

    void Update()
    {
        // ����һ����Ծ����
        if (isJumping)
        {
            float timeSinceJumpStarted = Time.time - jumpStartTime;
            float normalizedTime = Mathf.Clamp01(timeSinceJumpStarted / jump_target.jump_duration);
            float yOffset = Mathf.Sin(normalizedTime * Mathf.PI) * jump_target.jump_height;
            yOffset += (jump_target.jump_target.y - jump_start_pos.y) * normalizedTime;
            float zOffset = (jump_target.jump_target.z - jump_start_pos.z) * normalizedTime;
            float xOffset = (jump_target.jump_target.x - jump_start_pos.x) * normalizedTime;

            // ���������߼����µ�Y����
            float newX = jump_start_pos.x + xOffset;
            float newY = jump_start_pos.y + yOffset;
            float newZ = jump_start_pos.z + zOffset;

            // �����µ�λ��
            Vector3 newPosition = new Vector3(newX, newY, newZ);
            transform.position = newPosition;

            // �ж���Ծ�Ƿ����
            if (timeSinceJumpStarted >= jump_target.jump_duration)
            {
                jump_start_pos = transform.position; // �����µ�����λ��
                jumpTCS.TrySetResult(true);
            }
        }
    }
}
