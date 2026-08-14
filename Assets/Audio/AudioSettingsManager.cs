using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class AudioSettingsManager : MonoBehaviour
{
    [Header("Audio Mixer")]
    [SerializeField] private AudioMixer audioMixer;

    [Header("Volume Sliders")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;

    private const string MasterVolumeParameter = "MasterVolume";
    private const string MusicVolumeParameter = "MusicVolume";
    private const string SFXVolumeParameter = "SFXVolume";

    private const string MasterVolumeKey = "MasterVolume";
    private const string MusicVolumeKey = "MusicVolume";
    private const string SFXVolumeKey = "SFXVolume";

    private const float DefaultVolume = 1f;
    private const float MinimumVolume = 0.0001f;

    private void Start()
    {
        LoadVolumeSettings();
    }

    public void SetMasterVolume(float volume)
    {
        SetMixerVolume(MasterVolumeParameter, volume);
        PlayerPrefs.SetFloat(MasterVolumeKey, volume);
    }

    public void SetMusicVolume(float volume)
    {
        SetMixerVolume(MusicVolumeParameter, volume);
        PlayerPrefs.SetFloat(MusicVolumeKey, volume);
    }

    public void SetSFXVolume(float volume)
    {
        SetMixerVolume(SFXVolumeParameter, volume);
        PlayerPrefs.SetFloat(SFXVolumeKey, volume);
    }

    private void LoadVolumeSettings()
    {
        float masterVolume =
            PlayerPrefs.GetFloat(MasterVolumeKey, DefaultVolume);

        float musicVolume =
            PlayerPrefs.GetFloat(MusicVolumeKey, DefaultVolume);

        float sfxVolume =
            PlayerPrefs.GetFloat(SFXVolumeKey, DefaultVolume);

        masterVolumeSlider.SetValueWithoutNotify(masterVolume);
        musicVolumeSlider.SetValueWithoutNotify(musicVolume);
        sfxVolumeSlider.SetValueWithoutNotify(sfxVolume);

        SetMixerVolume(MasterVolumeParameter, masterVolume);
        SetMixerVolume(MusicVolumeParameter, musicVolume);
        SetMixerVolume(SFXVolumeParameter, sfxVolume);

        Debug.Log("Audio Settings: Volume settings loaded.");
    }

    private void SetMixerVolume(string parameter, float volume)
    {
        volume = Mathf.Clamp(volume, MinimumVolume, 1f);

        float decibels = 20f * Mathf.Log10(volume);

        audioMixer.SetFloat(parameter, decibels);
    }
}