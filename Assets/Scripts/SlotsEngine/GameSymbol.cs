using DG.Tweening;
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

		EventManager.Instance.RegisterEvent("SymbolLanded", OnSymbolLanded);
	}

	private void OnSymbolLanded(object obj)
	{
		GameSymbol symbol = (GameSymbol)obj;

		if (symbol == this)
		{
			transform.DOShakePosition(1, strength: 50);
		}
	}

	private void OnDestroy()
	{
		EventManager.Instance?.UnregisterEvent("SymbolLanded", OnSymbolLanded);
	}
}
