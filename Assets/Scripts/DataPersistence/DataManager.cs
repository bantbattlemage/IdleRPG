using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// Base class for persistence-backed data managers.
/// Provides a `SerializableDictionary` keyed by unique int AccessorIds, lifecycle registration with
/// `DataPersistenceManager`, and helper APIs for add/get/clear operations.
/// 
/// Type Parameters:
/// - T: concrete manager type (for singleton base)
/// - D: data model type (must derive from `Data` and contain an `AccessorId`)
/// </summary>
public abstract class DataManager<T, D> : Singleton<T>, IDataPersistence, IDataManager<D> where T : DataManager<T, D> where D : Data
{
	protected SerializableDictionary<int, D> LocalData;

	protected DataManager()
	{
		LocalData = new SerializableDictionary<int, D>();
	}

	private void OnEnable()
	{
		// Register with the central DataPersistenceManager so it can call Load/Save without expensive finds
		if (DataPersistenceManager.Instance != null)
		{
			DataPersistenceManager.Instance.RegisterDataPersistence(this);
		}
	}

	private void OnDisable()
	{
		if (DataPersistenceManager.Instance != null)
		{
			DataPersistenceManager.Instance.UnregisterDataPersistence(this);
		}
	}

	/// <summary>
	/// Default load implementation ensures LocalData exists. Concrete managers should override to pull from GameData.
	/// </summary>
	public virtual void LoadData(GameData persistantData)
	{
		// default implementation: attempt to load corresponding dictionary if present, otherwise ensure LocalData is initialized
		if (LocalData == null)
		{
			LocalData = new SerializableDictionary<int, D>();
		}
	}

	/// <summary>
	/// Default save implementation ensures LocalData exists. Concrete managers should override to assign into GameData.
	/// </summary>
	public virtual void SaveData(GameData persistantData)
	{
		// default implementation: ensure LocalData is non-null. Concrete managers should override and assign to persistantData.
		if (LocalData == null)
		{
			LocalData = new SerializableDictionary<int, D>();
		}
	}

	/// <summary>
	/// Attempts to get data by accessor id.
	/// </summary>
	public bool TryGetData(int accessor, out D data)
	{
		if (LocalData != null && LocalData.ContainsKey(accessor))
		{
			data = LocalData[accessor];
			return true;
		}

		data = default;
		return false;
	}

	/// <summary>
	/// Gets data by accessor id or throws an informative exception when missing.
	/// </summary>
	public D GetData(int accessor)
	{
		// keep existing behavior but make it safer: throw with clearer message if missing
		if (LocalData == null || !LocalData.ContainsKey(accessor))
		{
			throw new KeyNotFoundException($"Data with accessor id {accessor} was not found in {typeof(T).Name}.");
		}

		return LocalData[accessor];
	}

	/// <summary>
	/// Returns a snapshot list of all stored data entries.
	/// </summary>
	public List<D> GetAllData()
	{
		if (LocalData == null)
		{
			return new List<D>();
		}

		return LocalData.Values.ToList();
	}

	/// <summary>
	/// Clears all local data entries.
	/// </summary>
	public void ClearData()
	{
		LocalData = new SerializableDictionary<int, D>();
	}

	/// <summary>
	/// Adds a new data entry, assigning a unique AccessorId.
	/// </summary>
	public virtual void AddNewData(D newData)
	{
		if (LocalData == null)
		{
			LocalData = new SerializableDictionary<int, D>();
		}

		int id = GenerateUniqueAccessorId(LocalData.Keys);
		newData.AccessorId = id;
		LocalData.Add(id, newData);
	}

	/// <summary>
	/// Generates a compact unique id by selecting the max existing id and adding one.
	/// Falls back to a random unused id if int.MaxValue is reached.
	/// </summary>
	protected int GenerateUniqueAccessorId(IEnumerable<int> existingIds)
	{
		// prefer a compact incremental id: find max existing id and add one. Guard against empty and overflow.
		int maxId = 0;
		if (existingIds != null)
		{
			foreach (var id in existingIds)
			{
				if (id > maxId) maxId = id;
			}
		}

		// handle potential overflow
		if (maxId == int.MaxValue)
		{
			// fall back to random id generation as a last resort
			int fallbackId = Random.Range(1, int.MaxValue);
			while (existingIds != null && existingIds.Contains(fallbackId))
			{
				fallbackId = Random.Range(1, int.MaxValue);
			}
			return fallbackId;
		}

		return maxId + 1;
	}
}

public interface IDataManager<D>
{
	public abstract D GetData(int accessor);
	public abstract List<D> GetAllData();
}

/// <summary>
/// Base class for data entries persisted by DataManagers. Carries a unique AccessorId assigned by the manager.
/// </summary>
public abstract class Data
{
	public int AccessorId;
}