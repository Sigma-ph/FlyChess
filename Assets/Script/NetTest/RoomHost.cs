using System.Collections;
using System.Collections.Generic;
using Unity.Services.Authentication;
using Unity.Services.CloudCode.Subscriptions;
using Unity.Services.CloudCode;
using Unity.Services.Core;
using UnityEngine;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System;
using System.Xml.Serialization;
using Unity.VisualScripting;
using DG.Tweening;

namespace RoomConfig
{
    public enum RoomType {OnlineRoom=0, OfflineRoom=1};

    public class RollDiceInfo
    {
        public int msg_id;
        public int player_id;
        public List<float> compaction_power;
        public List<float> rotation_power;
        public int dice_value;
    }

    public class PawnChooseInfo
    {
        public int msg_id;
        public int player_id;
        public int choosenPawnID;
    }

    public class RoomInfo
    {
        public bool params_set = false;

        public RoomType roomType;
        public List<int> playerIdxes;  //玩家角色id
        public List<int> aiIdxes;  // ai角色id
        public List<int> charactorIdxes;  //所有角色id
        public List<int> charactorPawnCount;  //所有角色的棋子数量
        public List<PlayerColor> charactorPawnColor;  //所有角色的棋子颜色
        public int localIdx;  // 本机玩家id
        public int curActCharactorIdx;  // 当前行动的角色id
        public List<List<int>> charactorSteps;  //每个玩家走的步数

        public int localMsgID = 0;
        public List<int> receivedMsgID;

        // Net Params
        public string localNetId;
        public List<string> playerNetIds;
        public string hostNetId;
        public bool isRoomHost;

        public RoomInfo()
        {
            //playerIdxes = new List<int>();
            //aiIdxes = new List<int>();
            //playerNetIds = new List<string>();
            //charactorIdxes = new List<int>();
        }

        void InitRoomPlayerSetting(int playeNum, int aiNum, int pawn_count)
        {

            int pId = 0;
            aiIdxes = new List<int>();
            playerIdxes = new List<int>();
            charactorIdxes = new List<int>();
            charactorPawnCount = new List<int>();
            charactorSteps = new List<List<int>>();
            charactorPawnColor = new List<PlayerColor>();

            for (int i = 0; i < playeNum; ++i)
            {
                playerIdxes.Add(pId);
                charactorIdxes.Add(pId);
                charactorPawnCount.Add(pawn_count);
                charactorSteps.Add(new List<int> {pId, 0});
                charactorPawnColor.Add((PlayerColor)pId);
                ++pId;
            }
            for(int i=0; i<aiNum; ++i)
            {
                aiIdxes.Add(pId);
                charactorIdxes.Add(pId);
                charactorPawnCount.Add(pawn_count);
                charactorSteps.Add(new List<int> { pId, 0 });
                charactorPawnColor.Add((PlayerColor)pId);
                ++pId;
            }
            curActCharactorIdx = 0;
        }

        public void CreateOnlineRoom(int playerNum, int localPlayerIdx, List<string> playerNetIds, string hostNetId, int aiNum)
        {
            roomType = RoomType.OnlineRoom;
            this.InitRoomPlayerSetting(playerNum, aiNum, 4);
            localMsgID = 0;
            receivedMsgID = new List<int>();
            for(int i=0; i < playerNum; ++i)
            {
                receivedMsgID.Add(0);
            }

            this.localIdx = localPlayerIdx;
            this.playerNetIds = playerNetIds;
            this.hostNetId = hostNetId;
            isRoomHost = (this.localNetId == hostNetId);
            params_set = true;
        }

        public void CreateOfflineRoom(int playerNum, int aiNum)
        {
            roomType = RoomType.OfflineRoom;
            this.InitRoomPlayerSetting(playerNum, aiNum, 4);
            this.localIdx = 0;
            isRoomHost = true;
            params_set = true;
        }

        public int ChangeCharactor()
        {
            curActCharactorIdx++;
            if(curActCharactorIdx >= charactorIdxes.Count)
            {
                curActCharactorIdx = 0;
            }
            return curActCharactorIdx;
        }
    }

    public static class RoomHost
    {
        public static RoomInfo roomInfo = new RoomInfo();
        public static Queue<RollDiceInfo> rollDiceInfoQueue = new Queue<RollDiceInfo>();
        public static Queue<PawnChooseInfo> pawnChooseInfoQueue = new Queue<PawnChooseInfo>();

