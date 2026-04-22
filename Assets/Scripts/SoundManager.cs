using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance;

    [Header("Audio Sources")]
    public AudioSource musicSource;
    public AudioSource sfxSource;

    [Header("Audio Clips")]
    public AudioClip backgroundMusic;
    public AudioClip diceRollSFX;
    public AudioClip walkSFX;

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
        {
            sfxSource.PlayOneShot(walkSFX);
        }
    }
}