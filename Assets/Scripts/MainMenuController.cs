using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    public GameObject mainMenuCanvas;
    [Tooltip("Parent of the page panels and their background overlay")]
    public GameObject pagesContainer;
    public GameObject setupPanel;
    public GameObject creditsPanel;

    public GameObject[] pages;

    public void Start()
    {
        LoadMainMenu();
    }

    public void LoadMainMenu()
    {
        pagesContainer.SetActive(false);
        mainMenuCanvas.SetActive(true);
    }

    public void LoadSetup()
    {
        ShowPage(setupPanel);
        ActivateTab(0);
    }

    public void LoadCredits()
    {
        ShowPage(creditsPanel);
    }

    private void ShowPage(GameObject page)
    {
        mainMenuCanvas.SetActive(false);
        setupPanel.SetActive(page == setupPanel);
        creditsPanel.SetActive(page == creditsPanel);
        pagesContainer.SetActive(true);
    }

    public void ActivateTab(int tabNum)
    {
        foreach (var tab in pages)
        {
            tab.SetActive(false);
        }

        pages[tabNum].SetActive(true);
    }

    public void StartGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
