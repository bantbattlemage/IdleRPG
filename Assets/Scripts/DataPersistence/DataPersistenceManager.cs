using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.SceneManagement;
using System.IO;
using System;

	/// <summary>
	/// Central coordinator for save/load of game data to disk, with per-profile support and optional encryption.
	/// Provides registration for scene objects implementing <see cref="IDataPersistence"/> so they can receive Load/Save callbacks.
	/// </summary>
public class DataPersistenceManager : MonoBehaviour
{
	[Header("Debugging")]
	[SerializeField] private bool disableDataPersistence = false;
	[SerializeField] private bool initializeDataIfNull = false;
	[SerializeField] private bool autoSaveOnQuitAndPause = true;
	// Diagnostic toggle: set true in editor when you need verbose load/save diagnostics
	[SerializeField] private bool enableDiagnostics = false;

	[Header("File Storage Config")]
	[SerializeField] private string fileName;
	[SerializeField] private bool useEncryption;

	private GameData gameData;
	private readonly List<IDataPersistence> dataPersistenceObjects = new List<IDataPersistence>();
	FileDataHandler dataHandler;

	private string selectedProfileId = "";
	private readonly string defaultProfileId = "Default";

	public static DataPersistenceManager Instance { get; private set; }

	// Indicates whether a load operation is currently in progress. Other systems can use this
	// to avoid creating or registering new runtime data while persisted data is being restored.
	public bool IsLoading { get; private set; } = false;

	// Debounce coroutine handle for batching save requests
	private Coroutine saveCoroutine;

	private void Awake()
	{
		if (Instance != null)
		{
			Debug.LogWarning("Found more than one Data Persistence Manager in the scene. Destroying the newest one.");
			Destroy(gameObject);
			return;
		}
		Instance = this;
		DontDestroyOnLoad(gameObject);

		if (disableDataPersistence)
		{
			Debug.LogWarning("Data Persistence is currently disabled!");
		}

		dataHandler = new FileDataHandler(Application.persistentDataPath, fileName, useEncryption);

		InitializeSelectedProfileId();
	}

	private void OnEnable()
	{
		SceneManager.sceneLoaded += OnSceneLoaded;
	}

	private void OnDisable()
	{
		SceneManager.sceneLoaded -= OnSceneLoaded;
	}

	public void OnSceneLoaded(Scene scene, LoadSceneMode mode)
	{
		// By default we don't auto-load on scene changes. Controlled LoadGame should be used.
	}

	/// <summary>
	/// Registers an <see cref="IDataPersistence"/> object to participate in save/load.
	/// </summary>
	public void RegisterDataPersistence(IDataPersistence obj)
	{
		if (!dataPersistenceObjects.Contains(obj))
		{
			dataPersistenceObjects.Add(obj);
		}
	}

	/// <summary>
	/// Unregisters an <see cref="IDataPersistence"/> object from save/load.
	/// </summary>
	public void UnregisterDataPersistence(IDataPersistence obj)
	{
		if (dataPersistenceObjects.Contains(obj))
		{
			dataPersistenceObjects.Remove(obj);
		}
	}

	/// <summary>
	/// Irreversibly deletes on-disk data for the given profile id.
	/// </summary>
	public void DeleteProfileData(string profileId)
	{
		profileId = SanitizeProfileId(profileId);
		// delete the data for this profile id
		dataHandler.Delete(profileId);
	}

	private void InitializeSelectedProfileId()
	{
		selectedProfileId = dataHandler.GetMostRecentlyUpdatedProfileId();

		if (string.IsNullOrEmpty(selectedProfileId))
		{
			selectedProfileId = defaultProfileId;
		}

		//Debug.Log($"Selected data profile {this.selectedProfileId}");
	}

	/// <summary>
	/// Starts a new game profile and immediately saves an initial `GameData` file.
	/// </summary>
	public void NewGame(string name = "")
	{
		if (string.IsNullOrEmpty(name))
		{
			name = defaultProfileId;
		}

		selectedProfileId = SanitizeProfileId(name);
		gameData = new GameData();

		// If dev saved a seed in PlayerPrefs, apply it so initial randomness is deterministic for the new profile
		try
		{
			const string devSeedKey = "Dev_RNGSeed";
			if (PlayerPrefs.HasKey(devSeedKey))
			{
				int s = PlayerPrefs.GetInt(devSeedKey);
				RNGManager.SetSeed(s);
				if (enableDiagnostics) Debug.Log($"Applied dev RNG seed {s} for New Game.");
			}
		}
		catch (Exception ex)
		{
			Debug.LogWarning($"Failed to apply saved dev seed: {ex.Message}");
		}

		// perform immediate save for new game
		SaveGame();
	}

