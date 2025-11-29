using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Base class for persistence-backed data managers.
/// Provides a `SerializableDictionary` keyed by unique int AccessorId, lifecycle registration with
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
    /// Also registers existing keys with the GlobalAccessorIdProvider so the global counter advances past them.
    /// </summary>
    public virtual void LoadData(GameData persistantData)
    {
        // default implementation: attempt to load corresponding dictionary if present, otherwise ensure LocalData is initialized
        if (LocalData == null)
        {
            LocalData = new SerializableDictionary<int, D>();
        }

        // If game data is present, register any existing keys so global id provider doesn't reuse them
        if (persistantData != null)
        {
            // Attempt to initialize provider from persisted snapshot (GameData.LastAssignedAccessorId)
            GlobalAccessorIdProvider.InitializeFromPersisted(persistantData.LastAssignedAccessorId);

            if (LocalData != null)
            {
                foreach (var key in LocalData.Keys)
                {
                    GlobalAccessorIdProvider.RegisterExistingId(key);
                }
            }
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

        // Persist the global last assigned id into GameData so the provider can resume across sessions.
        if (persistantData != null)
        {
            persistantData.LastAssignedAccessorId = GlobalAccessorIdProvider.SnapshotLastAssigned();
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

        // If the incoming data already has a positive AccessorId:
        // - if the id is already present in LocalData, assume it's already registered and do nothing
        //   (prevents double-registration/duplication when managers are initialized multiple times)
        // - if the id is not present, adopt that id to preserve persisted identity
        if (newData != null && newData.AccessorId > 0)
        {
            if (LocalData.ContainsKey(newData.AccessorId))
            {
                // Already registered; do not add again
                return;
            }
            else
            {
                LocalData.Add(newData.AccessorId, newData);
                // Ensure global provider advances past this id
                GlobalAccessorIdProvider.RegisterExistingId(newData.AccessorId);
                return;
            }
        }

        // Use centralized provider to get globally unique id
        int id = GlobalAccessorIdProvider.GetNextId();
        newData.AccessorId = id;
        LocalData.Add(id, newData);
    }

    /// <summary>
    /// Generates a compact unique id by selecting the max existing id and adding one.
    /// Falls back to a random unused id if int.MaxValue is reached.
    /// Deprecated in favor of GlobalAccessorIdProvider but kept for compatibility if needed.
    /// </summary>
    protected int GenerateUniqueAccessorId(IEnumerable<int> existingIds)
    {
        // Snapshot existing ids into a HashSet once to avoid repeated enumeration and O(n^2) Contains checks.
        HashSet<int> idSet = (existingIds != null) ? new HashSet<int>(existingIds) : new HashSet<int>();

        if (idSet.Count == 0)
        {
            // start ids at 1
            return 1;
        }

        int maxId = 0;
        foreach (var id in idSet)
        {
            if (id > maxId) maxId = id;
        }

        // handle potential overflow
        if (maxId == int.MaxValue)
        {
            // fall back to random id generation as a last resort. Use HashSet for fast containment checks.
            int fallbackId = RNGManager.UnseededRange(1, int.MaxValue);
            int safety = 0;
            while (idSet.Contains(fallbackId))
            {
                fallbackId = RNGManager.UnseededRange(1, int.MaxValue);
                if (++safety > 1000)
                {
                    // Extremely unlikely: all sampled ids collided; pick first unused by linear scan starting at 1
                    for (int candidate = 1; candidate > 0; candidate++)
                    {
                        if (!idSet.Contains(candidate))
                        {
                            fallbackId = candidate; break;
                        }
                        if (candidate == int.MaxValue) break;
                    }
                    break;
                }
            }
            return fallbackId;
        }

        // Prefer compact incremental id
        return maxId + 1;
    }
}

public interface IDataManager<D>
{
    D GetData(int accessor);
    List<D> GetAllData();
}

/// <summary>
/// Base class for data entries persisted by DataManagers. Carries a unique AccessorId assigned by the manager.
/// </summary>
public abstract class Data
{
    public int AccessorId;
}