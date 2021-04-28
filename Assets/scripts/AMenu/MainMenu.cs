using UnityEngine;
using UnityEngine.UI;

public enum MenuState
{
    Main,
    Settings
}
public class MainMenu : MonoBehaviour
{
    public GliderController controller;
    public Text scoreText;
    public Text highScore;
    public void NewGame()
    {
        // get the seed from config
    }
    public void Quit()
    {
        Application.Quit();
    }

    [Header("display")]
    public GameObject main;
    public GameObject settings;
    public MenuState state;
    public GameObject fadeIn;
    void Start()
    {
        settings.SetActive(false);
        main.SetActive(true);
        state = MenuState.Main;
    }
    void Update()
    {
        scoreText.text = controller.lastScore + "";
        highScore.text = controller.highScore + "";
    }
    public void Main()
    {
        settings.SetActive(false);
        main.SetActive(true);
    }
    public void Settings()
    {
        settings.SetActive(true);
        main.SetActive(false);
    }
}
