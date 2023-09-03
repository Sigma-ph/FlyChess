using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using System.Threading.Tasks;
using UnityEditor.Animations;

public class DiceGrilController : MonoBehaviour
{
    Vector3 initPos;
    Quaternion initRot;
    Animator girlAnim;
    public async Task MoveToPosition(Vector3 pos, float duration)
    {
        girlAnim.SetTrigger("doRun");
        Task t1 = this.transform.DOLookAt(pos, 0.2f).AsyncWaitForCompletion();
        Task t2 = this.transform.DOMove(pos, duration).SetEase(Ease.InOutSine).AsyncWaitForCompletion();
        await Task.WhenAll(t1, t2);

        girlAnim.SetTrigger("doStandby");
    }

    public async Task MoveToInitTrans(float duration)
    {
        await MoveToPosition(initPos, duration);
        await transform.DORotateQuaternion(initRot, 0.2f).AsyncWaitForCompletion();
    }

    public async Task MoveToPositionXZ(Vector3 pos, float duration)
    {
        pos.y = initPos.y;
        await MoveToPosition(pos, duration);
    }

    public void DoThrowAction()
    {
        girlAnim.SetTrigger("doThrow");
    }
    private void Start()
    {
        this.initPos = this.transform.position;
        this.initRot = this.transform.rotation;
        this.girlAnim = GetComponent<Animator>();
    }
}
