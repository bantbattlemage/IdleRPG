using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public class WinlinePreviewWindow : EditorWindow
{
    private List<UnityEngine.Object> winlineDefs = new List<UnityEngine.Object>();
    private int columns = 3;
    private string rowsPerColumnText = "3,3,3";
    private Vector2 scroll;

    private enum TestPattern { AllSame, DiagonalMatch, Alternating }
    private TestPattern testPattern = TestPattern.AllSame;

    private string output = string.Empty;

    // Visualization state
    private string[] lastGrid = null;
    private int[] lastRowsPerCol = null;
    private int lastColumns = 0;
    private Dictionary<int, List<int>> lastWinningIndices = new Dictionary<int, List<int>>();
    private Dictionary<int, int[]> lastGeneratedIndexes = new Dictionary<int, int[]>();

    [MenuItem("Window/Slots/Winline Preview")]
    public static void ShowWindow()
    {
        var w = GetWindow<WinlinePreviewWindow>("Winline Preview");
        w.minSize = new Vector2(400, 300);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Winline Preview & Tests", EditorStyles.boldLabel);

        EditorGUILayout.Space();

        // Winline definitions list
        EditorGUILayout.LabelField("Winline Definitions", EditorStyles.label);
        int toRemove = -1;
        for (int i = 0; i < winlineDefs.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            winlineDefs[i] = EditorGUILayout.ObjectField(winlineDefs[i], typeof(UnityEngine.Object), false);
            if (GUILayout.Button("Remove", GUILayout.Width(60))) toRemove = i;
            EditorGUILayout.EndHorizontal();
        }
        if (toRemove >= 0) winlineDefs.RemoveAt(toRemove);

        if (GUILayout.Button("Add Winline Definition"))
        {
            winlineDefs.Add(null);
        }

        EditorGUILayout.Space();

        // Grid dimensions
        EditorGUILayout.LabelField("Grid Dimensions", EditorStyles.label);
        columns = EditorGUILayout.IntField("Columns (reels)", Math.Max(1, columns));
        rowsPerColumnText = EditorGUILayout.TextField("Rows per column (csv)", rowsPerColumnText);

        // Pattern
        testPattern = (TestPattern)EditorGUILayout.EnumPopup("Test Pattern", testPattern);

        EditorGUILayout.Space();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Generate Indexes"))
        {
            GenerateIndexes();
        }
        if (GUILayout.Button("Simulate Evaluation"))
        {
            SimulateEvaluation();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        // Visualization area
        DrawVisualizationArea();

        EditorGUILayout.Space();

        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(200));
        EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
        EditorGUILayout.TextArea(output, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    private int[] ParseRowsPerColumn()
    {
        var parts = rowsPerColumnText.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
        var result = new int[columns];
        for (int i = 0; i < columns; i++)
        {
            if (i < parts.Length && int.TryParse(parts[i].Trim(), out int v)) result[i] = Math.Max(0, v);
            else result[i] = 0;
        }
        return result;
    }

    private void GenerateIndexes()
    {
        output = string.Empty;
        lastGeneratedIndexes.Clear();
        var perCol = ParseRowsPerColumn();

        if (winlineDefs == null || winlineDefs.Count == 0)
        {
            output = "No WinlineDefinitions added.\n";
            return;
        }

        for (int i = 0; i < winlineDefs.Count; i++)
        {
            var obj = winlineDefs[i];
            if (obj == null)
            {
                output += $"[{i}] null definition\n";
                continue;
            }

            // Attempt to call GenerateIndexes(columns, int[])
            Type t = obj.GetType();
            MethodInfo gen = t.GetMethod("GenerateIndexes", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(int), typeof(int[]) }, null);
            if (gen == null)
            {
                // fallback to legacy signature
                gen = t.GetMethod("GenerateIndexes", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(int), typeof(int) }, null);
                if (gen != null)
                {
                    int max = perCol.Max();
                    var res = gen.Invoke(obj, new object[] { columns, max }) as int[];
                    lastGeneratedIndexes[i] = res;
                    output += $"[{i}] {obj.name} -> Indexes: [{(res != null ? string.Join(",", res) : "(null)")} ]\n";
                    continue;
                }

                output += $"[{i}] {obj.name} -> no compatible GenerateIndexes method found\n";
                continue;
            }

            var indexes = gen.Invoke(obj, new object[] { columns, perCol }) as int[];
            lastGeneratedIndexes[i] = indexes;
            output += $"[{i}] {obj.name} -> Indexes: [{(indexes != null ? string.Join(",", indexes) : "(null)")} ]\n";
        }

        Repaint();
    }

    private void SimulateEvaluation()
    {
        output = string.Empty;
        lastWinningIndices.Clear();
        var perCol = ParseRowsPerColumn();
        int maxRows = perCol.Max();
        int totalCells = maxRows * columns;
        lastGrid = new string[totalCells];
        lastRowsPerCol = perCol;
        lastColumns = columns;

        if (winlineDefs == null || winlineDefs.Count == 0)
        {
            output = "No WinlineDefinitions to evaluate.\n";
            return;
        }

        // Build name-based grid (column-major)
        for (int c = 0; c < columns; c++)
        {
            for (int r = 0; r < perCol[c]; r++)
            {
                int idx = r * columns + c;
                string name = "A";
                switch (testPattern)
                {
                    case TestPattern.AllSame:
                        name = "A";
                        break;
                    case TestPattern.DiagonalMatch:
                        if (r == c) name = "X"; else name = "A";
                        break;
                    case TestPattern.Alternating:
                        name = ((r + c) % 2 == 0) ? "A" : "B";
                        break;
                }

                lastGrid[idx] = name;
            }

            for (int r = perCol[c]; r < maxRows; r++)
            {
                lastGrid[r * columns + c] = null;
            }
        }

        // Evaluate each winline by calling GenerateIndexes and checking names
        for (int i = 0; i < winlineDefs.Count; i++)
        {
            var obj = winlineDefs[i];
            if (obj == null) continue;

            Type t = obj.GetType();
            MethodInfo gen = t.GetMethod("GenerateIndexes", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(int), typeof(int[]) }, null);
            int[] indexes = null;
            if (gen != null)
            {
                indexes = gen.Invoke(obj, new object[] { columns, perCol }) as int[];
            }
            else
            {
                MethodInfo legacy = t.GetMethod("GenerateIndexes", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(int), typeof(int) }, null);
                if (legacy != null) indexes = legacy.Invoke(obj, new object[] { columns, maxRows }) as int[];
            }

            lastGeneratedIndexes[i] = indexes;

            if (indexes == null || indexes.Length == 0)
            {
                output += $"[{i}] {obj.name}: no indexes\n";
                continue;
            }

            // Find first non-null grid cell among indexes to choose symbol to match
            string match = null;
            foreach (int idx in indexes)
            {
                if (idx < 0 || idx >= lastGrid.Length) continue;
                if (lastGrid[idx] != null) { match = lastGrid[idx]; break; }
            }

            if (string.IsNullOrEmpty(match))
            {
                output += $"[{i}] {obj.name}: no valid starting cell in grid\n";
                continue;
            }

            List<int> winning = new List<int>();
            foreach (int idx in indexes)
            {
                if (idx < 0 || idx >= lastGrid.Length) break;
                int col = idx % columns;
                int row = idx / columns;
                if (row >= perCol[col]) break; // column doesn't have this row
                if (lastGrid[idx] == null) break;
                if (lastGrid[idx] == match) winning.Add(idx);
                else break;
            }

            lastWinningIndices[i] = winning;
            output += $"[{i}] {obj.name}: match='{match}' winningIndexes=[{string.Join(",", winning)}]\n";
        }

        Repaint();
    }

    private void DrawVisualizationArea()
    {
        // If no lastGrid, show placeholder
        if (lastGrid == null || lastRowsPerCol == null || lastColumns == 0)
        {
            EditorGUILayout.LabelField("Grid preview will appear here after simulation", EditorStyles.centeredGreyMiniLabel);
            return;
        }

        int cols = lastColumns;
        int maxRows = lastRowsPerCol.Max();

        // Reserve an area
        float areaHeight = Mathf.Min(400, maxRows * 36 + 24);
        Rect area = GUILayoutUtility.GetRect(EditorGUIUtility.currentViewWidth, areaHeight);
        GUI.Box(area, GUIContent.none);

        // Compute cell size
        float padding = 4f;
        float availableWidth = area.width - padding * 2;
        float cellSize = Mathf.Floor(Mathf.Min(40f, availableWidth / cols));
        float startX = area.x + (area.width - (cellSize * cols)) / 2f;
        float startY = area.y + 8f;

        // Draw cells column-major: rows bottom-to-top, but we'll render top row at top
        for (int r = maxRows - 1; r >= 0; r--)
        {
            for (int c = 0; c < cols; c++)
            {
                int idx = r * cols + c;
                Rect cellRect = new Rect(startX + c * cellSize, startY + (maxRows - 1 - r) * cellSize, cellSize - 2, cellSize - 2);

                // background color depending on value
                Color bg = new Color(0.2f, 0.2f, 0.2f); // missing
                string val = null;
                if (idx >= 0 && idx < lastGrid.Length) val = lastGrid[idx];
                if (val == null)
                {
                    bg = new Color(0.15f, 0.15f, 0.15f);
                }
                else if (val == "A")
                {
                    bg = Color.white;
                }
                else if (val == "B")
                {
                    bg = new Color(0.6f, 0.8f, 1f);
                }
                else if (val == "X")
                {
                    bg = Color.yellow;
                }

                EditorGUI.DrawRect(cellRect, bg);

                // highlight if part of any winningIndices
                bool highlighted = false;
                foreach (var kv in lastWinningIndices)
                {
                    if (kv.Value != null && kv.Value.Contains(idx)) { highlighted = true; break; }
                }

                if (highlighted)
                {
                    // draw border
                    Rect border = new Rect(cellRect.x - 1, cellRect.y - 1, cellRect.width + 2, cellRect.height + 2);
                    EditorGUI.DrawRect(border, new Color(0f, 1f, 0f, 0.6f));
                }

                // index label
                var style = new GUIStyle(EditorStyles.label);
                style.alignment = TextAnchor.LowerRight;
                style.normal.textColor = Color.black;
                GUI.Label(new Rect(cellRect.x + 2, cellRect.y + 2, cellRect.width - 4, cellRect.height - 4), (val ?? " ") , style);

                // numeric index small
                var idxStyle = new GUIStyle(EditorStyles.miniLabel);
                idxStyle.alignment = TextAnchor.UpperLeft;
                idxStyle.normal.textColor = Color.black;
                GUI.Label(new Rect(cellRect.x + 2, cellRect.y + 2, 24, 16), idx.ToString(), idxStyle);
            }
        }

        // Legend
        Rect legendRect = new Rect(area.x + 8, area.yMax - 18, area.width - 16, 16);
        GUI.Label(legendRect, "Legend: Yellow=X, White=A, Blue=B, Green border = winning cells", EditorStyles.miniLabel);
    }
}
