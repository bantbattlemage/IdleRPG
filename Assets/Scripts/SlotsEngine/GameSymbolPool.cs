using UnityEngine;

public class GameSymbolPool : Singleton<GameSymbolPool>
{
	[SerializeField] private GameSymbol symbolPrefab;
	[SerializeField] private Transform poolParent; // optional parent for pool root
	[SerializeField] private int initialSize = 20;
	[SerializeField] private int expansionSize = 1;
	[SerializeField] private int maxSize = 0; // 0 == no cap

	private ObjectPool<GameSymbol> symbolPool;

	private void Awake()
	{
		if (symbolPrefab == null)
		{
			Debug.LogError("GameSymbolPool: symbolPrefab is not assigned.", this);
			return;
		}

		int? max = maxSize > 0 ? (int?)maxSize : null;

		// Configure release callback to clean up GameSymbol state before pooling
		symbolPool = new ObjectPool<GameSymbol>(
			symbolPrefab,
			initialSize: initialSize,
			root: poolParent,
			onGet: null, // initialization left to caller (ApplySymbol/InitializeSymbol)
			onRelease: symbol =>
			{
				// ensure the symbol is detached from events & tweens are cleared before pooling
				symbol.UnregisterFromEventManager();
				symbol.StopAndClearTweens();
			},
			expansionSize: Mathf.Max(1, expansionSize),
			maxSize: max
		);
	}

	/// <summary>
	/// Acquire a pooled GameSymbol and parent it under <paramref name="parent"/>.
	/// Caller should call <see cref="GameSymbol.InitializeSymbol"/> / <see cref="GameSymbol.ApplySymbol"/> as required.
	/// </summary>
	public GameSymbol Get(Transform parent = null)
	{
		if (symbolPool == null) Awake(); // ensure pool exists (handles cases where Awake ordering differs)
		return symbolPool.Get(parent);
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
		symbolPool?.Prewarm(count);
	}

	/// <summary>
	/// Clear/destroy pooled instances.
	/// </summary>
	public void Clear(bool destroy = true)
	{
		symbolPool?.Clear(destroy);
	}
}