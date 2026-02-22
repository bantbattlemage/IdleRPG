using System;
using UnityEngine;

/// <summary>
/// Consolidated debug tool for testing game features via GUI.
/// Toggle visibility with F1.
/// </summary>
public class TestTool : MonoBehaviour
{
    private bool visible = false;
    private Rect windowRect = new Rect(10, 200, 400, 650);
    private Vector2 scrollPosition = Vector2.zero;

    // RNG Seed fields
    private string seedInput = string.Empty;
    private string rngStatus = string.Empty;
    private const string DevSeedKey = "Dev_RNGSeed";

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1))
        {
            visible = !visible;
        }
    }

    void OnGUI()
    {
        if (!visible) return;

        windowRect = GUI.Window(987654, windowRect, WindowFunction, "Test Tool (F1 to toggle)");
    }

    private void WindowFunction(int id)
    {
        scrollPosition = GUILayout.BeginScrollView(scrollPosition);

        GUILayout.Label("=== Slot Management ===", GUI.skin.box);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Add Slot (Inventory)", GUILayout.Height(30)))
        {
            Debug.Log("TestTool: Add inventory-backed slot");
            if (GamePlayer.Instance != null && GamePlayer.Instance.CheckAllSlotsState(State.Idle))
            {
                GamePlayer.Instance.AddTestSlot();
            }
            else
            {
                Debug.LogWarning("Cannot add slot - not all slots are idle");
            }
        }
        if (GUILayout.Button("Remove Slot (Inventory)", GUILayout.Height(30)))
        {
            Debug.Log("TestTool: Remove inventory-backed slot");
            GamePlayer.Instance?.RemoveTestSlot();
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Add Slot (No Inventory)", GUILayout.Height(30)))
        {
            Debug.Log("TestTool: Add non-inventory slot");
            if (GamePlayer.Instance != null && GamePlayer.Instance.CheckAllSlotsState(State.Idle))
            {
                GamePlayer.Instance.AddTestSlotNoInventory();
            }
            else
            {
                Debug.LogWarning("Cannot add slot - not all slots are idle");
            }
        }
        if (GUILayout.Button("Remove Slot (No Inventory)", GUILayout.Height(30)))
        {
            Debug.Log("TestTool: Remove non-inventory slot");
            GamePlayer.Instance?.RemoveTestNonInventorySlot();
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(10);
        GUILayout.Label("=== Reel Management ===", GUI.skin.box);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Add Reel", GUILayout.Height(30)))
        {
            Debug.Log("TestTool: Add reel");
            if (GamePlayer.Instance != null && GamePlayer.Instance.CheckAllSlotsState(State.Idle))
            {
                GamePlayer.Instance.AddTestReel();
            }
            else
            {
                Debug.LogWarning("Cannot add reel - not all slots are idle");
            }
        }
        if (GUILayout.Button("Remove Reel", GUILayout.Height(30)))
        {
            Debug.Log("TestTool: Remove reel");
            if (GamePlayer.Instance != null && GamePlayer.Instance.CheckAllSlotsState(State.Idle))
            {
                GamePlayer.Instance.RemoveTestReel();
            }
            else
            {
                Debug.LogWarning("Cannot remove reel - not all slots are idle");
            }
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(10);
        GUILayout.Label("=== Symbol Management ===", GUI.skin.box);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Add Symbol", GUILayout.Height(30)))
        {
            Debug.Log("TestTool: Add symbol");
            if (GamePlayer.Instance != null && GamePlayer.Instance.CheckAllSlotsState(State.Idle))
            {
                GamePlayer.Instance.AddTestSymbol();
            }
            else
            {
                Debug.LogWarning("Cannot add symbol - not all slots are idle");
            }
        }
        if (GUILayout.Button("Remove Symbol", GUILayout.Height(30)))
        {
            Debug.Log("TestTool: Remove symbol");
            if (GamePlayer.Instance != null && GamePlayer.Instance.CheckAllSlotsState(State.Idle))
            {
                GamePlayer.Instance.RemoveTestSymbol();
            }
            else
            {
                Debug.LogWarning("Cannot remove symbol - not all slots are idle");
            }
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(10);
        GUILayout.Label("=== Credits & Game Speed ===", GUI.skin.box);

        if (GUILayout.Button("Add 100 Credits", GUILayout.Height(30)))
        {
            Debug.Log("TestTool: Adding 100 credits");
            GamePlayer.Instance?.AddCredits(100);
        }

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Speed Down (-)", GUILayout.Height(30)))
        {
            Time.timeScale = Mathf.Clamp(Time.timeScale - 0.1f, 0.1f, 10f);
            Debug.Log($"TestTool: Time scale = {Time.timeScale}");
        }
        if (GUILayout.Button("Speed Up (+)", GUILayout.Height(30)))
        {
            Time.timeScale = Mathf.Clamp(Time.timeScale + 0.1f, 0.1f, 10f);
            Debug.Log($"TestTool: Time scale = {Time.timeScale}");
        }
        GUILayout.EndHorizontal();

        GUILayout.Label($"Current Time Scale: {Time.timeScale:F2}", GUILayout.Height(20));

        GUILayout.Space(10);
        GUILayout.Label("=== RNG Seed ===", GUI.skin.box);

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
                PlayerPrefs.SetInt(DevSeedKey, s);
                PlayerPrefs.Save();
                rngStatus = $"Set seed to {s} and saved for New Game.";
            }
            else
            {
                rngStatus = "Invalid int. Enter a valid integer.";
            }
        }

        if (GUILayout.Button("Reseed", GUILayout.Height(28)))
        {
            RNGManager.Reseed();
            PlayerPrefs.SetInt(DevSeedKey, RNGManager.CurrentSeed);
            PlayerPrefs.Save();
            rngStatus = $"Reseeded (now {RNGManager.CurrentSeed}) and saved for New Game.";
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Set Seed = Time", GUILayout.Height(22)))
        {
            int t = Environment.TickCount;
            RNGManager.SetSeed(t);
            PlayerPrefs.SetInt(DevSeedKey, t);
            PlayerPrefs.Save();
            rngStatus = $"Set seed to time-derived {t} and saved for New Game.";
        }
        if (GUILayout.Button("Copy Current", GUILayout.Height(22)))
        {
            GUIUtility.systemCopyBuffer = RNGManager.CurrentSeed.ToString();
            rngStatus = "Current seed copied to clipboard.";
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Clear Saved Seed", GUILayout.Height(22)))
        {
            if (PlayerPrefs.HasKey(DevSeedKey))
            {
                PlayerPrefs.DeleteKey(DevSeedKey);
                PlayerPrefs.Save();
                rngStatus = "Cleared saved dev seed.";
            }
            else rngStatus = "No saved dev seed to clear.";
        }
        if (GUILayout.Button("Load Saved Seed", GUILayout.Height(22)))
        {
            if (PlayerPrefs.HasKey(DevSeedKey))
            {
                int s = PlayerPrefs.GetInt(DevSeedKey);
                RNGManager.SetSeed(s);
                rngStatus = $"Loaded saved seed {s} and applied.";
            }
            else rngStatus = "No saved dev seed found.";
        }
        GUILayout.EndHorizontal();

        if (!string.IsNullOrEmpty(rngStatus))
        {
            GUILayout.Space(4);
            GUILayout.Label(rngStatus);
        }

        GUILayout.Space(10);
        GUILayout.Label("=== UI ===", GUI.skin.box);

        if (GUILayout.Button("Open Inventory (I)", GUILayout.Height(30)))
        {
            Debug.Log("TestTool: Opening inventory");
            GameInterfaceController.Instance?.InventoryInterface?.OpenInventory();
        }

        GUILayout.EndScrollView();

        GUI.DragWindow(new Rect(0, 0, 10000, 20));
    }
}
