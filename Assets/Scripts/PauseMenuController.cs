using UnityEngine;
using UnityEngine.InputSystem;

public class PauseMenuController : MonoBehaviour
{
    [Header("Menu Objects")]
    public GameObject _pauseMenuUI;
    private bool _isPaused = false;

    [Header("Levels To Load")]
    public string _menuScene;

    void Update()
    {
        if (InputSystem.actions["PauseMenu"].WasPressedThisDynamicUpdate())
        {
            if (_isPaused)
            {
                ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }
    }
    public void ResumeGame()
    {
        _pauseMenuUI.SetActive(false);
        Time.timeScale = 1f;
        _isPaused = false;
        Debug.Log("Game Resumed");
    }
    public void PauseGame()
    {
        _pauseMenuUI.SetActive(true);
        Time.timeScale = 0f;
        _isPaused = true;
        Debug.Log("Game Paused");
    }

    public void ReturnMenu()
    {
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene(_menuScene);
        Debug.Log("Returned to Menu");
    }
}

