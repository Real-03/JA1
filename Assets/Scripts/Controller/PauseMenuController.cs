using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class PauseMenuController : MonoBehaviour
{
    [Header("Menu Objects")]
    public GameObject _pauseMenuUI;
    private bool _isPaused = false;

    [Header("Levels To Load")]
    public string _menuScene;

    [Header("Nav: Pause Menu Buttons")]
    public List<Button> pauseMenuButtons = new List<Button>();

    private int _currentIndex = 0;
    private bool _blockInputThisFrame = false;
    private StandaloneInputModule _inputModule;

    void Start()
    {
        _inputModule = EventSystem.current.GetComponent<StandaloneInputModule>();
    }

    void Update()
    {
        if (InputSystem.actions["PauseMenu"].WasPressedThisDynamicUpdate())
        {
            if (_isPaused) ResumeGame();
            else PauseGame();
            return;
        }

        if (!_isPaused) return;
        if (_blockInputThisFrame) { _blockInputThisFrame = false; return; }

        HandleKeyboardNavigation();
    }

    void HandleKeyboardNavigation()
    {
        if (pauseMenuButtons == null || pauseMenuButtons.Count == 0) return;

        if (Input.GetKeyDown(KeyCode.DownArrow)) { _currentIndex = Wrap(_currentIndex + 1, pauseMenuButtons.Count); FocusItem(_currentIndex); }
        if (Input.GetKeyDown(KeyCode.UpArrow))   { _currentIndex = Wrap(_currentIndex - 1, pauseMenuButtons.Count); FocusItem(_currentIndex); }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
        {
            _blockInputThisFrame = true;
            pauseMenuButtons[_currentIndex].onClick.Invoke();
        }
    }

    void FocusItem(int index)
    {
        if (pauseMenuButtons == null || index < 0 || index >= pauseMenuButtons.Count) return;
        Button btn = pauseMenuButtons[index];
        EventSystem.current.SetSelectedGameObject(btn.gameObject);
        btn.Select();
        TriggerNarration(btn);
    }

    void TriggerNarration(Button btn)
    {
        string name = btn.gameObject.name.ToLower();
        if (name.Contains("resume"))                               Narrate_Resume();
        else if (name.Contains("return") || name.Contains("menu")) Narrate_ReturnToMenu();
    }

    int Wrap(int value, int count) { return (value % count + count) % count; }

    public void Narrate_Resume()       => SoundManager.Instance.PlayNarrator(SoundManager.Instance.narr_ResumeGame);
    public void Narrate_ReturnToMenu() => SoundManager.Instance.PlayNarrator(SoundManager.Instance.narr_ReturnToMainMenu);

    public void ResumeGame()
    {
        _pauseMenuUI.SetActive(false);
        Time.timeScale = 1f;
        _isPaused = false;
        if (_inputModule != null) _inputModule.enabled = false;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        Debug.Log("Game Resumed");
    }

    public void PauseGame()
    {
        _pauseMenuUI.SetActive(true);
        Time.timeScale = 0f;
        _isPaused = true;
        _blockInputThisFrame = true;
        if (_inputModule != null) _inputModule.enabled = false;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        _currentIndex = 0;
        FocusItem(_currentIndex);
        Debug.Log("Game Paused");
    }

    public void ReturnMenu()
    {
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene(_menuScene);
        Debug.Log("Returned to Menu");
    }
}