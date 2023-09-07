using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.CloudCode.Subscriptions;
using Unity.Services.CloudCode;
using Unity.Services.Core;
using UnityEngine;
using System;
using Newtonsoft.Json;
using UnityEngine.UI;
using Unity.Services.Analytics.Internal;

public class NetTestClient : MonoBehaviour
{
    public InputField Your_id, Another_id, Message_send;
    public Text Message_received;

    // Start is called before the first frame update
    async void Start()
    {
        await UnityServices.InitializeAsync();
        await AuthenticationService.Instance.SignInAnonymouslyAsync();
        Debug.Log(AuthenticationService.Instance.PlayerId);
        Your_id.text = AuthenticationService.Instance.PlayerId;
        await SubscribeToPlayerMessages();
        await SubscribeToProjectMessages();
    }
    // This method creates a subscription to player messages and logs out the messages received,
    // the state changes of the connection, when the player is kicked and when an error occurs.
    Task SubscribeToPlayerMessages()
    {
        // Register callbacks, which are triggered when a player message is received
        var callbacks = new SubscriptionEventCallbacks();
        callbacks.MessageReceived += OnMessageReceived;
        callbacks.ConnectionStateChanged += @event =>
        {
            Debug.Log($"Got player subscription ConnectionStateChanged: {JsonConvert.SerializeObject(@event, Formatting.Indented)}");
        };
        callbacks.Kicked += () =>
        {
            Debug.Log($"Got player subscription Kicked");
        };
        callbacks.Error += @event =>
        {
            Debug.Log($"Got player subscription Error: {JsonConvert.SerializeObject(@event, Formatting.Indented)}");
        };
        return CloudCodeService.Instance.SubscribeToPlayerMessagesAsync(callbacks);
    }
    // This method creates a subscription to project messages and logs out the messages received,
    // the state changes of the connection, when the player is kicked and when an error occurs.
    Task SubscribeToProjectMessages()
    {
        var callbacks = new SubscriptionEventCallbacks();
        callbacks.MessageReceived += OnMessageReceived;
        callbacks.ConnectionStateChanged += @event =>
        {
            Debug.Log($"Got project subscription ConnectionStateChanged: {JsonConvert.SerializeObject(@event, Formatting.Indented)}");
        };
        callbacks.Kicked += () =>
        {
            Debug.Log($"Got project subscription Kicked");
        };
        callbacks.Error += @event =>
        {
            Debug.Log($"Got project subscription Error: {JsonConvert.SerializeObject(@event, Formatting.Indented)}");
        };
        return CloudCodeService.Instance.SubscribeToProjectMessagesAsync(callbacks);
    }

    public void OnSendToOneCLick()
    {
        OnSendToOne();
    }

    public void OnSendToAllClick()
    {
        OnSendToAll();
    }
    public async Task OnSendToOne()
    {
        string a_id = Another_id.text;
        string msg = Message_send.text;
        Dictionary<string, object> m_args = new Dictionary<string, object>();
        m_args["message"] = msg;
        m_args["messageType"] = "testType";
        m_args["playerId"] = a_id;
        var send_result = await CloudCodeService.Instance.CallModuleEndpointAsync<string>("FlyChessService", "SendPlayerMessage", m_args);
        Debug.Log(send_result);
    }

    public async Task OnSendToAll()
    {
        string a_id = Another_id.text;
        string msg = Message_send.text;
        Dictionary<string, object> m_args = new Dictionary<string, object>();
        m_args["message"] = msg;
        m_args["messageType"] = "testType";
        var send_result = await CloudCodeService.Instance.CallModuleEndpointAsync<string>("FlyChessService", "SendProjectMessage", m_args);
        Debug.Log(send_result);
    }

    void OnMessageReceived(IMessageReceivedEvent msg)
    {
        //string text = JsonConvert.SerializeObject(msg, Formatting.Indented);
        Message_received.text = msg.Message;
        Debug.Log("test");
    }
}


class Abc
{
    public string name;
    [JsonProperty]
    public List<Vector3> track;

    public Abc() 
    { 
        track = new List<Vector3>();
    }
}