	/// <summary>
	/// Loads the current profile's data from disk and pushes it to all registered `IDataPersistence` objects.
	/// Optionally initializes new data when none exists and debugging flag is set.
	/// </summary>
	public void LoadGame()
	{
		// return right away if data persistence is disabled
		if (disableDataPersistence)
		{
			return;
		}

		// load any saved data from a file using the data handler
		gameData = dataHandler.Load(selectedProfileId);

		// start a new game if the data is null and we're configured to initialize data for debugging purposes
		if ((gameData == null && initializeDataIfNull))
		{
			NewGame();
		}

		// if no data can be loaded, don't continue
		if (gameData == null)
		{
			Debug.LogWarning("No data was found. A New Game needs to be started before data can be loaded.");
			return;
		}

		// Initialize global accessor provider from persisted snapshot BEFORE managers load so generated ids won't collide
		GlobalAccessorIdProvider.InitializeFromPersisted(gameData.LastAssignedAccessorId);

		// push the loaded data to all other scripts that need it
		// Load core managers first to populate their LocalData dictionaries. This prevents other
		// objects (e.g., SlotsDataManager) from attempting to register nested data (reels/strips/symbols)
		// before the managers have loaded their persisted entries, which would otherwise produce duplicate instances.
		IsLoading = true;
		try {
			if (enableDiagnostics) Debug.Log("[Diag] Loading SymbolDataManager"); SymbolDataManager.Instance?.LoadData(gameData); if (enableDiagnostics) Debug.Log($"[Diag] SymbolDataManager.LocalCount={(SymbolDataManager.Instance!=null && SymbolDataManager.Instance.GetAllData()!=null?SymbolDataManager.Instance.GetAllData().Count:0)}");
			// Register existing symbol accessors with global provider
			if (SymbolDataManager.Instance != null)
			{
				var all = SymbolDataManager.Instance.GetAllData();
				if (all != null)
				{
					foreach (var d in all) { if (d != null) GlobalAccessorIdProvider.RegisterExistingId(d.AccessorId); }
				}
			}
		} catch (Exception ex) { if (enableDiagnostics) Debug.LogWarning($"[Diag] SymbolDataManager.Load failed: {ex.Message}"); }
		try { if (enableDiagnostics) Debug.Log("[Diag] Loading ReelStripDataManager"); ReelStripDataManager.Instance?.LoadData(gameData); if (enableDiagnostics) Debug.Log($"[Diag] ReelStripDataManager.LocalCount={(ReelStripDataManager.Instance!=null && ReelStripDataManager.Instance.ReadOnlyLocalData!=null?ReelStripDataManager.Instance.ReadOnlyLocalData.Count:0)}");
			if (ReelStripDataManager.Instance != null)
			{
				var all = ReelStripDataManager.Instance.GetAllData();
				if (all != null) { foreach (var d in all) { if (d != null) GlobalAccessorIdProvider.RegisterExistingId(d.AccessorId); } }
			}
		} catch (Exception ex) { if (enableDiagnostics) Debug.LogWarning($"[Diag] ReelStripDataManager.Load failed: {ex.Message}"); }
		try { if (enableDiagnostics) Debug.Log("[Diag] Loading ReelDataManager"); ReelDataManager.Instance?.LoadData(gameData); if (enableDiagnostics) Debug.Log($"[Diag] ReelDataManager.LocalCount={(ReelDataManager.Instance!=null && ReelDataManager.Instance.GetAllData()!=null?ReelDataManager.Instance.GetAllData().Count:0)}");
			if (ReelDataManager.Instance != null)
			{
				var all = ReelDataManager.Instance.GetAllData();
				if (all != null) { foreach (var d in all) { if (d != null) GlobalAccessorIdProvider.RegisterExistingId(d.AccessorId); } }
			}
		} catch (Exception ex) { if (enableDiagnostics) Debug.LogWarning($"[Diag] ReelDataManager.Load failed: {ex.Message}"); }
		try { if (enableDiagnostics) Debug.Log("[Diag] Loading SlotsDataManager"); SlotsDataManager.Instance?.LoadData(gameData); if (enableDiagnostics) Debug.Log($"[Diag] SlotsDataManager.LocalCount={(SlotsDataManager.Instance!=null && SlotsDataManager.Instance.GetAllData()!=null?SlotsDataManager.Instance.GetAllData().Count:0)}");
			if (SlotsDataManager.Instance != null)
			{
				var all = SlotsDataManager.Instance.GetAllData();
				if (all != null) { foreach (var d in all) { if (d != null) GlobalAccessorIdProvider.RegisterExistingId(d.AccessorId); } }
			}
		} catch (Exception ex) { if (enableDiagnostics) Debug.LogWarning($"[Diag] SlotsDataManager.Load failed: {ex.Message}"); }
		try { if (enableDiagnostics) Debug.Log("[Diag] Loading PlayerSkillDataManager"); PlayerSkillDataManager.Instance?.LoadData(gameData); if (enableDiagnostics) Debug.Log($"[Diag] PlayerSkillDataManager.LocalCount={(PlayerSkillDataManager.Instance!=null && PlayerSkillDataManager.Instance.GetAllData()!=null?PlayerSkillDataManager.Instance.GetAllData().Count:0)}");
			if (PlayerSkillDataManager.Instance != null)
			{
				var all = PlayerSkillDataManager.Instance.GetAllData();
				if (all != null) { foreach (var d in all) { if (d != null) GlobalAccessorIdProvider.RegisterExistingId(d.AccessorId); } }
			}
		} catch (Exception ex) { if (enableDiagnostics) Debug.LogWarning($"[Diag] PlayerSkillDataManager.Load failed: {ex.Message}"); }
		try { if (enableDiagnostics) Debug.Log("[Diag] Loading PlayerDataManager"); PlayerDataManager.Instance?.LoadData(gameData); if (enableDiagnostics) Debug.Log($"[Diag] PlayerDataManager.LocalCount={(PlayerDataManager.Instance!=null && PlayerDataManager.Instance.GetAllData()!=null?PlayerDataManager.Instance.GetAllData().Count:0)}");
			if (PlayerDataManager.Instance != null)
			{
				var all = PlayerDataManager.Instance.GetAllData();
				if (all != null) { foreach (var d in all) { if (d != null) GlobalAccessorIdProvider.RegisterExistingId(d.AccessorId); } }
			}
		} catch (Exception ex) { if (enableDiagnostics) Debug.LogWarning($"[Diag] PlayerDataManager.Load failed: {ex.Message}"); }
		

		// Now notify remaining registered persistence objects. Managers above will still be iterated here,
		// but their LoadData implementations are idempotent so it's safe to call again.
		foreach (IDataPersistence dataPersistenceObj in dataPersistenceObjects)
		{
			try
			{
				dataPersistenceObj.LoadData(gameData);
			}
			catch (Exception e)
			{
				Debug.LogError($"Error while loading data into {dataPersistenceObj.GetType().Name}: {e}");
			}
		}
		IsLoading = false;
	}