        private static async Task SendMessageToOtherPlayer(string message, string messageType)
        {
            Dictionary<string, object> msg_args = new Dictionary<string, object>();
            List<Task> tasks = new List<Task>();
            for(int i=0; i<roomInfo.playerIdxes.Count; ++i)
            {
                if (roomInfo.playerIdxes[i] == roomInfo.localIdx) continue;
                msg_args["message"] = message;
                msg_args["messageType"] = messageType;
                msg_args["playerId"] = roomInfo.playerNetIds[i];
                tasks.Add(CloudCodeService.Instance.CallModuleEndpointAsync<string>("FlyChessService", "SendPlayerMessage", msg_args));
            }
            await Task.WhenAll(tasks);
        }

        public static async Task SendRollDiceInfoToOthers(RollDiceInfo dice_info)
        {
            dice_info.msg_id = roomInfo.localMsgID;
            string msg = JsonConvert.SerializeObject(dice_info);
            await SendMessageToOtherPlayer(msg, "RollDiceInfo");
            roomInfo.localMsgID++;
        }

        public static async Task SendPawnChooseInfoToOthers(int pawnId)
        {
            PawnChooseInfo pawnInfo = new PawnChooseInfo();
            pawnInfo.msg_id = roomInfo.localMsgID;
            pawnInfo.player_id = roomInfo.localIdx;
            pawnInfo.choosenPawnID = pawnId;
            string msg = JsonConvert.SerializeObject(pawnInfo);
            await SendMessageToOtherPlayer(msg, "PawnChooseInfo");
            roomInfo.localMsgID++;
        }
        
        public static async Task<RollDiceInfo> GetRollDiceInfoFromOthers()
        {
            while (true)
            {
                if(rollDiceInfoQueue.Count == 0)
                {
                    await Task.Delay(1000);
                }
                else
                {
                    break;
                }
            }
            RollDiceInfo dice_info = rollDiceInfoQueue.Dequeue();
            if(dice_info.player_id != roomInfo.curActCharactorIdx)
            {
                Debug.LogError("Charactor Id of received DiceInfo dose not match current charactor Id!");
            }
            return dice_info;
        }

        public static async Task<PawnChooseInfo> GetPawnChooseInfoFromOthers()
        {
            while (true) 
            {
                if (pawnChooseInfoQueue.Count == 0)
                {
                    await Task.Delay(1000);
                }
                else
                {
                    break;
                }
            }
            PawnChooseInfo pawnInfo = pawnChooseInfoQueue.Dequeue();
            if (pawnInfo.player_id != roomInfo.curActCharactorIdx)
            {
                Debug.LogError("Charactor Id of received PawnInfo dose not match current charactor Id!");
            }
            return pawnInfo;

        }

        public async static Task InitNetEnv()
        {
            await UnityServices.InitializeAsync();
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            Debug.Log(AuthenticationService.Instance.PlayerId);
            string id = AuthenticationService.Instance.PlayerId;
            await SubscribeToPlayerMessages();
            await SubscribeToProjectMessages();
        }

        static Task SubscribeToProjectMessages()
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

        static Task SubscribeToPlayerMessages()
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
        public static event Action<RollDiceInfo> OnReceiveRollDice;
        public static event Action<string> OnGetHostId;
        public static event Action<string> OnGetJoinedID;

        private static void OnMessageReceived(IMessageReceivedEvent msg)
        {
            switch (msg.MessageType)
            {
                case "RollDiceInfo":
                    RollDiceInfo info = JsonConvert.DeserializeObject<RollDiceInfo>(msg.Message);
                    if(info.msg_id == roomInfo.receivedMsgID[info.player_id])
                    {
                        rollDiceInfoQueue.Enqueue(info);
                        roomInfo.receivedMsgID[info.player_id]++;
                    }
                    else if(info.msg_id > roomInfo.receivedMsgID[info.player_id])
                    {
                        Debug.LogError("DiceInfo missing, player id=" + info.player_id.ToString() + ", msg_id=" + roomInfo.receivedMsgID[info.player_id]);
                    }
                    break;
                case "PawnChooseInfo":
                    PawnChooseInfo p_info = JsonConvert.DeserializeObject<PawnChooseInfo>(msg.Message);
                    if(p_info.msg_id == roomInfo.receivedMsgID[p_info.player_id])
                    {
                        pawnChooseInfoQueue.Enqueue(p_info);
                        roomInfo.receivedMsgID[p_info.player_id]++;
                    }
                    else if(p_info.msg_id > roomInfo.receivedMsgID[p_info.player_id])
                    {
                        Debug.LogError("DiceInfo missing, player id=" + p_info.player_id.ToString() + ", msg_id=" + roomInfo.receivedMsgID[p_info.player_id]);
                    }
                    break; 
                case "HostId":
                    string host_id = msg.Message;
                    OnGetHostId(host_id);
                    break;
                case "JoinedId":
                    string joined_id = msg.Message;
                    OnGetJoinedID(joined_id);
                    break;
            }
        }
    }
}



