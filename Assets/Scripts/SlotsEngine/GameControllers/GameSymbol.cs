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

	// Cached reference to owning reel (set by GameReel when creating/allocating symbols)
	public GameReel OwnerReel { get; set; }

	// Backwards-compatible setter for owners (useful when other code can't access the property)
	public void SetOwnerReel(GameReel owner)
	{
		OwnerReel = owner;
	}

	private void Awake()
	{
		// Cache common components once
		cachedImage = GetComponent<Image>();
		cachedRect = GetComponent<RectTransform>();
		originalLocalRotation = transform.localRotation;
	}

	public void InitializeSymbol(SymbolData symbol, EventManager slotsEventManager)
	{
		eventManager = slotsEventManager;

		// register to slot events
		eventManager.RegisterEvent(State.Idle, "Exit", OnIdleExit);

		ApplySymbol(symbol);
	}

	public void ApplySymbol(SymbolData symbol)
	{
		currentSymbolData = symbol;

		var img = CachedImage;
		if (symbol == null)
		{
			// Clear visual state when no symbol provided to avoid showing stale sprites
			if (img != null)
			{
				img.enabled = true;
				img.sprite = null;
				img.color = Color.white;
			}
			return;
		}

		// Ensure sprite is obtained from the data object (may trigger runtime resolution)
		var s = symbol.Sprite;
		if (img != null)
		{
			img.enabled = true; // ensure image component is enabled so visible sprites render
			img.sprite = s;
			img.color = Color.white;
		}

		// ensure transform rotation matches original when applying a new symbol
		transform.localRotation = originalLocalRotation;
	}

	private void OnIdleExit(object obj)
	{
		var img = CachedImage;
		if (img != null) img.color = Color.white;
		transform.localRotation = originalLocalRotation;
	}

	/// <summary>
	/// Highlight this symbol for a win: set color immediately and optionally play a shake tween.
	/// This is the preferred API for presentation controllers to call directly instead of broadcasting
	/// individual SymbolWin events to every symbol.
	/// </summary>
	public void HighlightForWin(Color highlight, bool doShake)
	{
		var img = CachedImage;
		if (img != null) img.color = highlight;

		// restore original rotation before any animation
		transform.localRotation = originalLocalRotation;

		// stop any existing tweens
		if (activeTweener != null)
		{
			try { activeTweener.Kill(); } catch { }
			activeTweener = null;
		}

		if (!doShake) return;

		// Start shake tween
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

	public void UnregisterFromEventManager()
	{
		if (eventManager == null) return;

		// Only unregister Idle handler (SymbolWin no longer used)
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

		// Note: Avoid calling DOTween.Kill(this) here — killing by target can be expensive when many symbols
		// and is largely redundant because we already kill known tween handles above.

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