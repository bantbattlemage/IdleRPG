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
    /// Generate concrete symbol indexes for a grid with given columns (reels) and rows (symbols per reel).
    /// Grid indexing is column-major with bottom-left origin: index = row * columns + column.
    /// </summary>
    public int[] GenerateIndexes(int columns, int rows)
    {
        if (columns <= 0 || rows <= 0) return System.Array.Empty<int>();

        switch (pattern)
        {
            case PatternType.StraightAcross:
                int r = Mathf.Clamp(rowIndex, 0, rows - 1);
                var straight = new int[columns];
                for (int c = 0; c < columns; c++) straight[c] = r * columns + c;
                return straight;

            case PatternType.DiagonalDown:
                // Start near the top then step down to the right
                var diagDown = new int[columns];
                for (int c = 0; c < columns; c++)
                {
                    int row = (rows - 1 - rowOffset) - c;
                    row = Mathf.Clamp(row, 0, rows - 1);
                    diagDown[c] = row * columns + c;
                }
                return diagDown;

            case PatternType.DiagonalUp:
                // Start near the bottom then step up to the right
                var diagUp = new int[columns];
                for (int c = 0; c < columns; c++)
                {
                    int rowUp = rowOffset + c;
                    rowUp = Mathf.Clamp(rowUp, 0, rows - 1);
                    diagUp[c] = rowUp * columns + c;
                }
                return diagUp;

            case PatternType.Custom:
                if (customRowOffsets != null && customRowOffsets.Length == columns)
                {
                    var custom = new int[columns];
                    for (int c = 0; c < columns; c++)
                    {
                        int rc = Mathf.Clamp(customRowOffsets[c], 0, rows - 1);
                        custom[c] = rc * columns + c;
                    }
                    return custom;
                }

                // Fallback to middle row across
                int mid = rows / 2;
                var fallback = new int[columns];
                for (int c2 = 0; c2 < columns; c2++) fallback[c2] = mid * columns + c2;
                return fallback;

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
