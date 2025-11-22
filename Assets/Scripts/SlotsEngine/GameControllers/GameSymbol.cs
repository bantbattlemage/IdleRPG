using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class GameSymbol : MonoBehaviour
{
	SymbolData currentSymbolData;

	public SymbolData CurrentSymbolData 
	{
		get
		{
			return currentSymbolData;
		}
	}

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
		// Unregister from previous eventManager if it exists and is different
		if (eventManager != null && eventManager != slotsEventManager)
		{
			UnregisterFromEventManager();
		}

		eventManager = slotsEventManager;

		// Only register if not already registered (prevents duplicate handlers)
		if (eventManager != null)
		{
			// Unregister first to ensure no duplicates, then register
			eventManager.UnregisterEvent(SlotsEvent.SymbolLanded, OnSymbolLanded);
			eventManager.UnregisterEvent(SlotsEvent.SymbolWin, OnSymbolWin);

			eventManager.RegisterEvent(SlotsEvent.SymbolLanded, OnSymbolLanded);
			eventManager.RegisterEvent(SlotsEvent.SymbolWin, OnSymbolWin);
		}

		// IMPORTANT: Only apply the symbol data, DO NOT call ApplySymbol here
		// ApplySymbol changes the sprite immediately which causes visible flicker
		// when this GameSymbol is already visible on screen during pooling reuse.
		// Instead, just store the reference - sprite will be set by caller when appropriate.
		currentSymbolData = symbol;
	}

	public void ApplySymbol(SymbolData symbol)
	{
		// Allow null to clear the sprite safely
		if (symbol == null)
		{
			currentSymbolData = null;
			if (cachedImage == null) cachedImage = GetComponent<Image>();
			if (cachedImage != null)
			{
				cachedImage.sprite = null;
			}
			return;
		}

		#if UNITY_EDITOR || DEVELOPMENT_BUILD
		// DIAGNOSTIC: Log every sprite change with timestamp and caller info
		string prevSprite = (cachedImage != null && cachedImage.sprite != null) ? cachedImage.sprite.name : "null";
		string newSprite = (symbol != null && symbol.Sprite != null) ? symbol.Sprite.name : "null";
		
		if (prevSprite != newSprite)
		{
			// Get caller information
			var stackTrace = new System.Diagnostics.StackTrace(1, true);
			var frame = stackTrace.GetFrame(0);
			string caller = frame?.GetMethod()?.Name ?? "Unknown";
			string callerClass = frame?.GetMethod()?.DeclaringType?.Name ?? "Unknown";
			
			UnityEngine.Debug.Log($"[ApplySymbol {Time.frameCount}] GameObject={gameObject.name}, " +
				$"PrevSprite={prevSprite}, NewSprite={newSprite}, " +
				$"SymbolName={symbol?.Name ?? "null"}, " +
				$"Caller={callerClass}.{caller}, " +
				$"Parent={transform.parent?.name ?? "null"}");
		}
		#endif
		
		currentSymbolData = symbol;

		if (cachedImage == null) cachedImage = GetComponent<Image>();
		if (cachedImage != null)
		{
			cachedImage.sprite = symbol.Sprite;
		}
		
		// CRITICAL: Do NOT set color here - it triggers Canvas rebuild that makes symbol visible
		// Color will be set by caller after positioning (GameReel sets color after SetSizeAndLocalY)
		// cachedImage.color = Color.white;
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

		cachedImage.color = highlight;
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
	// CRITICAL: Always resets X to 0 to ensure symbols moved offscreen during pooling are brought back.
	// Also resets color to white AFTER positioning to prevent flicker.
	public void SetSizeAndLocalY(float size, float localY)
	{
		var rt = CachedRect;
		if (rt == null) return;
		rt.sizeDelta = new Vector2(size, size);
		// CRITICAL: Reset X to 0 when positioning - symbols are moved offscreen (X=10000) during pooling
		rt.localPosition = new Vector3(0f, localY, 0f);
		
		// NOW that symbol is correctly positioned, reset color to white
		// This ensures the symbol is visible only AFTER it's in the correct location
		var img = CachedImage;
		if (img != null)
		{
			img.color = Color.white;
		}
	}
}