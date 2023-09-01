using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;
using static UnityEngine.GraphicsBuffer;
using DG.Tweening;

public class CameraController : MonoBehaviour
{
    // Start is called before the first frame update
    Camera c_camera;
    private Vector3 init_position;
    private Quaternion init_rotation;
    // 缓动曲线
    public AnimationCurve easingCurve;

    // 转向
    private bool is_turning = false;

    // 瞄准目标
    private GameObject focus_on_target;
    private bool is_focus_on = false;


    void Start()
    {
        this.c_camera = GetComponent<Camera>();
        this.init_position = this.transform.position;
        this.init_rotation = this.transform.rotation;
    }

    private async Task MoveToPosition(Vector3 moving_target, float duration)
    {
        TaskCompletionSource<bool> m_TCS = new TaskCompletionSource<bool>();
        this.transform.DOMove(moving_target, duration).SetEase(Ease.InOutSine).OnComplete(() => m_TCS.TrySetResult(true));
        await m_TCS.Task;
    }

    public async Task MoveToPosition(GameObject dice, float duration)
    {
        Vector3 moving_t = dice.transform.position;
        await MoveToPosition(moving_t, duration);
    }

    public async Task LookAtDiceValue(GameObject dice, float turning_duration=0.2f, float moving_duration = 0.6f,
        float checking_duration = 1.0f, float recover_duration = 0.8f)
    {
        // 转向目标 
        await TurnToTarget(dice, turning_duration);
        // 注视目标
        FocusOnTarget(dice);
        // 移动到目标上方
        Vector3 moving_t = dice.transform.position;
        moving_t.y += 40;
        Vector3 look_direction = transform.position - dice.transform.position;
        look_direction.Normalize();
        moving_t += look_direction * 0.1f;
        await MoveToPosition(moving_t, moving_duration);
        // 停止注视目标
        DefocusOnTarget();
        // 停顿
        await Task.Delay((int)(checking_duration * 1000));
        // 回到原始位置
        await this.RecoverView(recover_duration);
    }

    private async Task TurnToTarget(Quaternion target_rotation, float duration)
    {
        is_turning = true;
        TaskCompletionSource<bool> t_TCS = new TaskCompletionSource<bool>();
        c_camera.transform.DORotateQuaternion(target_rotation, duration).SetEase(Ease.InOutSine).OnComplete(()=>t_TCS.SetResult(true));
        await t_TCS.Task;
        is_turning = false;
    }

    public async Task TurnToTarget(GameObject target, float duration)
    {
        Vector3 targe_direction = target.transform.position - c_camera.transform.position;
        Quaternion target_r = Quaternion.LookRotation(targe_direction);
        await TurnToTarget(target_r, duration);
    }

    public void FocusOnTarget(GameObject target)
    {
        focus_on_target = target;
        is_focus_on = true;
    }

    public void DefocusOnTarget()
    {
        is_focus_on = false;
    }

    public async Task RecoverView(float duration)
    {
        Task t1 = MoveToPosition(init_position, duration);
        Task t2 = TurnToTarget(init_rotation, duration);
        Task t_sum = Task.WhenAll(t1, t2);
        await t_sum;
    }


    // Update is called once per frame
    void Update()
    {
        if (!is_turning && is_focus_on)
        {
            c_camera.transform.LookAt(focus_on_target.transform);
        }

    }
}
