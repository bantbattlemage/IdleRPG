using System.Collections.Generic;
using System;
using UnityEngine;

public static class Helpers
{
	public static T[] CombineColumnsToGrid<T>(List<T[]> columns)
	{
		if (columns == null || columns.Count == 0)
			return Array.Empty<T>();

		int colCount = columns.Count;
		int rowCount = 0;
		// Find max row length without LINQ allocations
		for (int i = 0; i < colCount; i++)
		{
			var col = columns[i];
			int len = col != null ? col.Length : 0;
			if (len > rowCount) rowCount = len;
		}

		// total cells in the rectangular grid
		T[] result = new T[rowCount * colCount];

		for (int r = 0; r < rowCount; r++)
		{
			int rowOffset = r * colCount;
			for (int c = 0; c < colCount; c++)
			{
				int dstIndex = rowOffset + c;
				var srcCol = columns[c];
				if (srcCol != null && r < srcCol.Length)
				{
					result[dstIndex] = srcCol[r];
				}
				// else leave default(T)
			}
		}

		return result;
	}
}
