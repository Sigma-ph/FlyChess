using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;
using static UnityEngine.GraphicsBuffer;

public class CameraController : MonoBehaviour
{
    // Start is called before the first frame update
    Camera c_camera;
    private Vector3 init_position;
    private Quaternion init_rotation;
    // 缓动曲线
    public AnimationCurve easingCurve;

    // 过渡动画参数
    // 转向
    private Quaternion turning_target;
    private Quaternion turning_source;
    private float turning_duration;
    private float turning_start_time;
    private bool is_turning = false;
    // 移动
    private bool is_moving = false;
    private Vector3 moving_target;
    private Vector3 moving_source;
    private float moving_duration;
    private float moving_start_time;
    // 瞄准目标
    private GameObject focus_on_target;
    private bool is_focus_on = false;

    // Task完成事件
    private TaskCompletionSource<bool> turningTCS;  // 转向完成事件
    private TaskCompletionSource<bool> movingTCS;  // 移动完成事件

    void Start()
    {
        this.c_camera = GetComponent<Camera>();
        this.init_position = this.transform.position;
        this.init_rotation = this.transform.rotation;
    }

    public static Quaternion RotateObject(Transform targetTransform)
    {
        // Get the current rotation angles of the object
        Vector3 currentRotation = targetTransform.eulerAngles;

        // Calculate the new rotation angles with x-axis aligned to world xy-plane
        Vector3 newRotation = new Vector3(currentRotation.x, currentRotation.y, 0);

        // Create a new quaternion based on the new rotation angles
        Quaternion rotationQuaternion = Quaternion.Euler(newRotation);

        // Ensure the y-axis direction is non-negative in world coordinates
        if (Vector3.Dot(targetTransform.up, Vector3.up) < 0)
        {
            rotationQuaternion = Quaternion.Euler(new Vector3(newRotation.x, newRotation.y, 180));
        }

        return rotationQuaternion;
    }

    private async Task MoveToPosition(Vector3 moving_target, float duration)
    {
        movingTCS = new TaskCompletionSource<bool>();
        moving_start_time = Time.time;
        moving_duration = duration;
        moving_source = this.transform.position;
        this.moving_target = moving_target;
        is_moving = true;

        await movingTCS.Task;
        is_moving = false;

    }

    public async Task MoveToPosition(GameObject dice, float duration)
    {
        Vector3 moving_t = dice.transform.position;
        await MoveToPosition(moving_t, duration);
    }

    public async Task LookAtDiceValue(GameObject dice, float turning_duration=0.2f, float moving_duration = 1.0f,
        float checking_duration = 2.0f, float recover_duration = 1.0f)
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
        turningTCS = new TaskCompletionSource<bool>();
        turning_start_time = Time.time;
        turning_duration = duration;
        turning_source = c_camera.transform.rotation;
        turning_target = target_rotation;
        is_turning = true;

        await turningTCS.Task;
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
        if (is_turning)
        {
            float time_since_turning = Time.time - turning_start_time;
            if(time_since_turning > turning_duration)
            {
                turningTCS.TrySetResult(true);
            }
            else
            {
                float normal_time = Mathf.Clamp01(time_since_turning / turning_duration);
                //normal_time = Mathf.Sqrt(normal_time);
                normal_time = easingCurve.Evaluate(normal_time);
                c_camera.transform.rotation = Quaternion.Slerp(turning_source, turning_target, normal_time);
                //c_camera.transform.rotation = RotateObject(c_camera.transform);
            }

            
        }
        else if (is_focus_on)
        {
            c_camera.transform.LookAt(focus_on_target.transform);
        }

        if (is_moving)
        {
            float time_since_moving = Time.time - moving_start_time;
            if(time_since_moving > moving_duration)
            {
                movingTCS.TrySetResult(true);
            }
            else
            {
                float normal_time = Mathf.Clamp01(time_since_moving / moving_duration);
                //normal_time = Mathf.Sqrt(normal_time);
                normal_time = easingCurve.Evaluate(normal_time);
                c_camera.transform.position = Vector3.Lerp(moving_source, moving_target, normal_time);
            }
        }
    }
}
