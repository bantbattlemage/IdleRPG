using UnityEngine;

public class GameSymbolPool : Singleton<GameSymbolPool>
{
	[SerializeField] private GameSymbol symbolPrefab;
	[SerializeField] private Transform poolParent; // optional parent for pool root (ignored at runtime)
	[SerializeField] private int initialSize = 20;
	[SerializeField] private int expansionSize = 1;
	[SerializeField] private int maxSize = 0; // 0 == no cap

	private ObjectPool<GameSymbol> symbolPool;
	private bool poolInitialized = false;

	protected override void Awake()
	{
		base.Awake();
		InitializePool();
	}

	private void InitializePool()
	{
		if (poolInitialized) return;

		if (symbolPrefab == null)
		{
			Debug.LogError("GameSymbolPool: symbolPrefab is not assigned.", this);
			return;
		}

		// Always create a dedicated pool root that is owned by this singleton. Do not rely on an inspector-assigned
		// transform because it can easily point to a runtime reel root or scene object that leads to pooling into
		// an active hierarchy and causes symbols to reappear in the wrong place.
		if (poolParent != null)
		{
			Debug.LogWarning("GameSymbolPool: inspector-assigned poolParent will be ignored. A dedicated pool root will be created under the pool singleton.", this);
		}

		var poolGo = new GameObject($"{typeof(GameSymbol).Name}Pool");
		poolGo.transform.SetParent(this.transform, worldPositionStays: false);
		// Keep the pool root inactive so pooled instances reparented here are not visible in the scene
		// This prevents visual artifacts when releasing symbols mid-frame (they become inactive in hierarchy).
		poolGo.SetActive(false);
		poolParent = poolGo.transform;

		int? max = maxSize > 0 ? (int?)maxSize : null;

		// Configure release callback to clean up GameSymbol state before pooling
		symbolPool = new ObjectPool<GameSymbol>(
			symbolPrefab,
			initialSize: initialSize,
			root: poolParent,
			onGet: symbol =>
			{
				// Re-enable rendering when getting from pool
				if (symbol != null)
				{
					var img = symbol.CachedImage;
					if (img != null)
					{
						img.enabled = true;
						// CRITICAL: Re-enable the CanvasRenderer to allow rendering
						var canvasRenderer = img.GetComponent<CanvasRenderer>();
						if (canvasRenderer != null)
						{
							canvasRenderer.SetAlpha(1f);
						}
					}
				}
			},
			onRelease: symbol =>
			{
				// CRITICAL FIX: Disable the CanvasRenderer to COMPLETELY prevent rendering
				// This stops Unity's Canvas batching system from rendering this GameObject AT ALL,
				// even if the sprite property changes. This is the ONLY way to prevent visible
				// sprite swaps when pooled GameObjects are immediately reused.
				var img = symbol.CachedImage;
				if (img != null)
				{
					// Disable Image component first
					img.enabled = false;
					
					// CRITICAL: Set CanvasRenderer alpha to 0 to force it offline
					// This ensures Unity's Canvas batching completely ignores this GameObject
					var canvasRenderer = img.GetComponent<CanvasRenderer>();
					if (canvasRenderer != null)
					{
						canvasRenderer.SetAlpha(0f);
					}
				}
				
				// Move the transform off-screen as additional safety
				var rt = symbol.CachedRect;
				if (rt != null) rt.localPosition = new Vector3(10000f, 10000f, 0f);
				
				// Clear the cached symbol data reference to prevent stale data reads
				try
				{
					// Use reflection to clear the private field since there's no public setter
					var field = typeof(GameSymbol).GetField("currentSymbolData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
					if (field != null)
					{
						field.SetValue(symbol, null);
					}
				}
				catch
				{
					// Best effort - pooled symbols are inactive so stale data won't cause issues
				}
				
				// ensure the symbol is detached from events & tweens are cleared before pooling
				symbol.UnregisterFromEventManager();
				symbol.StopAndClearTweens();
			},
			expansionSize: Mathf.Max(1, expansionSize),
			maxSize: max
		);

		poolInitialized = true;
	}

	/// <summary>
	/// Acquire a pooled GameSymbol and parent it under <paramref name="parent"/>.
	/// Caller should call <see cref="GameSymbol.InitializeSymbol"/> / <see cref="GameSymbol.ApplySymbol"/> as required.
	/// CRITICAL: Caller MUST position symbol offscreen immediately after Get() and before ApplySymbol()
	/// to prevent visible sprite changes during reuse.
	/// </summary>
	public GameSymbol Get(Transform parent = null)
	{
		// Ensure the pool is initialized (handles cases where Get is called before Awake)
		InitializePool();

		if (symbolPool == null)
		{
			return null;
		}

		// Get from pool with inactive poolParent to prevent rendering during activation
		var s = symbolPool.Get(parent: poolParent);

		// Reparent to requested parent after activation (symbol is still invisible via CanvasRenderer alpha=0)
		if (s != null && parent != null)
		{
			s.transform.SetParent(parent, worldPositionStays: false);
		}

		return s;
	}

	/// <summary>
	/// Return a symbol to the pool.
	/// </summary>
	public void Release(GameSymbol symbol)
	{
		if (symbolPool == null) return;
		symbolPool.Release(symbol);
	}

	/// <summary>
	/// Prewarm additional instances into the pool.
	/// </summary>
	public void Prewarm(int count)
	{
		InitializePool();
		symbolPool?.Prewarm(count);
	}

	/// <summary>
	/// Clear/destroy pooled instances.
	/// </summary>
	public void Clear(bool destroy = true)
	{
		symbolPool?.Clear(destroy);
		poolInitialized = false;
	}
}