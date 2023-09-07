using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using DG.Tweening;
using Unity.Mathematics;
using Newtonsoft.Json;
using System.Diagnostics;
using UnityEngine.UI;

public class DiceController : MonoBehaviour
{
    private readonly uint[] point_ref = {4, 7, 2, 10, 5, 8};
    Vector3 last_position;
    float last_time;
    bool is_rolling = false;
    private List<List<float>> loc_track;
    private List<List<float>> rot_track;
    private List<List<List<List<float>>>> diceTracks = new List<List<List<List<float>>>>();

    private TaskCompletionSource<int> dice_handle;

    // fake roll dice
    bool isFakeRolling = false;
    int fakeRollIndex = 0;
    int fakeDiceValue = 0;

    private void Start()
    {
        for(int i=1; i<=6; ++i)
        {
            TextAsset text = Resources.Load<TextAsset>("DiceTrack/" + i.ToString());
            List<List<List<float>>> data = JsonConvert.DeserializeObject<List<List<List<float>>>>(text.text); 
            diceTracks.Add(data);
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (is_rolling)
        {
            if((last_position - transform.position).magnitude < 0.00001)
            {
                last_time += Time.deltaTime;
                //loc_track.Add(new List<float> { transform.position.x, transform.position.y, transform.position.z });
                //rot_track.Add(new List<float> { transform.rotation[0], transform.rotation[1], transform.rotation[2], transform.rotation[3]});
            }
            else
            {
                //loc_track.Add(new List<float> { transform.position.x, transform.position.y, transform.position.z });
                //rot_track.Add(new List<float> { transform.rotation[0], transform.rotation[1], transform.rotation[2], transform.rotation[3] });
                last_time = 0;
            }
            if(last_time > 1)
            {
                is_rolling = false;
                //List<List<List<float>>> data = new List<List<List<float>>>();
                //data.Add(loc_track);
                //data.Add(rot_track);
                //string s_data = JsonConvert.SerializeObject(data);
                int dice_value = CalculateDiceValue();
                dice_handle.SetResult(dice_value);
                
            }
            last_position = transform.position;
        }
        else if (isFakeRolling)
        {
            Vector3 dice_pos = new Vector3(diceTracks[fakeDiceValue-1][0][fakeRollIndex][0],
                                            diceTracks[fakeDiceValue-1][0][fakeRollIndex][1],
                                            diceTracks[fakeDiceValue-1][0][fakeRollIndex][2]);
            Quaternion dice_rot = new Quaternion(diceTracks[fakeDiceValue - 1][1][fakeRollIndex][0],
                                                diceTracks[fakeDiceValue - 1][1][fakeRollIndex][1],
                                                diceTracks[fakeDiceValue - 1][1][fakeRollIndex][2],
                                                diceTracks[fakeDiceValue - 1][1][fakeRollIndex][3]);
            transform.position = dice_pos;
            transform.rotation = dice_rot;
            fakeRollIndex++;
            if(fakeRollIndex >= diceTracks[fakeDiceValue - 1][1].Count)
            {
                dice_handle.SetResult(fakeDiceValue);
                isFakeRolling = false;
            }

        }
    }

    public List<Vector3> StartToRoll()
     {
        // Debug
        this.loc_track = new List<List<float>>();
        this.rot_track = new List<List<float>>();
        last_time = 0;
        is_rolling = true;
        transform.rotation = UnityEngine.Random.rotation;
        Vector3 random_r = UnityEngine.Random.insideUnitSphere * 500f;
        Vector3 random_m = new Vector3(0, 2000, -200);
        this.GetComponent<Rigidbody>().AddTorque(random_r);
        this.GetComponent<Rigidbody>().AddForce(random_m);
        last_position = transform.position;
        return new List<Vector3> { random_r, random_m };
    }

    public void FakeRollDice(int dice_value)
    {
        isFakeRolling = true;
        fakeDiceValue = dice_value;
        fakeRollIndex = 0;
    }

    public void StartToRoll(List<Vector3> power)
    {
        last_time = 0;
        is_rolling = true;
        this.GetComponent<Rigidbody>().AddTorque(power[0]);
        this.GetComponent<Rigidbody>().AddForce(power[1]);
        last_position = transform.position;
    }

    public Task<int> GetDiceValue()
    {
        dice_handle = new TaskCompletionSource<int>();
        return dice_handle.Task;
    }

    public async Task SetDiceAppear(Vector3 pos)
    {
        this.transform.rotation = new Quaternion(0, 0, 0, 0);
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
