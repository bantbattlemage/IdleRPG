using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class GameSymbol : MonoBehaviour, ISymbol
{
	SymbolDefinition definition;

	public SymbolDefinition Definition => definition;

	private Tweener activeTweener;

	public void ApplySymbol(SymbolDefinition symbol)
	{
		definition = symbol;

		Image r = gameObject.GetComponent<Image>();
		r.sprite = symbol.Sprite;

		EventManager.Instance.RegisterEvent("SymbolLanded", OnSymbolLanded);
		EventManager.Instance.RegisterEvent("SymbolWin", OnSymbolWin);
	}

	private void OnSymbolWin(object obj)
	{
		GameSymbol symbol = (GameSymbol)obj;

		if (symbol != this)
		{
			return;
		}

		if (activeTweener != null && activeTweener.IsPlaying())
		{
			return;
		}

		activeTweener = transform.DOShakeRotation(1f, strength: 25f);
	}

	private void OnSymbolLanded(object obj)
	{
		GameSymbol symbol = (GameSymbol)obj;

		if (symbol != this)
		{
			return;
		}
	}

	private void OnDestroy()
	{
		EventManager.Instance?.UnregisterEvent("SymbolLanded", OnSymbolLanded);
	}
}
