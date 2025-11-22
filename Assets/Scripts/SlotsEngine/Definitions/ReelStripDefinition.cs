using System;
using UnityEngine;

public class ReelStripDefinition : BaseDefinition<ReelStripData>
{
	[SerializeField] private int stripSize;
	public int StripSize => stripSize;
	
	// Symbols array is a generic list of symbols associated with this strip (used as default pool)
	[SerializeField] private SymbolDefinition[] symbols;
	public SymbolDefinition[] Symbols => symbols;

	// Per-slot inventory: array of slot entries. If an entry is null it represents a random placeholder.
	// Length is expected to match StripSize but is not strictly required.
	[SerializeField] private SymbolDefinition[] slotSymbols;
	public SymbolDefinition[] SlotSymbols => slotSymbols;

	// Optional per-strip random pool: if provided, placeholders will draw from this pool. Otherwise
	// fall back to `symbols` field (or global pool if desired elsewhere).
	[SerializeField] private SymbolDefinition[] randomPool = null;
	public SymbolDefinition[] RandomPool => randomPool;

	// Optional parallel counts for entries in RandomPool. When set, picks from placeholders will
	// be depleted as existing selections increase. Use -1 or missing entries to indicate unlimited.
	[SerializeField] private int[] randomPoolCounts = null;
	public int[] RandomPoolCounts => randomPoolCounts;

	public override ReelStripData CreateInstance()
	{
		return new ReelStripData(this, stripSize, symbols);
	}
}