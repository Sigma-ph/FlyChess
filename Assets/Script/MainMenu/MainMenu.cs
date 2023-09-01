using System.Collections;
using System.Collections.Generic;
using System.Xml.Serialization;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    
    private int playerCount=0, aiCount=0;

    public List<Button> playerCountButtons;
    public List<Button> aiCountButtons;
    public Button gameStartButton;

    public GameObject aiChecker, playerChecker;
    public GameObject playerChoosePanel;
    // Start is called before the first frame update
    void Start()
    {
        foreach(var bt in aiCountButtons)
        {
            bt.gameObject.SetActive(false);
        }
        aiChecker.SetActive(false);
        playerChecker.SetActive(false);
        playerChoosePanel.SetActive(false);
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

    public void OnGameStartClick()
    {
        gameStartButton.interactable = false;
        playerChoosePanel.gameObject.SetActive(true);
    }

    public void OnCancelButtoClick()
    {
        gameStartButton.interactable = true;
        playerChoosePanel.gameObject.SetActive(false);
    }

    public void OnStartButtonClick()
    {
        PlayerPrefs.SetInt("playerCount", playerCount);
        PlayerPrefs.SetInt("aiCount", aiCount);
        //SceneManager.LoadScene(1);
    }
}
