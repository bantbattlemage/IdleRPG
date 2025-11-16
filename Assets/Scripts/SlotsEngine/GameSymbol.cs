using UnityEngine;
using UnityEngine.UI;

public class GameSymbol : MonoBehaviour, ISymbol
{
	SymbolDefinition definition;

	public SymbolDefinition Definition => definition;

	public void ApplySymbol(SymbolDefinition symbol)
	{
		definition = symbol;

		Image r = gameObject.GetComponent<Image>();
		r.sprite = symbol.Sprite;
	}
}
