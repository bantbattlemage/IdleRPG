using System;
using UnityEngine;

/// <summary>
/// Simple runtime dev tool to set the centralized RNG seed.
/// Toggle visibility with F1. Attach to any GameObject in the scene.
/// </summary>
[AddComponentMenu("Dev/RNG Seed Setter")]
public class RNGSeedSetter : MonoBehaviour
{
    private Rect windowRect = new Rect(10, 10, 300, 160);
    private string seedInput = string.Empty;
    private string status = string.Empty;
    private bool visible = false; // start hidden by default

    // Key used to persist a developer-selected seed so NewGame can apply it at startup
    private const string DevSeedKey = "Dev_RNGSeed";

    void Update()
    {
        // Toggle the UI with F1
        if (Input.GetKeyDown(KeyCode.F1))
            visible = !visible;
    }

    void OnGUI()
    {
        // Only show when enabled
        if (!visible) return;

        windowRect = GUI.Window(123456, windowRect, WindowFunction, "RNG Seed Setter");
    }

    private void WindowFunction(int id)
    {
        GUILayout.BeginVertical();

        GUILayout.Label($"Current seed: {RNGManager.CurrentSeed}");

        GUILayout.BeginHorizontal();
        GUILayout.Label("Seed (int):", GUILayout.Width(70));
        seedInput = GUILayout.TextField(seedInput);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Set Seed", GUILayout.Height(28)))
        {
            if (int.TryParse(seedInput, out var s))
            {
                RNGManager.SetSeed(s);
                // persist for NewGame usage
                PlayerPrefs.SetInt(DevSeedKey, s);
                PlayerPrefs.Save();
                status = $"Set seed to {s} and saved for New Game.";
            }
            else
            {
                status = "Invalid int. Enter a valid integer.";
            }
        }

        if (GUILayout.Button("Reseed", GUILayout.Height(28)))
        {
            RNGManager.Reseed();
            // persist the new value
            PlayerPrefs.SetInt(DevSeedKey, RNGManager.CurrentSeed);
            PlayerPrefs.Save();
            status = $"Reseeded (now {RNGManager.CurrentSeed}) and saved for New Game.";
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Set Seed = Time", GUILayout.Height(22)))
        {
            int t = Environment.TickCount;
            RNGManager.SetSeed(t);
            PlayerPrefs.SetInt(DevSeedKey, t);
            PlayerPrefs.Save();
            status = $"Set seed to time-derived {t} and saved for New Game.";
        }
        if (GUILayout.Button("Copy Current", GUILayout.Height(22)))
        {
            GUIUtility.systemCopyBuffer = RNGManager.CurrentSeed.ToString();
            status = "Current seed copied to clipboard.";
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Clear Saved Seed", GUILayout.Height(22)))
        {
            if (PlayerPrefs.HasKey(DevSeedKey))
            {
                PlayerPrefs.DeleteKey(DevSeedKey);
                PlayerPrefs.Save();
                status = "Cleared saved dev seed.";
            }
            else status = "No saved dev seed to clear.";
        }
        if (GUILayout.Button("Load Saved Seed", GUILayout.Height(22)))
        {
            if (PlayerPrefs.HasKey(DevSeedKey))
            {
                int s = PlayerPrefs.GetInt(DevSeedKey);
                RNGManager.SetSeed(s);
                status = $"Loaded saved seed {s} and applied.";
            }
            else status = "No saved dev seed found.";
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(6);
        GUILayout.Label(status);

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Close", GUILayout.Height(22))) visible = false;

        GUILayout.EndVertical();

        // Allow window dragging
        GUI.DragWindow(new Rect(0, 0, 10000, 20));
    }
}
