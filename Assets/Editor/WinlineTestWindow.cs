using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public class WinlineTestWindow : EditorWindow
{
    private SymbolDefinition[] symbolDefs;
    private WinlineDefinition[] winlineDefs;

    [MenuItem("Window/Slots/Winline Tester")]
    public static void ShowWindow()
    {
        var w = GetWindow<WinlineTestWindow>("Winline Tester");
        w.minSize = new Vector2(400, 200);
    }

    private void OnEnable()
    {
        // Load definitions from Resources/SlotsEngine/SymbolDefinitions
        symbolDefs = Resources.LoadAll<SymbolDefinition>("SlotsEngine/SymbolDefinitions");
        winlineDefs = Resources.LoadAll<WinlineDefinition>("SlotsEngine/WinlineDefinitions");

        // Ensure a runtime WinlineEvaluator instance exists in the scene so we can call EvaluateWins
        if (WinlineEvaluator.Instance == null)
        {
            var go = new GameObject("WinlineEvaluator_TestRuntime");
            go.hideFlags = HideFlags.HideAndDontSave;
            go.AddComponent<WinlineEvaluator>();
        }

        // Ensure a GamePlayer exists and has a minimal PlayerData/BetLevel so evaluator can read CurrentBet
        EnsureGamePlayerInitialized();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Winline Tester", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        if (symbolDefs == null || symbolDefs.Length == 0)
        {
            EditorGUILayout.HelpBox("No SymbolDefinitions found in Resources/SlotsEngine/SymbolDefinitions.", MessageType.Warning);
        }
        else
        {
            EditorGUILayout.LabelField($"Loaded {symbolDefs.Length} SymbolDefinitions");
            EditorGUILayout.LabelField($"Loaded {winlineDefs?.Length ?? 0} WinlineDefinitions");
        }

        EditorGUILayout.Space();

        // Use SlotsDef_5x4 layout for presets so tests run on the same configuration
        if (GUILayout.Button("Run preset: WWW2W (straight) on SlotsDef_5x4"))
        {
            RunPatternOnSlotsDef(new string[] { "W", "W", "W", "2", "W" }, "SlotsDef_5x4");
        }

        if (GUILayout.Button("Run preset: WW1 (straight) on SlotsDef_5x4"))
        {
            RunPatternOnSlotsDef(new string[] { "W", "W", "1" }, "SlotsDef_5x4");
        }

        if (GUILayout.Button("Run preset: W22W (straight) on SlotsDef_5x4"))
        {
            RunPatternOnSlotsDef(new string[] { "W", "2", "2", "W" }, "SlotsDef_5x4");
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("Run SlotsDef_5x4 WWW2W test"))
        {
            RunSlotsDef5x4Test();
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("Reload Definitions"))
        {
            symbolDefs = Resources.LoadAll<SymbolDefinition>("SlotsEngine/SymbolDefinitions");
            winlineDefs = Resources.LoadAll<WinlineDefinition>("SlotsEngine/WinlineDefinitions");
            Debug.Log($"Reloaded {symbolDefs.Length} symbol defs, {winlineDefs.Length} winlines");
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Notes:");
        EditorGUILayout.LabelField("- Presets now use the SlotsDef_5x4 layout (consistent testing).", EditorStyles.wordWrappedLabel);
        EditorGUILayout.LabelField("- Symbol keys used are the asset names or the Name used in SymbolDefinition.", EditorStyles.wordWrappedLabel);
    }

    /// <summary>
    /// Build a grid using the columns/rows defined by the named SlotsDefinition and apply the provided bottom-row pattern.
    /// Pattern entries: symbol keys (SymbolName or asset name), or "W" to use the first wild symbol definition.
    /// If pattern length is less than columns, remaining columns default to "W".
    /// </summary>
    private void RunPatternOnSlotsDef(string[] pattern, string slotsDefName = "SlotsDef_5x4")
    {
        var slotsDef = DefinitionResolver.Resolve<SlotsDefinition>(slotsDefName);
        if (slotsDef == null)
        {
            var all = Resources.LoadAll<SlotsDefinition>("SlotsEngine/SlotsDefinitions");
            slotsDef = all.FirstOrDefault(s => s.name == slotsDefName);
        }

        if (slotsDef == null)
        {
            Debug.LogError($"SlotsDefinition '{slotsDefName}' not found.");
            return;
        }

        int columns = slotsDef.ReelDefinitions.Length;
        int[] rowsPerColumn = new int[columns];
        for (int i = 0; i < columns; i++) rowsPerColumn[i] = Mathf.Max(1, slotsDef.ReelDefinitions[i].SymbolCount);
        int maxRows = rowsPerColumn.Max();
        int gridSize = maxRows * columns;

        // Ensure symbol definitions loaded
        symbolDefs = symbolDefs ?? Resources.LoadAll<SymbolDefinition>("SlotsEngine/SymbolDefinitions");

        // default pattern fill with "W"
        string[] fullPattern = new string[columns];
        for (int c = 0; c < columns; c++) fullPattern[c] = (c < pattern.Length) ? pattern[c] ?? "W" : "W";

        SymbolData[] grid = new SymbolData[gridSize];

        for (int c = 0; c < columns; c++)
        {
            string key = fullPattern[c];
            SymbolData sd = ResolveSymbol(key);
            int idx = 0 * columns + c;
            grid[idx] = sd;

            for (int r = 1; r < maxRows; r++)
            {
                int idx2 = r * columns + c;
                grid[idx2] = null;
            }
        }

        // Use winlines from the SlotsDefinition when available
        List<WinlineDefinition> winlines = (slotsDef.WinlineDefinitions != null && slotsDef.WinlineDefinitions.Length > 0)
            ? new List<WinlineDefinition>(slotsDef.WinlineDefinitions)
            : (winlineDefs != null ? winlineDefs.ToList() : new List<WinlineDefinition>());

        var wins = WinlineEvaluator.Instance.EvaluateWins(grid, columns, rowsPerColumn, winlines);

        Debug.Log($"Pattern test '{string.Join(" ", fullPattern)}' on '{slotsDefName}': found {wins?.Count ?? 0} wins");
        if (wins != null && wins.Count > 0)
        {
            foreach (var w in wins) Debug.Log($"AWARDED: line={w.LineIndex}, value={w.WinValue}, indexes=[{string.Join(",", w.WinningSymbolIndexes)}]");
        }
        else
        {
            Debug.Log("No wins awarded for pattern test.");
        }
    }

    private void RunSlotsDef5x4Test()
    {
        // Keep existing behavior for the dedicated 5x4 test (uses bottom-row pattern WWW2W)
        RunPatternOnSlotsDef(new string[] { "W", "W", "W", "2", "W" }, "SlotsDef_5x4");
    }

    private void RunEvaluation(SymbolData[] grid, int columns, int[] rowsPerColumn)
    {
        // Ensure GamePlayer exists and is initialized before calling evaluator
        EnsureGamePlayerInitialized();

        if (WinlineEvaluator.Instance == null)
        {
            Debug.LogError("WinlineEvaluator instance not present.");
            return;
        }

        var winlines = new List<WinlineDefinition>(winlineDefs);
        var wins = WinlineEvaluator.Instance.EvaluateWins(grid, columns, rowsPerColumn, winlines);

        Debug.Log($"Evaluation result: {wins?.Count ?? 0} wins");
        if (wins != null)
        {
            foreach (var w in wins)
            {
                Debug.Log($"Win: line={w.LineIndex}, value={w.WinValue}, indexes=[{string.Join(",", w.WinningSymbolIndexes)}]");
            }
        }

        // print grid for debugging
        Debug.Log("Grid contents (column-major, row 0 = bottom):");
        for (int i = 0; i < grid.Length; i++)
        {
            var s = grid[i];
            Debug.Log($"idx={i} name={(s==null?"null":s.Name)} minWin={ (s==null?"n/a":s.MinWinDepth.ToString()) }");
        }
    }

    private SymbolData ResolveSymbol(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;

        // If key is "W" treat as any wild symbol found
        if (key == "W")
        {
            var wild = symbolDefs.FirstOrDefault(s => s.IsWild);
            if (wild != null) return wild.CreateInstance();
        }

        // try match by SymbolName property first
        var def = symbolDefs.FirstOrDefault(s => string.Equals(s.SymbolName, key, StringComparison.OrdinalIgnoreCase));
        if (def == null)
        {
            // fallback to asset name
            def = symbolDefs.FirstOrDefault(s => string.Equals(s.name, key, StringComparison.OrdinalIgnoreCase));
        }

        if (def == null)
        {
            Debug.LogWarning($"SymbolDefinition for key '{key}' not found.");
            return null;
        }

        return def.CreateInstance();
    }

    private void EnsureGamePlayerInitialized()
    {
        // Create GamePlayer singleton if missing
        if (GamePlayer.Instance == null)
        {
            var go = new GameObject("GamePlayer_TestRuntime");
            go.hideFlags = HideFlags.HideAndDontSave;
            go.AddComponent<GamePlayer>();
        }

        // If GamePlayer exists ensure it has a PlayerData with a valid CurrentBet (credit cost)
        var gp = GamePlayer.Instance;
        if (gp == null) return;

        // Use reflection to set private playerData field if it's null or missing CurrentBet
        var gpType = typeof(GamePlayer);
        var field = gpType.GetField("playerData", BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null) return;

        var current = field.GetValue(gp) as PlayerData;
        if (current != null && current.CurrentBet != null)
        {
            return; // already initialized
        }

        // Create a temporary BetLevelDefinition with creditCost = 1
        var bet = ScriptableObject.CreateInstance<BetLevelDefinition>();
        var betType = typeof(BetLevelDefinition);
        var betField = betType.GetField("creditCost", BindingFlags.Instance | BindingFlags.NonPublic);
        if (betField != null) betField.SetValue(bet, 1);

        // Create PlayerData and assign
        var pd = new PlayerData(c: 1000, bet: bet);
        field.SetValue(gp, pd);

        Debug.Log("GamePlayer test instance initialized with temporary PlayerData and BetLevelDefinition (creditCost=1)");
    }
}
