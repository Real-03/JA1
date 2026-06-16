using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class MenuController : MonoBehaviour
{
    [Header("Levels To Load")]
    public string _newGameLevel;

    [Header("Volume Settings")]
    [SerializeField] private TMP_Text _SFXTextValue = null;
    [SerializeField] private Slider _SFXSlider = null;
    [SerializeField] private TMP_Text _musicTextValue = null;
    [SerializeField] private Slider _musicSlider = null;
    [SerializeField] private TMP_Text _NarratorTextValue = null;
    [SerializeField] private Slider _NarratorSlider = null;
    [SerializeField] private SoundManager SoundManager;

    [Header("General Settings")]
    [SerializeField] private TMP_Text ControllerSenTextValue = null;
    [SerializeField] private Slider ControllerSenSlider = null;
    [SerializeField] private Toggle InvertYToggle = null;
    public int mainControllerSen = 4;

    [Header("Graphics Settings")]
    [SerializeField] private Slider brightnessSlider = null;
    [SerializeField] private TMP_Text brightnessTextValue = null;

    private int _qualityLevel;
    private bool _isFullScreen;
    private float _brightnessLevel;

    [Header("Resolution Settings")]
    public TMP_Dropdown resolutionDropdown;
    private Resolution[] resolutions;

    [Header("Confirmation Prompt")]
    [SerializeField] private GameObject confirmationPrompt;

    [Header("Nav: Main Menu")]
    public List<Button> mainMenuButtons = new List<Button>();

    [Header("Nav: Settings Tabs")]
    public List<Button> settingsTabButtons = new List<Button>();

    [Header("Nav: Audio Tab")]
    public Slider audioSFXSlider;
    public Slider audioMusicSlider;
    public Slider audioNarratorSlider;

    [Header("Nav: General Tab")]
    public Slider generalSensitivitySlider;
    public Toggle generalInvertYToggle;

    [Header("Nav: Graphics Tab")]
    public Slider graphicsBrightnessSlider;
    public Toggle graphicsFullscreenToggle;
    public TMP_Dropdown graphicsResolutionDropdown;
    public TMP_Dropdown graphicsQualityDropdown;

    [Header("Nav: Apply & Close")]
    public Button applyButton;
    public Button closeButton;

    private enum Section { MainMenu, SettingsTabs, SettingsContent, ApplyClose }
    private Section _section = Section.MainMenu;
    private int _activeTab = 0;
    private int _colIndex = 0;
    private bool _dropdownOpen = false;
    private TMP_Dropdown _openDropdown = null;
    private const float SliderStep = 0.1f;
    private bool _blockInputThisFrame = false;

    void Start()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        StandaloneInputModule inputModule = EventSystem.current.GetComponent<StandaloneInputModule>();
        if (inputModule != null) inputModule.enabled = false;

        resolutions = Screen.resolutions;
        resolutionDropdown.ClearOptions();
        List<string> options = new List<string>();
        int currentResolutionIndex = 0;
        for (int i = 0; i < resolutions.Length; i++)
        {
            string option = resolutions[i].width + " x " + resolutions[i].height;
            options.Add(option);
            if (resolutions[i].width == Screen.width && resolutions[i].height == Screen.height)
                currentResolutionIndex = i;
        }
        resolutionDropdown.AddOptions(options);
        resolutionDropdown.value = currentResolutionIndex;
        resolutionDropdown.RefreshShownValue();

        EnterMainMenu();
    }

    void Update()
    {
        if (_blockInputThisFrame) { _blockInputThisFrame = false; return; }
        if (_dropdownOpen) { HandleDropdownNavigation(); return; }

        switch (_section)
        {
            case Section.MainMenu:        HandleMainMenu();        break;
            case Section.SettingsTabs:    HandleSettingsTabs();    break;
            case Section.SettingsContent: HandleSettingsContent(); break;
            case Section.ApplyClose:      HandleApplyClose();      break;
        }
    }

    void EnterMainMenu()
    {
        _section = Section.MainMenu;
        _colIndex = 0;
        FocusButton(mainMenuButtons, _colIndex);
    }

    void HandleMainMenu()
    {
        if (KeyDown()) { _colIndex = Wrap(_colIndex + 1, mainMenuButtons.Count); FocusButton(mainMenuButtons, _colIndex); }
        if (KeyUp())   { _colIndex = Wrap(_colIndex - 1, mainMenuButtons.Count); FocusButton(mainMenuButtons, _colIndex); }
        if (KeyConfirm())
        {
            _blockInputThisFrame = true;
            string btnName = mainMenuButtons[_colIndex].gameObject.name.ToLower();
            if (btnName.Contains("settings"))       OnShowSettings();
            else if (btnName.Contains("play") || btnName.Contains("newgame")) NewGame();
            else if (btnName.Contains("quit"))      QuitGame();
        }
    }

    public void OnShowSettings()
    {
        _section = Section.SettingsTabs;
        _activeTab = 0;
        _colIndex = 0;
        _blockInputThisFrame = true;
        FocusButton(settingsTabButtons, _activeTab);
        NarrateTab(_activeTab);
    }

    public void OnCloseSettings()
    {
        _blockInputThisFrame = true;
        EnterMainMenu();
    }

    void HandleSettingsTabs()
    {
        if (KeyRight()) { _activeTab = Wrap(_activeTab + 1, settingsTabButtons.Count); FocusButton(settingsTabButtons, _activeTab); NarrateTab(_activeTab); }
        if (KeyLeft())  { _activeTab = Wrap(_activeTab - 1, settingsTabButtons.Count); FocusButton(settingsTabButtons, _activeTab); NarrateTab(_activeTab); }
        if (KeyDown() || KeyConfirm()) { _blockInputThisFrame = true; EnterTabContent(); }
    }

    void NarrateTab(int tab)
    {
        switch (tab)
        {
            case 0: Narrate_AudioSettings();  break;
            case 1: Narrate_GeneralOptions(); break;
            case 2: Narrate_Graphics();       break;
            case 3: Narrate_Controls();       break;
        }
    }

    void EnterTabContent()
    {
        _section = Section.SettingsContent;
        _colIndex = 0;
        FocusTabItem(_activeTab, _colIndex);
    }

    void HandleSettingsContent()
    {
        int rowLen = TabRowLength(_activeTab);

        if (KeyUp())   { _blockInputThisFrame = true; _section = Section.SettingsTabs; FocusButton(settingsTabButtons, _activeTab); NarrateTab(_activeTab); return; }
        if (KeyDown()) { _blockInputThisFrame = true; EnterApplyClose(); return; }

        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        Selectable focused = GetTabItem(_activeTab, _colIndex);
        bool isSlider = focused is Slider;

        if (isSlider && !shift)
        {
            Slider s = focused as Slider;
            if (KeyRight()) s.value = Mathf.Clamp(s.value + SliderStep * (s.maxValue - s.minValue), s.minValue, s.maxValue);
            if (KeyLeft())  s.value = Mathf.Clamp(s.value - SliderStep * (s.maxValue - s.minValue), s.minValue, s.maxValue);
        }
        else
        {
            if (KeyRight()) { _colIndex = Wrap(_colIndex + 1, rowLen); FocusTabItem(_activeTab, _colIndex); }
            if (KeyLeft())  { _colIndex = Wrap(_colIndex - 1, rowLen); FocusTabItem(_activeTab, _colIndex); }
        }

        if (KeyConfirm())
        {
            Selectable sel = GetTabItem(_activeTab, _colIndex);
            if (sel is Toggle t) { _blockInputThisFrame = true; t.isOn = !t.isOn; }
            else if (sel is TMP_Dropdown dd) { _blockInputThisFrame = true; OpenDropdown(dd); }
        }
    }

    int TabRowLength(int tab)
    {
        switch (tab)
        {
            case 0: return 3;
            case 1: return 2;
            case 2: return 4;
            case 3: return 0;
            default: return 0;
        }
    }

    Selectable GetTabItem(int tab, int col)
    {
        switch (tab)
        {
            case 0:
                if (col == 0) return audioSFXSlider;
                if (col == 1) return audioMusicSlider;
                if (col == 2) return audioNarratorSlider;
                break;
            case 1:
                if (col == 0) return generalSensitivitySlider;
                if (col == 1) return generalInvertYToggle;
                break;
            case 2:
                if (col == 0) return graphicsBrightnessSlider;
                if (col == 1) return graphicsFullscreenToggle;
                if (col == 2) return graphicsResolutionDropdown;
                if (col == 3) return graphicsQualityDropdown;
                break;
        }
        return null;
    }

    void FocusTabItem(int tab, int col)
    {
        if (tab == 3) { EnterApplyClose(); return; }
        Selectable sel = GetTabItem(tab, col);
        if (sel == null) return;
        Focus(sel);
        NarrateSelectable(sel);
    }

    void EnterApplyClose()
    {
        _section = Section.ApplyClose;
        _colIndex = 0;
        Focus(applyButton);
        Narrate_ApplyButton();
    }

    void HandleApplyClose()
    {
        if (KeyRight()) { _colIndex = Wrap(_colIndex + 1, 2); FocusApplyClose(); }
        if (KeyLeft())  { _colIndex = Wrap(_colIndex - 1, 2); FocusApplyClose(); }
        if (KeyUp())
        {
            _blockInputThisFrame = true;
            if (_activeTab == 3) { _section = Section.SettingsTabs; FocusButton(settingsTabButtons, _activeTab); }
            else EnterTabContent();
        }
        if (KeyConfirm())
        {
            _blockInputThisFrame = true;
            if (_colIndex == 0) applyButton.onClick.Invoke();
            else                closeButton.onClick.Invoke();
            OnCloseSettings();
        }
    }

    void FocusApplyClose()
    {
        if (_colIndex == 0) { Focus(applyButton); Narrate_ApplyButton(); }
        else                  Focus(closeButton);
    }

    void OpenDropdown(TMP_Dropdown dd)
    {
        _dropdownOpen = true;
        _openDropdown = dd;
        dd.Show();
    }

    void HandleDropdownNavigation()
    {
        if (_openDropdown == null) { _dropdownOpen = false; return; }

        if (KeyDown()) { _openDropdown.value = Wrap(_openDropdown.value + 1, _openDropdown.options.Count); _openDropdown.RefreshShownValue(); }
        if (KeyUp())   { _openDropdown.value = Wrap(_openDropdown.value - 1, _openDropdown.options.Count); _openDropdown.RefreshShownValue(); }
        if (KeyConfirm() || Input.GetKeyDown(KeyCode.Escape)) { _blockInputThisFrame = true; _openDropdown.Hide(); _dropdownOpen = false; _openDropdown = null; }
    }

    void Focus(Selectable sel)
    {
        if (sel == null) return;
        EventSystem.current.SetSelectedGameObject(sel.gameObject);
        sel.Select();
    }

    void FocusButton(List<Button> list, int index)
    {
        if (list == null || index < 0 || index >= list.Count) return;
        Focus(list[index]);
        NarrateButton(list[index]);
    }

    void NarrateButton(Button btn)
    {
        if (btn == null) return;
        string name = btn.gameObject.name.ToLower();
        if      (name.Contains("play") || name.Contains("newgame")) Narrate_Play();
        else if (name.Contains("quit"))                              Narrate_Quit();
        else if (name.Contains("settings"))                          Narrate_Settings();
    }

    int Wrap(int value, int count) { if (count <= 0) return 0; return (value % count + count) % count; }

    bool KeyUp()      => Input.GetKeyDown(KeyCode.UpArrow);
    bool KeyDown()    => Input.GetKeyDown(KeyCode.DownArrow);
    bool KeyLeft()    => Input.GetKeyDown(KeyCode.LeftArrow);
    bool KeyRight()   => Input.GetKeyDown(KeyCode.RightArrow);
    bool KeyConfirm() => Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space);

    void NarrateSelectable(Selectable sel)
    {
        if (sel == null) return;
        string name = sel.gameObject.name.ToLower();
        if      (name.Contains("sfx"))                                 Narrate_SFXVolume();
        else if (name.Contains("music"))                               Narrate_MusicVolume();
        else if (name.Contains("narrator"))                            Narrate_NarratorVolume();
        else if (name.Contains("sen") || name.Contains("sensitivity")) Narrate_ControllerSen();
        else if (name.Contains("invert"))                              Narrate_InvertY();
        else if (name.Contains("brightness"))                          Narrate_Brightness();
        else if (name.Contains("fullscreen"))                          Narrate_Fullscreen();
        else if (name.Contains("resolution"))                          Narrate_Resolution();
        else if (name.Contains("quality"))                             Narrate_Quality();
        else if (name.Contains("apply"))                               Narrate_ApplyButton();
    }

    public void Narrate_Play()            => SoundManager.Instance.PlayNarrator(SoundManager.Instance.narr_Play);
    public void Narrate_Quit()            => SoundManager.Instance.PlayNarrator(SoundManager.Instance.narr_Quit);
    public void Narrate_Settings()        => SoundManager.Instance.PlayNarrator(SoundManager.Instance.narr_Settings);
    public void Narrate_AudioSettings()   => SoundManager.Instance.PlayNarrator(SoundManager.Instance.narr_AudioSettings);
    public void Narrate_GeneralOptions()  => SoundManager.Instance.PlayNarrator(SoundManager.Instance.narr_GeneralOptions);
    public void Narrate_Graphics()        => SoundManager.Instance.PlayNarrator(SoundManager.Instance.narr_Graphics);
    public void Narrate_Controls()        => SoundManager.Instance.PlayNarrator(SoundManager.Instance.narr_Controls);
    public void Narrate_ApplyButton()     => SoundManager.Instance.PlayNarrator(SoundManager.Instance.narr_ApplyButton);
    public void Narrate_SFXVolume()       => SoundManager.Instance.PlayNarrator(SoundManager.Instance.narr_SFXVolume);
    public void Narrate_MusicVolume()     => SoundManager.Instance.PlayNarrator(SoundManager.Instance.narr_MusicVolume);
    public void Narrate_NarratorVolume()  => SoundManager.Instance.PlayNarrator(SoundManager.Instance.narr_NarratorVolume);
    public void Narrate_ControllerSen()   => SoundManager.Instance.PlayNarrator(SoundManager.Instance.narr_ControllerSensitivity);
    public void Narrate_InvertY()         => SoundManager.Instance.PlayNarrator(SoundManager.Instance.narr_InvertY);
    public void Narrate_Brightness()      => SoundManager.Instance.PlayNarrator(SoundManager.Instance.narr_Brightness);
    public void Narrate_Fullscreen()      => SoundManager.Instance.PlayNarrator(SoundManager.Instance.narr_Fullscreen);
    public void Narrate_Quality()         => SoundManager.Instance.PlayNarrator(SoundManager.Instance.narr_Quality);
    public void Narrate_Resolution()      => SoundManager.Instance.PlayNarrator(SoundManager.Instance.narr_Resolution);

    public void SetResolution(int resolutionIndex)
    {
        Resolution resolution = resolutions[resolutionIndex];
        Screen.SetResolution(resolution.width, resolution.height, Screen.fullScreen);
    }

    public void NewGame()
    {
        SceneManager.LoadScene(_newGameLevel);
        Debug.Log("New Game Started");
    }

    public void QuitGame()
    {
        Application.Quit();
        Debug.Log("Quit Game");
    }

    public void SetSFXVolume(float volume)
    {
        AudioListener.volume = volume;
        _SFXTextValue.text = volume.ToString("0.0");
    }

    public void SetMusicVolume(float volume)
    {
        AudioListener.volume = volume;
        _musicTextValue.text = volume.ToString("0.0");
    }

    public void SetNarratorVolume(float volume)
    {
        AudioListener.volume = volume;
        _NarratorTextValue.text = volume.ToString("0.0");
    }

    public void ApplyAudio()
    {
        PlayerPrefs.SetFloat("SFXVolume", AudioListener.volume);
        PlayerPrefs.SetFloat("MusicVolume", AudioListener.volume);
        PlayerPrefs.SetFloat("NarratorVolume", AudioListener.volume);
        StartCoroutine(ConfirmationBox());
    }

    public void SetControllerSensitivity(float sensitivity)
    {
        mainControllerSen = Mathf.RoundToInt(sensitivity);
        ControllerSenTextValue.text = mainControllerSen.ToString("0");
    }

    public void GameplayApply()
    {
        PlayerPrefs.SetInt("masterInvertY", InvertYToggle.isOn ? 1 : 0);
        PlayerPrefs.SetFloat("masterSen", mainControllerSen);
        StartCoroutine(ConfirmationBox());
    }

    public void SetBrightness(float brightness)
    {
        _brightnessLevel = brightness;
        brightnessTextValue.text = brightness.ToString("0.0");
    }

    public void SetFullScreen(bool isFullScreen) { _isFullScreen = isFullScreen; }

    public void SetQuality(int qualityIndex) { _qualityLevel = qualityIndex; }

    public void GraphicsApply()
    {
        PlayerPrefs.SetFloat("masterBrightness", _brightnessLevel);
        QualitySettings.SetQualityLevel(_qualityLevel);
        PlayerPrefs.SetInt("masterFullscreen", (_isFullScreen ? 1 : 0));
        Screen.fullScreen = _isFullScreen;
        StartCoroutine(ConfirmationBox());
    }

    public IEnumerator ConfirmationBox()
    {
        confirmationPrompt.SetActive(true);
        yield return new WaitForSeconds(2);
        confirmationPrompt.SetActive(false);
    }
}