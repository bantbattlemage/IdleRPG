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
    /// This method produces an index array with one entry per column. If a column does not contain
    /// a valid cell for the pattern the corresponding entry will be -1. This makes missing/truncated
    /// columns explicit for the evaluator.
    /// </summary>
    public int[] GenerateIndexes(int columns, int[] rowsPerColumn)
    {
        if (columns <= 0 || rowsPerColumn == null || rowsPerColumn.Length != columns)
            return System.Array.Empty<int>();

        int maxRows = 0;
        for (int i = 0; i < rowsPerColumn.Length; i++) if (rowsPerColumn[i] > maxRows) maxRows = rowsPerColumn[i];
        if (maxRows <= 0) return System.Array.Empty<int>();

        // Initialize result with -1 placeholders (one slot per column)
        var result = new int[columns];
        for (int i = 0; i < columns; i++) result[i] = -1;

        switch (pattern)
        {
            case PatternType.StraightAcross:
            {
                int r = rowIndex;
                // anchor base to column 0 available rows
                int base0 = Mathf.Clamp(r, 0, rowsPerColumn[0] > 0 ? rowsPerColumn[0] - 1 : 0);
                // column 0 always anchored
                if (base0 >= 0 && base0 < rowsPerColumn[0]) result[0] = base0 * columns + 0;

                for (int c = 1; c < columns; c++)
                {
                    if (r >= 0 && r < rowsPerColumn[c])
                    {
                        result[c] = r * columns + c;
                    }
                    else
                    {
                        result[c] = -1; // explicit missing
                    }
                }
                return result;
            }
            case PatternType.DiagonalDown:
            {
                int desiredBase = (maxRows - 1 - rowOffset);
                int base0 = Mathf.Clamp(desiredBase, 0, rowsPerColumn[0] > 0 ? rowsPerColumn[0] - 1 : 0);

                for (int c = 0; c < columns; c++)
                {
                    int row = base0 - c; // step down to the right
                    if (row >= 0 && row < rowsPerColumn[c])
                    {
                        result[c] = row * columns + c;
                    }
                    else
                    {
                        result[c] = -1;
                    }
                }
                return result;
            }
            case PatternType.DiagonalUp:
            {
                int desiredBase = rowOffset;
                int base0 = Mathf.Clamp(desiredBase, 0, rowsPerColumn[0] > 0 ? rowsPerColumn[0] - 1 : 0);

                for (int c = 0; c < columns; c++)
                {
                    int rowUp = base0 + c; // step up to the right
                    if (rowUp >= 0 && rowUp < rowsPerColumn[c])
                    {
                        result[c] = rowUp * columns + c;
                    }
                    else
                    {
                        result[c] = -1;
                    }
                }
                return result;
            }
            case PatternType.Custom:
            {
                if (customRowOffsets != null && customRowOffsets.Length == columns)
                {
                    int base0 = Mathf.Clamp(customRowOffsets[0], 0, rowsPerColumn[0] > 0 ? rowsPerColumn[0] - 1 : 0);
                    // include column0 anchored
                    if (base0 >= 0 && base0 < rowsPerColumn[0]) result[0] = base0 * columns + 0;
                    else result[0] = -1;

                    for (int c = 1; c < columns; c++)
                    {
                        int rc = customRowOffsets[c];
                        if (rc >= 0 && rc < rowsPerColumn[c])
                        {
                            result[c] = rc * columns + c;
                        }
                        else
                        {
                            result[c] = -1;
                        }
                    }
                    return result;
                }

                int mid = maxRows / 2;
                int baseMid = Mathf.Clamp(mid, 0, rowsPerColumn[0] > 0 ? rowsPerColumn[0] - 1 : 0);
                if (baseMid >= 0 && baseMid < rowsPerColumn[0]) result[0] = baseMid * columns + 0;
                else result[0] = -1;

                for (int c2 = 1; c2 < columns; c2++)
                {
                    if (mid < rowsPerColumn[c2])
                        result[c2] = mid * columns + c2;
                    else
                        result[c2] = -1;
                }
                return result;
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
