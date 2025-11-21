using UnityEngine;
using System.Linq;
using System.Collections.Generic;

public class ReelStripData : Data
{
	[SerializeField] private int stripSize;
	[SerializeField] private string[] symbolDefinitionKeys;
	[SerializeField] private string definitionKey;

	[System.NonSerialized] private SymbolDefinition[] symbolDefinitions;
	[System.NonSerialized] private ReelStripDefinition definition;

	public int StripSize => stripSize;
	public SymbolDefinition[] SymbolDefinitions
	{
		get { EnsureResolved(); return symbolDefinitions; }
	}

	public ReelStripDefinition Definition { get { EnsureResolved(); return definition; } }

	public ReelStripData(ReelStripDefinition def, int size, SymbolDefinition[] syms)
	{
		stripSize = size;
		definition = def;
		symbolDefinitions = syms;
		definitionKey = def != null ? def.name : null;
		if (syms != null) { symbolDefinitionKeys = new string[syms.Length]; for (int i = 0; i < syms.Length; i++) symbolDefinitionKeys[i] = syms[i].name; }
	}

	private void EnsureResolved()
	{
		if (definition == null && !string.IsNullOrEmpty(definitionKey)) definition = DefinitionResolver.Resolve<ReelStripDefinition>(definitionKey);
		if ((symbolDefinitions == null || symbolDefinitions.Length == 0) && symbolDefinitionKeys != null) { symbolDefinitions = new SymbolDefinition[symbolDefinitionKeys.Length]; for (int i = 0; i < symbolDefinitionKeys.Length; i++) symbolDefinitions[i] = DefinitionResolver.Resolve<SymbolDefinition>(symbolDefinitionKeys[i]); }
	}

	public SymbolData GetWeightedSymbol()
	{
		EnsureResolved();
		var weighted = new (SymbolDefinition, float)[symbolDefinitions.Length];
		for (int i = 0; i < symbolDefinitions.Length; i++) weighted[i] = (symbolDefinitions[i], symbolDefinitions[i].Weight / stripSize);
		var def = WeightedRandom.Pick(weighted);
		if (WinlineEvaluator.Instance != null && WinlineEvaluator.Instance.LoggingEnabled && (Application.isEditor || Debug.isDebugBuild)) Debug.Log($"ReelStripData.GetWeightedSymbol(): picked '{def?.SymbolName ?? def?.name ?? "(null)"}' (no existingSelections)");
		return def.CreateInstance();
	}

	public SymbolData GetWeightedSymbol(System.Collections.Generic.List<SymbolData> existingSelections)
	{
		EnsureResolved();
		var candidates = new System.Collections.Generic.List<(SymbolDefinition, float)>();
		bool doLog = WinlineEvaluator.Instance != null && WinlineEvaluator.Instance.LoggingEnabled && (Application.isEditor || Debug.isDebugBuild);
		if (doLog)
		{
			string existingNames = existingSelections == null ? "(null)" : string.Join(",", existingSelections.Select(s => s == null ? "(null)" : s.Name));
			Debug.Log($"ReelStripData.GetWeightedSymbol(existing): symbolDefinitions={symbolDefinitions?.Length ?? 0} existingSelections=[{existingNames}]");
			if (existingSelections != null) { for (int ex = 0; ex < existingSelections.Count; ex++) { var es = existingSelections[ex]; if (es == null) { Debug.Log($" existing[{ex}] = (null)"); continue; } int mg = es.MatchGroupId; string sname = es.Name ?? "(null)"; Debug.Log($" existing[{ex}] Name='{sname}' MatchGroupId={mg} Base={es.BaseValue} MinWin={es.MinWinDepth} IsWild={es.IsWild}"); } }
		}

		if (symbolDefinitions == null || symbolDefinitions.Length == 0) return GetWeightedSymbol();

		for (int i = 0; i < symbolDefinitions.Length; i++)
		{
			var def = symbolDefinitions[i];
			if (def == null) continue;
			int max = def.MaxPerReel;
			int already = 0;
			if (existingSelections != null && existingSelections.Count > 0)
			{
				for (int j = 0; j < existingSelections.Count; j++)
				{
					var s = existingSelections[j];
					if (s == null) continue;
					int sGroup = s.MatchGroupId;
					int defGroup = def.MatchGroupId;
					if (sGroup != 0 && defGroup != 0 && sGroup == defGroup) { already++; continue; }
				}
			}

			if (doLog) Debug.Log($"ReelStripData: candidate='{def.SymbolName ?? def.name}' maxPerReel={def.MaxPerReel} alreadyCount={already} MatchGroupId={def.MatchGroupId}");

			if (max >= 0 && already >= max) { if (doLog) Debug.Log($"ReelStripData: skipping '{def.SymbolName ?? def.name}' because already={already} >= maxPerReel={max}"); continue; }

			candidates.Add((def, def.Weight / stripSize));
		}

		if (candidates.Count == 0)
		{
			if (doLog) { Debug.Log("ReelStripData: all candidates excluded by MaxPerReel - falling back to unconstrained selection."); var list = symbolDefinitions.Select(sd => sd != null ? (sd.SymbolName ?? sd.name) : "(null)"); Debug.Log($"ReelStripData: availableDefinitions=[{string.Join(",", list)}]"); }
			return GetWeightedSymbol();
		}

		var arr = candidates.ToArray();
		SymbolDefinition symbolToReturn = WeightedRandom.Pick(arr);
		if (doLog) Debug.Log($"ReelStripData: selected '{symbolToReturn.SymbolName ?? symbolToReturn.name}' from candidates count={candidates.Count}");
		return symbolToReturn.CreateInstance();
	}
}
