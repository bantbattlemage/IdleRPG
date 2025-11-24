using UnityEngine;

[CreateAssetMenu(fileName = "Data", menuName = "ScriptableObjects/WinlineDefinition")]
public class WinlineDefinition : ScriptableObject
{
    public enum PatternType { StraightAcross, DiagonalDown, DiagonalUp, Custom }

    [SerializeField] private PatternType pattern = PatternType.StraightAcross;
    [SerializeField] private int winMultiplier = 1;
    [SerializeField] private int rowIndex = 0; // 0 = bottom visual row
    [SerializeField] private int rowOffset = 0; // diagonal start offset
    [SerializeField] private int[] customRowOffsets; // per-column rows (Custom pattern)
    [HideInInspector][SerializeField] private bool runtimeTransient = false; // identifies runtime-created temporary lines

    // Public accessors (unchanged external API expectations)
    public PatternType Pattern => pattern;
    public int WinMultiplier => winMultiplier;
    public int RowIndex => rowIndex;
    public int RowOffset => rowOffset;
    public int[] CustomRowOffsets => customRowOffsets;
    public bool IsRuntimeTransient => runtimeTransient;

    // Mutators (replace reflection usage)
    public void SetRowIndex(int r) { rowIndex = r; }
    public void SetRowOffset(int o) { rowOffset = o; }
    public void SetCustomRowOffsets(int[] offsets) { customRowOffsets = offsets; }
    public void SetWinMultiplier(int m) { winMultiplier = m; }

    // Runtime configuration (replaces previous reflection setting of private fields)
    public void ConfigureRuntime(PatternType newPattern, int multiplier, int newRowIndex, int newRowOffset, int[] newCustomOffsets)
    {
        pattern = newPattern;
        winMultiplier = multiplier;
        rowIndex = newRowIndex;
        rowOffset = newRowOffset;
        customRowOffsets = newCustomOffsets;
        runtimeTransient = true;
    }

    public static WinlineDefinition CreateRuntimeStraightAcross(int row, int multiplier = 1)
    {
        var inst = CreateInstance<WinlineDefinition>();
        inst.ConfigureRuntime(PatternType.StraightAcross, multiplier, row, 0, null);
        inst.name = $"__runtime_Straight_row_{row}";
        return inst;
    }

    public static WinlineDefinition CreateRuntimeCustom(int[] perColumnRows, int multiplier = 1)
    {
        var inst = CreateInstance<WinlineDefinition>();
        int[] copy = null;
        if (perColumnRows != null)
        {
            copy = new int[perColumnRows.Length];
            System.Array.Copy(perColumnRows, copy, perColumnRows.Length);
        }
        inst.ConfigureRuntime(PatternType.Custom, multiplier, 0, 0, copy);
        inst.name = "__runtime_Custom";
        return inst;
    }

    /// <summary>
    /// Clone this definition into a runtime-transient instance (replaces prior reflection mutation approach).
    /// </summary>
    public WinlineDefinition CloneForRuntime()
    {
        var clone = CreateInstance<WinlineDefinition>();
        int[] offsetsCopy = customRowOffsets != null ? (int[])customRowOffsets.Clone() : null;
        clone.ConfigureRuntime(pattern, winMultiplier, rowIndex, rowOffset, offsetsCopy);
        clone.name = name + "_runtimeClone";
        return clone;
    }

    // Legacy overload retained
    public int[] GenerateIndexes(int columns, int rows)
    {
        int[] perCol = new int[columns];
        for (int i = 0; i < columns; i++) perCol[i] = rows;
        return GenerateIndexes(columns, perCol);
    }

    /// <summary>
    /// Generates one index per column (or -1 if invalid) using column-major indexing (row * columns + column).
    /// StraightAcross matches original behavior by anchoring column 0 with clamped row when requested row exceeds its height.
    /// This reproduces previous reflection-based semantics so existing evaluation logic continues to function.
    /// </summary>
    public int[] GenerateIndexes(int columns, int[] rowsPerColumn)
    {
        if (columns <= 0 || rowsPerColumn == null || rowsPerColumn.Length != columns) return System.Array.Empty<int>();
        int maxRows = 0; for (int i = 0; i < rowsPerColumn.Length; i++) if (rowsPerColumn[i] > maxRows) maxRows = rowsPerColumn[i];
        if (maxRows <= 0) return System.Array.Empty<int>();

        var result = new int[columns];
        for (int i = 0; i < columns; i++) result[i] = -1;

        switch (pattern)
        {
            case PatternType.StraightAcross:
            {
                int requested = rowIndex;
                // For straight-across patterns used by presentation we must use the same visual row
                // index across all columns. If a column does not contain that row, mark it invalid (-1).
                for (int c = 0; c < columns; c++)
                {
                    if (requested >= 0 && c < rowsPerColumn.Length && rowsPerColumn[c] > requested)
                        result[c] = requested * columns + c;
                    else
                        result[c] = -1;
                }
                return result;
            }
            case PatternType.DiagonalDown:
            {
                int desiredBase = (maxRows - 1 - rowOffset);
                int base0 = ClampRow(desiredBase, rowsPerColumn[0]);
                for (int c = 0; c < columns; c++)
                {
                    int row = base0 - c;
                    if (row >= 0 && row < rowsPerColumn[c]) result[c] = row * columns + c; else result[c] = -1;
                }
                return result;
            }
            case PatternType.DiagonalUp:
            {
                int desiredBase = rowOffset;
                int base0 = ClampRow(desiredBase, rowsPerColumn[0]);
                for (int c = 0; c < columns; c++)
                {
                    int rowUp = base0 + c;
                    if (rowUp >= 0 && rowUp < rowsPerColumn[c]) result[c] = rowUp * columns + c; else result[c] = -1;
                }
                return result;
            }
            case PatternType.Custom:
            {
                if (customRowOffsets != null && customRowOffsets.Length == columns)
                {
                    for (int c = 0; c < columns; c++)
                    {
                        int rc = customRowOffsets[c];
                        if (rc >= 0 && rc < rowsPerColumn[c]) result[c] = rc * columns + c; else result[c] = -1;
                    }
                    return result;
                }
                // Fallback: center row selection (existing legacy fallback)
                int mid = maxRows / 2;
                for (int c2 = 0; c2 < columns; c2++)
                {
                    if (mid < rowsPerColumn[c2]) result[c2] = mid * columns + c2; else result[c2] = -1;
                }
                return result;
            }
            default:
                return result;
        }
    }

    private int ClampRow(int desired, int available) => Mathf.Clamp(desired, 0, available > 0 ? available - 1 : 0);

    /// <summary>
    /// Depth retains original semantics: returns number of columns in pattern (including -1 placeholders)
    /// so evaluators relying on length continue to behave consistently.
    /// </summary>
    public int Depth(int columns, int rows)
    {
        var idx = GenerateIndexes(columns, rows);
        return idx?.Length ?? 0; // legacy behavior (do not count only valid entries)
    }
}
