using UnityEngine;

public class ReelStripDefinition : BaseDefinition<ReelStripData>
{
	[SerializeField] private int stripSize;
	public int StripSize => stripSize;
	
	[SerializeField] private SymbolDefinition[] symbols;
	public SymbolDefinition[] Symbols => symbols;

	public override ReelStripData CreateInstance()
	{
		return new ReelStripData(this, stripSize, symbols);
	}
}