using System;
using System.Collections.Generic;
using UnityEngine;

public class ObjectPool<T> where T : MonoBehaviour
{
	private readonly T prefab;
	private readonly Stack<T> pool = new Stack<T>();
	private readonly Transform poolRoot;
	private readonly Action<T> onGet;
	private readonly Action<T> onRelease;

	// How many instances to create when a miss occurs (default: 1)
	private readonly int expansionSize;
	// Optional hard cap for total created instances
	private readonly int? maxSize;

	public int CountInactive => pool.Count;
	public int CountAll { get; private set; }

	public ObjectPool(T prefab, int initialSize = 0, Transform root = null, Action<T> onGet = null, Action<T> onRelease = null, int expansionSize = 1, int? maxSize = null)
	{
		if (prefab == null) throw new ArgumentNullException(nameof(prefab));
		if (expansionSize < 1) throw new ArgumentOutOfRangeException(nameof(expansionSize), "expansionSize must be >= 1");

		this.prefab = prefab;
		this.onGet = onGet;
		this.onRelease = onRelease;
		this.expansionSize = expansionSize;
		this.maxSize = maxSize;

		poolRoot = root ?? new GameObject($"{typeof(T).Name}Pool").transform;
		if (initialSize > 0) Prewarm(initialSize);
	}

	public T Get(Transform parent = null)
	{
		T instance;

		// reuse if possible (skip destroyed entries)
		while (pool.Count > 0)
		{
			instance = pool.Pop();
			if (instance != null)
			{
				PrepareForUse(instance, parent);
				return instance;
			}
			CountAll--;
		}

		// pool empty: attempt to expand the pool first
		ExpandPoolIfNeeded();

		// if expansion produced inactive instances, use one
		if (pool.Count > 0)
		{
			instance = pool.Pop();
			if (instance != null)
			{
				PrepareForUse(instance, parent);
				return instance;
			}
		}

		// fallback: create a single instance when expansion didn't yield any (e.g. maxSize not reached check)
		instance = UnityEngine.Object.Instantiate(prefab, poolRoot);
		CountAll++;
		PrepareForUse(instance, parent);
		return instance;
	}

	private void PrepareForUse(T instance, Transform parent)
	{
		instance.transform.SetParent(parent ?? poolRoot, worldPositionStays: false);
		instance.gameObject.SetActive(true);
		onGet?.Invoke(instance);
	}

	public void Release(T instance)
	{
		if (instance == null) return;
		onRelease?.Invoke(instance);
		instance.gameObject.SetActive(false);
		instance.transform.SetParent(poolRoot, worldPositionStays: false);
		pool.Push(instance);
	}

	public void Prewarm(int count)
	{
		if (count <= 0) return;

		int toCreate = count;
		if (maxSize.HasValue)
		{
			int available = maxSize.Value - CountAll;
			if (available <= 0) return;
			if (available < toCreate) toCreate = available;
		}

		for (int i = 0; i < toCreate; i++)
		{
			var inst = UnityEngine.Object.Instantiate(prefab, poolRoot);
			inst.gameObject.SetActive(false);
			pool.Push(inst);
			CountAll++;
		}
	}

	private void ExpandPoolIfNeeded()
	{
		int toCreate = expansionSize;
		if (maxSize.HasValue)
		{
			int available = maxSize.Value - CountAll;
			if (available <= 0) return;
			if (available < toCreate) toCreate = available;
		}

		for (int i = 0; i < toCreate; i++)
		{
			var inst = UnityEngine.Object.Instantiate(prefab, poolRoot);
			inst.gameObject.SetActive(false);
			pool.Push(inst);
			CountAll++;
		}
	}

	/// <summary>
	/// Clears the pool. Optionally destroy pooled GameObjects.
	/// </summary>
	public void Clear(bool destroy = true)
	{
		while (pool.Count > 0)
		{
			var item = pool.Pop();
			if (item != null && destroy)
			{
				UnityEngine.Object.Destroy(item.gameObject);
			}
		}

		if (destroy && poolRoot != null)
		{
			UnityEngine.Object.Destroy(poolRoot.gameObject);
		}

		CountAll = 0;
	}
}