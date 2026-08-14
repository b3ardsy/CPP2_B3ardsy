using UnityEngine;
using UnityEngine.Audio;

public class MainMenuMusic : MonoBehaviour
{
    [Header("Music")]
    [SerializeField] private AudioClip mainMenuMusic;

    [Header("Audio Routing")]
    [SerializeField] private AudioMixerGroup musicMixerGroup;

    [Header("Playback")]
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private bool loopMusic = true;

    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.loop = loopMusic;
        audioSource.spatialBlend = 0f;
        audioSource.outputAudioMixerGroup = musicMixerGroup;
    }

    private void Start()
    {
        if (!playOnStart)
        {
            return;
        }

        PlayMusic();
    }

    public void PlayMusic()
    {
        if (mainMenuMusic == null)
        {
            Debug.LogWarning("MainMenuMusic: No music clip assigned.");
            return;
        }

        audioSource.clip = mainMenuMusic;
        audioSource.Play();

        Debug.Log("MainMenuMusic: Menu music started.");
    }

    public void StopMusic()
    {
        audioSource.Stop();
    }
}