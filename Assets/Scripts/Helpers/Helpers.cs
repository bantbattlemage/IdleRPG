using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine;

public static class Helpers
{
	public static T[] CombineColumnsToGrid<T>(List<T[]> columns)
	{
		if (columns == null || columns.Count == 0)
			return Array.Empty<T>();

		int colCount = columns.Count;
		int rowCount = columns.Max(col => (col != null) ? col.Length : 0);

		// total cells in the rectangular grid
		T[] result = new T[rowCount * colCount];

		for (int r = 0; r < rowCount; r++)
		{
			for (int c = 0; c < colCount; c++)
			{
				// bottom-left origin:
				// row 0 = bottom row
				int srcRow = r;
				int dstIndex = r * colCount + c;

				if (columns[c] != null && srcRow < columns[c].Length)
					result[dstIndex] = columns[c][srcRow];
				else
					result[dstIndex] = default;
			}
		}

		return result;
	}
}
