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

    void Start()
    {
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
    }


    public void Narrate_Play() => SoundManager.Instance.PlayNarrator(SoundManager.Instance.narr_Play);
    public void Narrate_Quit() => SoundManager.Instance.PlayNarrator(SoundManager.Instance.narr_Quit);
    public void Narrate_Settings() => SoundManager.Instance.PlayNarrator(SoundManager.Instance.narr_Settings);

    public void Narrate_AudioSettings() => SoundManager.Instance.PlayNarrator(SoundManager.Instance.narr_AudioSettings);
    public void Narrate_GeneralOptions() => SoundManager.Instance.PlayNarrator(SoundManager.Instance.narr_GeneralOptions);
    public void Narrate_Graphics() => SoundManager.Instance.PlayNarrator(SoundManager.Instance.narr_Graphics);
    public void Narrate_Controls() => SoundManager.Instance.PlayNarrator(SoundManager.Instance.narr_Controls);
    public void Narrate_ApplyButton() => SoundManager.Instance.PlayNarrator(SoundManager.Instance.narr_ApplyButton);

    public void Narrate_SFXVolume() => SoundManager.Instance.PlayNarrator(SoundManager.Instance.narr_SFXVolume);
    public void Narrate_MusicVolume() => SoundManager.Instance.PlayNarrator(SoundManager.Instance.narr_MusicVolume);
    public void Narrate_NarratorVolume() => SoundManager.Instance.PlayNarrator(SoundManager.Instance.narr_NarratorVolume);

    public void Narrate_ControllerSen() => SoundManager.Instance.PlayNarrator(SoundManager.Instance.narr_ControllerSensitivity);
    public void Narrate_InvertY() => SoundManager.Instance.PlayNarrator(SoundManager.Instance.narr_InvertY);

    public void Narrate_Brightness() => SoundManager.Instance.PlayNarrator(SoundManager.Instance.narr_Brightness);
    public void Narrate_Fullscreen() => SoundManager.Instance.PlayNarrator(SoundManager.Instance.narr_Fullscreen);
    public void Narrate_Quality() => SoundManager.Instance.PlayNarrator(SoundManager.Instance.narr_Quality);
    public void Narrate_Resolution() => SoundManager.Instance.PlayNarrator(SoundManager.Instance.narr_Resolution);


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

    public void SetFullScreen(bool isFullScreen)
    {
        _isFullScreen = isFullScreen;
    }

    public void SetQuality(int qualityIndex)
    {
        _qualityLevel = qualityIndex;
    }

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