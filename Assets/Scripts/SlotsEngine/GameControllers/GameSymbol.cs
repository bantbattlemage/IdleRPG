using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using EvaluatorCore;

public class GameSymbol : MonoBehaviour
{
	SymbolData currentSymbolData;

	public SymbolData CurrentSymbolData => currentSymbolData;

	private Tweener activeTweener;

	private EventManager eventManager;

	private Image cachedImage;
	private RectTransform cachedRect;

	// Store original local rotation so tweens can be safely killed/restored
	private Quaternion originalLocalRotation;

	private void Awake()
	{
		cachedImage = GetComponent<Image>();
		cachedRect = GetComponent<RectTransform>();
		originalLocalRotation = transform.localRotation;
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
		if (symbol == null)
		{
			// Clear visual state when no symbol provided to avoid showing stale sprites
			if (cachedImage != null)
			{
				cachedImage.enabled = true;
				cachedImage.sprite = null;
				cachedImage.color = Color.white;
			}
			return;
		}

		// Ensure sprite is obtained from the data object (may trigger runtime resolution)
		var s = symbol.Sprite;
		if (cachedImage != null)
		{
			cachedImage.enabled = true; // ensure image component is enabled so visible sprites render
			cachedImage.sprite = s;
			cachedImage.color = Color.white;
		}

		// ensure transform rotation matches original when applying a new symbol
		transform.localRotation = originalLocalRotation;
	}

	private void OnIdleExit(object obj)
	{
		if (cachedImage == null) cachedImage = GetComponent<Image>();
		cachedImage.color = Color.white;
		transform.localRotation = originalLocalRotation;
	}

	private void OnSymbolWin(object obj)
	{
		GameSymbol symbol = (GameSymbol)obj;

		if (symbol != this)
		{
			return;
		}

		// Diagnostic: log when OnSymbolWin is invoked for this instance
		//try { Debug.Log($"GameSymbol.OnSymbolWin invoked for {name}@{GetInstanceID()} symbolName={(currentSymbolData!=null?currentSymbolData.Name:"(null)")} "); } catch { }

		// Always ensure cachedImage is available
		if (cachedImage == null) cachedImage = GetComponent<Image>();

		// Choose highlight color based on this symbol's win mode.
		Color highlight = Color.green; // default for line wins
		if (currentSymbolData != null)
		{
			switch (currentSymbolData.WinMode)
			{
				case SymbolWinMode.LineMatch:
					highlight = Color.green;
					break;
				case SymbolWinMode.SingleOnReel:
					highlight = Color.yellow;
					break;
				case SymbolWinMode.TotalCount:
					highlight = Color.red;
					break;
				default:
					highlight = Color.green;
					break;
			}
		}

		// Apply color immediately so it's visible even if a tween is restarted
		if (cachedImage != null) cachedImage.color = highlight;

		// Ensure transform rotation is reset before starting a new tween
		transform.localRotation = originalLocalRotation;

		// If an existing tweener is active, kill it so we reliably restart the animation and avoid stale state
		if (activeTweener != null)
		{
			try { activeTweener.Kill(); } catch { }
			activeTweener = null;
		}

		// Start a shake rotation tween and ensure rotation is restored when tween ends or is killed
		activeTweener = transform.DOShakeRotation(1f, strength: 25f);
		if (activeTweener != null)
		{
			try
			{
				activeTweener.SetTarget(this);
				activeTweener.OnComplete(() => { try { transform.localRotation = originalLocalRotation; } catch { } });
				activeTweener.OnKill(() => { try { transform.localRotation = originalLocalRotation; } catch { } });
			}
			catch { }
		}
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
			try { activeTweener.Kill(); } catch { }
			activeTweener = null;
		}

		// kill DOTween tweens targeting the image/gameObject as well
		if (cachedImage != null) cachedImage.DOKill();
		transform.DOKill();

		// kill any tweens that targeted this object (replaces previous TweenPool behavior)
		try { DOTween.Kill(this); } catch { }

		// restore rotation to original so symbols don't stay rotated
		try { transform.localRotation = originalLocalRotation; } catch { }
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