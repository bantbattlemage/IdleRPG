using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class GameSymbol : MonoBehaviour
{
	SymbolDefinition definition;

	public SymbolDefinition Definition => definition;

	private Tweener activeTweener;

	private EventManager eventManager;

	public void InitializeSymbol(SymbolDefinition symbol, EventManager slotsEventManager)
	{
		eventManager = slotsEventManager;

		slotsEventManager.RegisterEvent("SymbolLanded", OnSymbolLanded);
		slotsEventManager.RegisterEvent("SymbolWin", OnSymbolWin);
		slotsEventManager.RegisterEvent("IdleExit", OnIdleExit);

		ApplySymbol(symbol);
	}

	public void ApplySymbol(SymbolDefinition symbol)
	{
		definition = symbol;

		Image r = gameObject.GetComponent<Image>();
		r.sprite = symbol.Sprite;
		r.color = Color.white;
	}

	private void OnIdleExit(object obj)
	{
		GetComponent<Image>().color = Color.white;
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

		GetComponent<Image>().color = Color.green;
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
		eventManager?.UnregisterEvent("SymbolLanded", OnSymbolLanded);
		eventManager?.UnregisterEvent("SymbolWin", OnSymbolWin);
		eventManager?.UnregisterEvent("IdleExit", OnIdleExit);
	}
}