	/// <summary>
	/// Saves the current in-memory `GameData` to disk after gathering changes from registered `IDataPersistence` objects.
	/// Adds a timestamp and attempts an atomic file write via <see cref="FileDataHandler"/>.
	/// This performs the actual synchronous write; prefer <see cref="RequestSave(float)"/> for debounce/batched saves.
	/// </summary>
	public void SaveGame()
	{
		// return right away if data persistence is disabled
		if (disableDataPersistence)
		{
			return;
		}

		// if we don't have any data to save, log a warning here
		if (this.gameData == null)
		{
			Debug.LogWarning("No data was found. A New Game needs to be started before data can be saved.");
			return;
		}

		// pass the data to other scripts so they can update it
		foreach (IDataPersistence dataPersistenceObj in dataPersistenceObjects)
		{
			try
			{
				dataPersistenceObj.SaveData(gameData);
			}
			catch (Exception e)
			{
				Debug.LogError($"Error while saving data from {dataPersistenceObj.GetType().Name}: {e}");
			}
		}

		// timestamp the data so we know when it was last saved
		gameData.lastUpdated = System.DateTime.Now.ToBinary();

		// save that data to a file using the data handler
		try
		{
			// attempt atomic save via temp file
			string tempPath = Path.Combine(Application.persistentDataPath, "_tmp");
			Directory.CreateDirectory(tempPath);
			// rely on FileDataHandler to perform write/backup
			dataHandler.Save(gameData, selectedProfileId);
		}
		catch (Exception e)
		{
			Debug.LogError($"Failed to save game data: {e}");
		}
	}

	/// <summary>
	/// Request a save operation with an optional debounce delay. Multiple calls within the delay window
	/// will be coalesced into a single save. Use this from code paths that may trigger frequent changes
	/// (e.g., during slot/reel updates) to avoid blocking the main thread repeatedly.
	/// </summary>
	public void RequestSave(float debounceSeconds = 0.25f)
	{
		if (disableDataPersistence) return;
		if (saveCoroutine != null) StopCoroutine(saveCoroutine);
		saveCoroutine = StartCoroutine(DeferredSave(debounceSeconds));
	}

	private IEnumerator DeferredSave(float delay)
	{
		yield return new WaitForSeconds(delay);
		try { SaveGame(); } catch (Exception ex) { Debug.LogError($"Deferred save failed: {ex}"); }
		saveCoroutine = null;
	}

	/// <summary>
	/// Returns all profiles and their associated `GameData` loaded from disk.
	/// </summary>
	public Dictionary<string, GameData> GetAllProfilesGameData()
	{
		return dataHandler.LoadAllProfiles();
	}

	private void OnApplicationQuit()
	{
		if (autoSaveOnQuitAndPause && !disableDataPersistence)
		{
			SaveGame();
		}
	}

	private void OnApplicationPause(bool pause)
	{
		if (pause && autoSaveOnQuitAndPause && !disableDataPersistence)
		{
			SaveGame();
		}
	}

	/// <summary>
	/// Sanitizes a profile id so it is safe to use as a file name (strips invalid characters, trims).
	/// Returns the default profile id if the result would be empty.
	/// </summary>
	private string SanitizeProfileId(string profileId)
	{
		if (string.IsNullOrEmpty(profileId)) return defaultProfileId;

		// remove invalid path chars and trim
		var invalid = Path.GetInvalidFileNameChars();
		var cleaned = string.Concat(profileId.Where(c => !invalid.Contains(c))).Trim();
		if (string.IsNullOrEmpty(cleaned)) return defaultProfileId;
		return cleaned;
	}

}
