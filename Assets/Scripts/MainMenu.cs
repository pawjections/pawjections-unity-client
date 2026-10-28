using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class MainMenu : MonoBehaviour
{
    public GameObject titleScreen;
    public TMP_Text versionLabel;

    [Tooltip("Dark overlay shown behind every screen except the title screen")]
    public GameObject pageBackground;

    [Tooltip("Every screen, only one is shown at a time. Buttons open one by calling Show with it")]
    public GameObject[] screens;

    public void Start()
    {
        versionLabel.text = $"Pawjections {Application.version}";
        Show(titleScreen);
    }

    public void Show(GameObject screen)
    {
        foreach (var s in screens)
        {
            s.SetActive(s == screen);
        }

        versionLabel.gameObject.SetActive(screen == titleScreen);
        pageBackground.SetActive(screen != titleScreen);
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
