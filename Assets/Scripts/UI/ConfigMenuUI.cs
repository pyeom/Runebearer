using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Main configuration/pause menu. Press Escape to toggle.
/// Contains a Quit button and a link to the Settings sub-panel.
/// </summary>
public class ConfigMenuUI : MonoBehaviour
{
    [Header("Panels")]
    public GameObject configPanel;      // Root panel for the whole config menu
    public GameObject settingsPanel;    // Settings sub-panel (child of configPanel)

    [Header("Toggle Key")]
    public Key toggleKey = Key.Escape;

    bool isOpen;

    void Start()
    {
        // Ensure both panels start closed
        if (settingsPanel) settingsPanel.SetActive(false);
        if (configPanel)   configPanel.SetActive(false);
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame)
            Toggle();
    }

    // --- Public methods wired to UI buttons ---

    public void Toggle()
    {
        isOpen = !isOpen;
        if (configPanel) configPanel.SetActive(isOpen);

        // Close settings sub-panel whenever the root menu closes
        if (!isOpen && settingsPanel)
            settingsPanel.SetActive(false);

        // Pause / unpause time (remove if you want the game to keep running)
        Time.timeScale = isOpen ? 0f : 1f;
    }

    public void Open()
    {
        if (isOpen) return;
        Toggle();
    }

    public void Close()
    {
        if (!isOpen) return;
        Toggle();
    }

    public void OnSettingsButton()
    {
        if (settingsPanel)
            settingsPanel.SetActive(!settingsPanel.activeSelf);
    }

    public void OnQuitButton()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
