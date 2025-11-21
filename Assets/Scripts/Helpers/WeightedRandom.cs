using System;
using System.Collections.Generic;

public static class WeightedRandom
{
	// use centralized RNG manager
	public static T Pick<T>(IReadOnlyList<(T item, float weight)> entries)
	{
		float total = 0f;
		for (int i = 0; i < entries.Count; i++)
			total += entries[i].weight;

		if (total <= 0f) return entries.Count > 0 ? entries[0].item : default;

		float r = (float)(RNGManager.NextDouble() * total);

		for (int i = 0; i < entries.Count; i++)
		{
			r -= entries[i].weight;
			if (r <= 0f)
				return entries[i].item;
		}

		return entries[^1].item;
	}
}