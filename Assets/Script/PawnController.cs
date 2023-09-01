using DG.Tweening;
using System;
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

    // ������Ծ�Ķ���켣��
         

    // ��¼һ����Ծ��״̬
    private JumpInfo jump_target;       // ��Ծ��Ŀ��λ��
    private bool isJumping = false;       // �Ƿ�������Ծ
    private float jumpStartTime;          // ��Ծ��ʼʱ��
    private Vector3 jump_start_pos;

    private bool isBlinking = false;
    private bool isLighting = false;

    private TaskCompletionSource<bool> jumpTCS;

    // ������Ⱦ���
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
        await this.transform.DOJump(info.jump_target, info.jump_height, 1, info.jump_duration).AsyncWaitForCompletion();
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

        if (!isLighting && isBlinking)
        {
            float e_light = Mathf.Abs(Mathf.Sin(Time.time) / 1.4f);
            block.SetColor("_EmitLight", new Vector4(e_light, e_light, e_light, 1));
            render.SetPropertyBlock(block);
        }
    }
}

