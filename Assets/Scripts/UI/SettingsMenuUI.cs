using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controls the Settings sub-panel inside the configuration menu.
/// Handles audio volumes and graphics quality.
/// </summary>
public class SettingsMenuUI : MonoBehaviour
{
    [Header("Audio Mixer (optional)")]
    public AudioMixer audioMixer;

    [Header("Volume Sliders")]
    public Slider masterVolumeSlider;
    public Slider musicVolumeSlider;
    public Slider sfxVolumeSlider;

    [Header("Graphics")]
    public TMP_Dropdown qualityDropdown;
    public Toggle fullscreenToggle;

    void OnEnable()
    {
        LoadValuesIntoUI();
    }

    void LoadValuesIntoUI()
    {
        if (masterVolumeSlider)  masterVolumeSlider.SetValueWithoutNotify(SettingsData.MasterVolume);
        if (musicVolumeSlider)   musicVolumeSlider.SetValueWithoutNotify(SettingsData.MusicVolume);
        if (sfxVolumeSlider)     sfxVolumeSlider.SetValueWithoutNotify(SettingsData.SFXVolume);

        if (qualityDropdown)
        {
            PopulateQualityDropdown();
            qualityDropdown.SetValueWithoutNotify(SettingsData.QualityLevel);
        }

        if (fullscreenToggle)    fullscreenToggle.SetIsOnWithoutNotify(SettingsData.Fullscreen);
    }

    void PopulateQualityDropdown()
    {
        qualityDropdown.ClearOptions();
        var names = QualitySettings.names;
        var options = new System.Collections.Generic.List<string>(names);
        qualityDropdown.AddOptions(options);
    }

    // --- Called by UI elements via OnValueChanged events ---

    public void OnMasterVolumeChanged(float value)
    {
        SettingsData.MasterVolume = value;
        if (audioMixer != null)
            audioMixer.SetFloat("MasterVolume", value > 0.0001f ? Mathf.Log10(value) * 20f : -80f);
    }

    public void OnMusicVolumeChanged(float value)
    {
        SettingsData.MusicVolume = value;
        if (audioMixer != null)
            audioMixer.SetFloat("MusicVolume", value > 0.0001f ? Mathf.Log10(value) * 20f : -80f);
    }

    public void OnSFXVolumeChanged(float value)
    {
        SettingsData.SFXVolume = value;
        if (audioMixer != null)
            audioMixer.SetFloat("SFXVolume", value > 0.0001f ? Mathf.Log10(value) * 20f : -80f);
    }

    public void OnQualityChanged(int index)
    {
        SettingsData.QualityLevel = index;
        QualitySettings.SetQualityLevel(index, true);
    }

    public void OnFullscreenChanged(bool value)
    {
        SettingsData.Fullscreen = value;
        Screen.fullScreen = value;
    }

    public void OnResetToDefaults()
    {
        SettingsData.MasterVolume  = SettingsData.DefaultMasterVolume;
        SettingsData.MusicVolume   = SettingsData.DefaultMusicVolume;
        SettingsData.SFXVolume     = SettingsData.DefaultSFXVolume;
        SettingsData.QualityLevel  = SettingsData.DefaultQualityLevel;
        SettingsData.Fullscreen    = SettingsData.DefaultFullscreen;
        SettingsData.Apply(audioMixer);
        LoadValuesIntoUI();
    }
}
