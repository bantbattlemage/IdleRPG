using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class GameSymbol : MonoBehaviour
{
	SymbolData currentSymbolData;

	public SymbolData CurrentSymbolData => currentSymbolData;

	private Tweener activeTweener;

	private EventManager eventManager;

	private Image cachedImage;

	private void Awake()
	{
		cachedImage = GetComponent<Image>();
	}

	public void InitializeSymbol(SymbolData symbol, EventManager slotsEventManager)
	{
		eventManager = slotsEventManager;

		// register to slot events
		eventManager.RegisterEvent("SymbolLanded", OnSymbolLanded);
		eventManager.RegisterEvent("SymbolWin", OnSymbolWin);
		eventManager.RegisterEvent("IdleExit", OnIdleExit);

		ApplySymbol(symbol);
	}

	public void ApplySymbol(SymbolData symbol)
	{
		currentSymbolData = symbol;

		if (cachedImage == null) cachedImage = GetComponent<Image>();
		cachedImage.sprite = symbol.Sprite;
		cachedImage.color = Color.white;
	}

	private void OnIdleExit(object obj)
	{
		if (cachedImage == null) cachedImage = GetComponent<Image>();
		cachedImage.color = Color.white;
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

		if (cachedImage == null) cachedImage = GetComponent<Image>();
		cachedImage.color = Color.green;
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

	public void UnregisterFromEventManager()
	{
		if (eventManager == null) return;

		eventManager.UnregisterEvent("SymbolLanded", OnSymbolLanded);
		eventManager.UnregisterEvent("SymbolWin", OnSymbolWin);
		eventManager.UnregisterEvent("IdleExit", OnIdleExit);

		eventManager = null;
	}

	public void StopAndClearTweens()
	{
		if (activeTweener != null)
		{
			activeTweener.Kill();
			activeTweener = null;
		}

		// kill DOTween tweens targeting the image/gameObject as well
		if (cachedImage != null) cachedImage.DOKill();
		transform.DOKill();
	}

	private void OnDestroy()
	{
		UnregisterFromEventManager();
		StopAndClearTweens();
	}
}
