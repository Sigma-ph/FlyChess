using System.Collections;
using System.Collections.Generic;
using System.Xml.Serialization;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using RoomConfig;
using System.Threading.Tasks;
using Unity.Services.CloudCode;
using Unity.Services.Authentication;

public class MainMenu : MonoBehaviour
{
    
    private int playerCount=0, aiCount=0;

    public List<UnityEngine.UI.Button> playerCountButtons;
    public List<UnityEngine.UI.Button> aiCountButtons;
    public UnityEngine.UI.Button SinglePlayerButton;
    public UnityEngine.UI.Button MultiPlayerButton;
    public UnityEngine.UI.Button joinRoomButton;

    public GameObject aiChecker, playerChecker;
    public GameObject playerChoosePanel;
    public GameObject waitPlayerPanel;

    bool isWaitingPlayer = false;
    // Start is called before the first frame update
    async void Start()
    {
        Application.targetFrameRate = 30;
        await RoomHost.InitNetEnv();
        RoomHost.roomInfo.localNetId = AuthenticationService.Instance.PlayerId;
        foreach (var bt in aiCountButtons)
        {
            bt.gameObject.SetActive(false);
        }
        aiChecker.SetActive(false);
        playerChecker.SetActive(false);
        playerChoosePanel.SetActive(false);
        waitPlayerPanel.gameObject.SetActive(false);

        RoomHost.OnGetHostId += OnHostIdGot;
        RoomHost.OnGetJoinedID += OnJoinedIdGot;
    }

    public void OnHostIdGot(string host_id)
    {
        Debug.Log("Host Id: " + host_id);
        if (!isWaitingPlayer)
        {
            return;
        }
        isWaitingPlayer = false;
        List<string> playerNetIds = new List<string> {host_id, RoomHost.roomInfo.localNetId};
        RoomHost.roomInfo.CreateOnlineRoom(2, 1, playerNetIds, host_id, 0);
        SceneManager.LoadScene(1);
    }

    public void OnJoinedIdGot(string joined_id)
    {
        Debug.Log("Joined Id:" + joined_id);
        if (!isWaitingPlayer)
        {
            return;
        }
        isWaitingPlayer = false;
        List<string> playerNetIds = new List<string> { RoomHost.roomInfo.localNetId, joined_id };
        RoomHost.roomInfo.CreateOnlineRoom(2, 0, playerNetIds, RoomHost.roomInfo.localNetId, 0);
        SceneManager.LoadScene(1);

    }

    public void OnSetPlayerCountButtonClick(int count)
    {
        playerCount = count;
        playerChecker.SetActive(true);
        playerChecker.transform.position = playerCountButtons[count-1].gameObject.transform.position;
        for (int i=0; i < 4; ++i)
        {
            if (i < 4 - count)
            {
                aiCountButtons[i].gameObject.SetActive(true);
            }
            else
            {
                aiCountButtons[i].gameObject.SetActive(false);
            }
        }

        if(aiCount > 4 - count)
        {
            aiChecker.SetActive(false);
            aiCount = 0;
        }
    }

    public void OnSetAICountButtonCLick(int count)
    {
        aiCount = count;
        aiChecker.SetActive(true);
        aiChecker.transform.position = aiCountButtons[count-1].gameObject.transform.position;
    }

    public void OnSinglePlayerClick()
    {
        RoomHost.roomInfo.roomType = RoomType.OfflineRoom;
        SinglePlayerButton.interactable = false;
        MultiPlayerButton.interactable = false;
        joinRoomButton.interactable = false;
        playerChoosePanel.gameObject.SetActive(true);
    }

    public void OnCancelButtoClick()
    {
        SinglePlayerButton.interactable = true;
        MultiPlayerButton.interactable = true;
        joinRoomButton.interactable = true;
        playerChoosePanel.gameObject.SetActive(false);
    }

    private async Task CreateChessRoom()
    {
        var args_msg = new Dictionary<string, object> { { "host_id", RoomHost.roomInfo.localNetId } };
        bool ret = await CloudCodeService.Instance.CallModuleEndpointAsync<bool>("FlyChessService", "CreateChessRoom", args_msg);
        isWaitingPlayer = true;
        Debug.Log(ret);
    }

    private async Task DestoryChessRoom()
    {
        isWaitingPlayer = false;
        await CloudCodeService.Instance.CallModuleEndpointAsync<bool>("FlyChessService", "DestoryChessRoom");
    }

    private async Task JoinChessRoom()
    {
        bool has_room = await CloudCodeService.Instance.CallModuleEndpointAsync<bool>("FlyChessService", "JoinRoom", new Dictionary<string, object> { { "join_id", RoomHost.roomInfo.localNetId } });
        if (!has_room)
        {
            waitPlayerPanel.GetComponentInChildren<Button>().interactable = true;
            isWaitingPlayer = false;
            waitPlayerPanel.gameObject.SetActive(false);
        }
    }
    public void OnCreateRoomClick()
    {
        waitPlayerPanel.gameObject.SetActive(true);
        CreateChessRoom();
    }

    public void OnDestroyRoomClick()
    {
        DestoryChessRoom();
        waitPlayerPanel.gameObject.SetActive(false);
    }

    public void OnJoinRoomClick()
    {
        waitPlayerPanel.gameObject.SetActive(true);
        waitPlayerPanel.GetComponentInChildren<Button>().interactable = false;
        isWaitingPlayer = true;
        JoinChessRoom();
    }



    public void OnStartButtonClick()
    {
        RoomHost.roomInfo.CreateOfflineRoom(playerCount, aiCount);
        //PlayerPrefs.SetInt("playerCount", playerCount);
        //PlayerPrefs.SetInt("aiCount", aiCount);
        SceneManager.LoadScene(1);
        
        
    }
}
