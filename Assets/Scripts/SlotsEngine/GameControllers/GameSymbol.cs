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
	private RectTransform cachedRect;

	private void Awake()
	{
		cachedImage = GetComponent<Image>();
		cachedRect = GetComponent<RectTransform>();
	}

	public void InitializeSymbol(SymbolData symbol, EventManager slotsEventManager)
	{
		eventManager = slotsEventManager;

		// register to slot events
		eventManager.RegisterEvent(SlotsEvent.SymbolLanded, OnSymbolLanded);
		eventManager.RegisterEvent(SlotsEvent.SymbolWin, OnSymbolWin);
		eventManager.RegisterEvent(State.Idle, "Exit", OnIdleExit);

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

		eventManager.UnregisterEvent(SlotsEvent.SymbolLanded, OnSymbolLanded);
		eventManager.UnregisterEvent(SlotsEvent.SymbolWin, OnSymbolWin);
		eventManager.UnregisterEvent(State.Idle, "Exit", OnIdleExit);

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

	// --- Performance helpers added ---
	// Expose cached components for callers that need direct access (avoids GetComponent calls).
	public Image CachedImage
	{
		get
		{
			if (cachedImage == null) cachedImage = GetComponent<Image>();
			return cachedImage;
		}
	}

	public RectTransform CachedRect
	{
		get
		{
			if (cachedRect == null) cachedRect = GetComponent<RectTransform>();
			return cachedRect;
		}
	}

	// Set size (sizeDelta) and Y local position in a single call to avoid multiple GetComponent/struct ops.
	public void SetSizeAndLocalY(float size, float localY)
	{
		var rt = CachedRect;
		if (rt == null) return;
		rt.sizeDelta = new Vector2(size, size);
		var lp = rt.localPosition;
		lp.y = localY;
		rt.localPosition = lp;
	}
}