using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance;

    [Header("Audio Sources")]
    public AudioSource musicSource;
    public AudioSource sfxSource;
    public AudioSource narratorSource;

    [Header("Audio Clips - SFX")]
    public AudioClip backgroundMusic;
    public AudioClip diceRollSFX;
    public AudioClip walkSFX;

    [Header("Narrator - Main Menu")]
    public AudioClip narr_Play;
    public AudioClip narr_Quit;
    public AudioClip narr_Settings;

    [Header("Narrator - Pause Menu")]
    public AudioClip narr_ResumeGame;
    public AudioClip narr_ReturnToMainMenu;

    [Header("Narrator - Options Buttons")]
    public AudioClip narr_AudioSettings;
    public AudioClip narr_GeneralOptions;
    public AudioClip narr_Graphics;
    public AudioClip narr_Controls;
    public AudioClip narr_ApplyButton;

    [Header("Narrator - Audio Settings")]
    public AudioClip narr_SFXVolume;
    public AudioClip narr_MusicVolume;
    public AudioClip narr_NarratorVolume;

    [Header("Narrator - General Settings")]
    public AudioClip narr_ControllerSensitivity;
    public AudioClip narr_InvertY;

    [Header("Narrator - Graphics Settings")]
    public AudioClip narr_Brightness;
    public AudioClip narr_Fullscreen;
    public AudioClip narr_Quality;
    public AudioClip narr_Resolution;

    [Header("Narrator - Dice")]
    public AudioClip[] narr_Dice;

    [Header("Narrator - Turn")]
    public AudioClip narr_P1Turn;
    public AudioClip narr_P2Turn;

    [Header("Narrator - House")]
    public AudioClip narr_BuyHouse;
    public AudioClip narr_Yes;
    public AudioClip narr_No;

    [Header("Narrator - Properties")]
    public AudioClip[] narr_Properties;

    [Header("Narrator - Owned Properties")]
    public AudioClip[] narr_HaveHouse;

    private AudioClip _lastNarratorClip;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        PlayMusic();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R) && _lastNarratorClip != null)
        {
            narratorSource.Stop();
            narratorSource.clip = _lastNarratorClip;
            narratorSource.Play();
        }
    }

    public void PlayMusic()
    {
        if (backgroundMusic != null)
        {
            musicSource.clip = backgroundMusic;
            musicSource.loop = true;
            musicSource.Play();
        }
    }

    public void PlayDiceRoll()
    {
        if (diceRollSFX != null)
        {
            sfxSource.clip = diceRollSFX;
            sfxSource.loop = true;
            sfxSource.Play();
        }
    }

    public void PlayWalk()
    {
        if (walkSFX != null)
            sfxSource.PlayOneShot(walkSFX);
    }

    public void PlayNarrator(AudioClip clip)
    {
        if (clip == null) return;
        _lastNarratorClip = clip;
        narratorSource.Stop();
        narratorSource.clip = clip;
        narratorSource.Play();
    }

    public void PlayDiceNarrator(int diceValue)
    {
        int idx = diceValue - 1;
        if (narr_Dice != null && idx >= 0 && idx < narr_Dice.Length)
            PlayNarrator(narr_Dice[idx]);
    }

    public void PlayPropertyNarrator(int propertyIndex)
    {
        if (narr_Properties != null && propertyIndex >= 0 && propertyIndex < narr_Properties.Length)
            PlayNarrator(narr_Properties[propertyIndex]);
    }

    public void PlayHaveHouseNarrator(int propertyIndex)
    {
        if (narr_HaveHouse != null && propertyIndex >= 0 && propertyIndex < narr_HaveHouse.Length)
            PlayNarrator(narr_HaveHouse[propertyIndex]);
    }
}