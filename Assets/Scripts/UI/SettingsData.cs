using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Manages saving and loading of player settings via PlayerPrefs.
/// </summary>
public static class SettingsData
{
    const string KeyMasterVolume  = "vol_master";
    const string KeyMusicVolume   = "vol_music";
    const string KeySFXVolume     = "vol_sfx";
    const string KeyQualityLevel  = "quality_level";
    const string KeyFullscreen    = "fullscreen";

    // Defaults
    public static float DefaultMasterVolume  = 1f;
    public static float DefaultMusicVolume   = 0.8f;
    public static float DefaultSFXVolume     = 1f;
    public static int   DefaultQualityLevel  = 2;   // Medium
    public static bool  DefaultFullscreen    = true;

    public static float MasterVolume
    {
        get => PlayerPrefs.GetFloat(KeyMasterVolume, DefaultMasterVolume);
        set { PlayerPrefs.SetFloat(KeyMasterVolume, value); PlayerPrefs.Save(); }
    }

    public static float MusicVolume
    {
        get => PlayerPrefs.GetFloat(KeyMusicVolume, DefaultMusicVolume);
        set { PlayerPrefs.SetFloat(KeyMusicVolume, value); PlayerPrefs.Save(); }
    }

    public static float SFXVolume
    {
        get => PlayerPrefs.GetFloat(KeySFXVolume, DefaultSFXVolume);
        set { PlayerPrefs.SetFloat(KeySFXVolume, value); PlayerPrefs.Save(); }
    }

    public static int QualityLevel
    {
        get => PlayerPrefs.GetInt(KeyQualityLevel, DefaultQualityLevel);
        set { PlayerPrefs.SetInt(KeyQualityLevel, value); PlayerPrefs.Save(); }
    }

    public static bool Fullscreen
    {
        get => PlayerPrefs.GetInt(KeyFullscreen, DefaultFullscreen ? 1 : 0) == 1;
        set { PlayerPrefs.SetInt(KeyFullscreen, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    /// <summary>Applies all saved settings to the game immediately.</summary>
    public static void Apply(AudioMixer mixer = null)
    {
        if (mixer != null)
        {
            // AudioMixer volumes use dB: map 0-1 linear to -80..0 dB
            mixer.SetFloat("MasterVolume", LinearToDb(MasterVolume));
            mixer.SetFloat("MusicVolume",  LinearToDb(MusicVolume));
            mixer.SetFloat("SFXVolume",    LinearToDb(SFXVolume));
        }

        QualitySettings.SetQualityLevel(QualityLevel, true);
        Screen.fullScreen = Fullscreen;
    }

    static float LinearToDb(float linear)
    {
        return linear > 0.0001f ? Mathf.Log10(linear) * 20f : -80f;
    }
}
