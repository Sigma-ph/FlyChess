using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using DG.Tweening;

public class DiceController : MonoBehaviour
{
    private readonly uint[] point_ref = {4, 7, 2, 10, 5, 8};
    Vector3 last_position;
    float last_time;
    bool is_rolling = false;

    private TaskCompletionSource<int> dice_handle;

    // Update is called once per frame
    void Update()
    {
        if (is_rolling)
        {
            if((last_position - transform.position).magnitude < 0.00001)
            {
                last_time += Time.deltaTime;
            }
            else
            {
                last_time = 0;
            }
            if(last_time > 1)
            {
                is_rolling = false;
                dice_handle.SetResult(CalculateDiceValue());
            }
            last_position = transform.position;
        }
    }

    public void StartToRoll()
     {
        last_time = 0;
        is_rolling = true;
        transform.rotation = UnityEngine.Random.rotation;
        this.GetComponent<Rigidbody>().AddTorque(UnityEngine.Random.insideUnitSphere * 500f);
        this.GetComponent<Rigidbody>().AddForce(new Vector3(0, 2000, -200));
        last_position = transform.position;
    }
    public Task<int> GetDiceValue()
    {
        dice_handle = new TaskCompletionSource<int>();

        return dice_handle.Task;
    }

    public async Task SetDiceAppear(Vector3 pos)
    {
        this.transform.position = pos;
        this.transform.DOScale(new Vector3(0, 0, 0), 0.2f).From();
        this.GetComponent<Rigidbody>().AddForce(new Vector3(0, 200, 0));
        await Task.Delay(300);
    }

    int CalculateDiceValue()
    {
        Vector3 up_vec = new Vector3 ( 0, 1, 0 );
        Vector3 test_vec = transform.up;
        int y_value = (int)Mathf.Round(Vector3.Dot(up_vec, test_vec));
        test_vec = transform.right;
        int x_value = (int)Mathf.Round(Vector3.Dot(up_vec, test_vec));
        test_vec = transform.forward;
        int z_value = (int)Mathf.Round(Vector3.Dot(up_vec, test_vec));
        
        uint dice_value = (uint)(y_value * 4 + x_value * 2 + z_value + 6);
        for(int i=0; i<6; ++i)
        {
            if(point_ref[i] == dice_value)
            {
                return i + 1;
            }
        }
        return -1;
    }
}
