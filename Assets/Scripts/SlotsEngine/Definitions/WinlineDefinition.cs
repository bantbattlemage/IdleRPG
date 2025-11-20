using UnityEngine;

[CreateAssetMenu(fileName = "Data", menuName = "ScriptableObjects/WinlineDefinition")]
public class WinlineDefinition : ScriptableObject
{
    public enum PatternType
    {
        StraightAcross,
        DiagonalDown,
        DiagonalUp,
        Custom
    }

    [SerializeField] private PatternType pattern = PatternType.StraightAcross;
    [SerializeField] private int winMultiplier = 1;

    // For StraightAcross: rowIndex (0 = bottom row)
    // For Diagonal variants: rowOffset shifts the diagonal start
    // For Custom: customRowOffsets length should match columns and specify row for each column
    [HideInInspector][SerializeField] private int rowIndex = 0;
    [SerializeField] private int rowOffset = 0;
    [SerializeField] private int[] customRowOffsets;

    public PatternType Pattern => pattern;
    public int WinMultiplier => winMultiplier;
    public int RowIndex => rowIndex;
    public int RowOffset => rowOffset;
    public int[] CustomRowOffsets => customRowOffsets;

    /// <summary>
    /// Legacy signature kept for compatibility: generates indexes using a uniform row count across columns.
    /// </summary>
    public int[] GenerateIndexes(int columns, int rows)
    {
        // Delegate to per-column overload using uniform counts
        int[] perCol = new int[columns];
        for (int i = 0; i < columns; i++) perCol[i] = rows;
        return GenerateIndexes(columns, perCol);
    }

    /// <summary>
    /// Generate concrete symbol indexes for a grid with given columns (reels) and per-column rows.
    /// Grid indexing is column-major with bottom-left origin: index = row * columns + column.
    /// If a given column does not have the requested row, the resulting grid cell will be null when
    /// the grid array is constructed (Helpers.CombineColumnsToGrid uses Max rows). This method will
    /// still return the index into the full rectangular grid (using maxRows) so callers can inspect
    /// the produced cells.
    /// </summary>
    public int[] GenerateIndexes(int columns, int[] rowsPerColumn)
    {
        if (columns <= 0 || rowsPerColumn == null || rowsPerColumn.Length != columns)
            return System.Array.Empty<int>();

        int maxRows = 0;
        for (int i = 0; i < rowsPerColumn.Length; i++) if (rowsPerColumn[i] > maxRows) maxRows = rowsPerColumn[i];
        if (maxRows <= 0) return System.Array.Empty<int>();

        switch (pattern)
        {
            case PatternType.StraightAcross:
            {
                int r = Mathf.Clamp(rowIndex, 0, maxRows - 1);
                var straight = new int[columns];
                for (int c = 0; c < columns; c++) straight[c] = r * columns + c;
                return straight;
            }
            case PatternType.DiagonalDown:
            {
                var diagDown = new int[columns];
                for (int c = 0; c < columns; c++)
                {
                    int row = (maxRows - 1 - rowOffset) - c;
                    row = Mathf.Clamp(row, 0, maxRows - 1);
                    diagDown[c] = row * columns + c;
                }
                return diagDown;
            }
            case PatternType.DiagonalUp:
            {
                var diagUp = new int[columns];
                for (int c = 0; c < columns; c++)
                {
                    int rowUp = rowOffset + c;
                    rowUp = Mathf.Clamp(rowUp, 0, maxRows - 1);
                    diagUp[c] = rowUp * columns + c;
                }
                return diagUp;
            }
            case PatternType.Custom:
            {
                if (customRowOffsets != null && customRowOffsets.Length == columns)
                {
                    var custom = new int[columns];
                    for (int c = 0; c < columns; c++)
                    {
                        int rc = Mathf.Clamp(customRowOffsets[c], 0, maxRows - 1);
                        custom[c] = rc * columns + c;
                    }
                    return custom;
                }

                int mid = maxRows / 2;
                var fallback = new int[columns];
                for (int c2 = 0; c2 < columns; c2++) fallback[c2] = mid * columns + c2;
                return fallback;
            }
            default:
                return System.Array.Empty<int>();
        }
    }

    public int Depth(int columns, int rows)
    {
        var idx = GenerateIndexes(columns, rows);
        return idx?.Length ?? 0;
    }
}
