using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.SceneManagement;

public class DataPersistenceManager : MonoBehaviour
{
	[Header("Debugging")]
	[SerializeField] private bool disableDataPersistence = false;
	[SerializeField] private bool initializeDataIfNull = false;

	[Header("File Storage Config")]
	[SerializeField] private string fileName;
	[SerializeField] private bool useEncryption;

	private GameData gameData;
	private List<IDataPersistence> dataPersistenceObjects;
	FileDataHandler dataHandler;

	private string selectedProfileId = "";
	private readonly string defaultProfileId = "Default";

	public static DataPersistenceManager Instance { get; private set; }

	private void Awake()
	{
		if (Instance != null)
		{
			Debug.Log("Found more than one Data Persistence Manager in the scene. Destroying the newest one.");
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

		LoadGame();
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
		//Debug.Log("echo");

		//dataPersistenceObjects = FindAllDataPersistenceObjects();
		//LoadGame();

		//gameData = new GameData();
	}

	public void DeleteProfileData(string profileId)
	{
		// delete the data for this profile id
		dataHandler.Delete(profileId);
		// initialize the selected profile id
		//InitializeSelectedProfileId();
		// reload the game so that our data matches the newly selected profile id
		//LoadGame();
	}

	private void InitializeSelectedProfileId()
	{
		selectedProfileId = dataHandler.GetMostRecentlyUpdatedProfileId();

		if (string.IsNullOrEmpty(selectedProfileId))
		{
			selectedProfileId = defaultProfileId;
		}

		Debug.Log($"Selected data profile {this.selectedProfileId}");
	}

	public void NewGame(string name = "")
	{
		if (string.IsNullOrEmpty(name))
		{
			name = defaultProfileId;
		}

		gameData = new GameData();
		selectedProfileId = name;

		SaveGame();
	}

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
			Debug.Log("No data was found. A New Game needs to be started before data can be loaded.");
			return;
		}

		dataPersistenceObjects = FindAllDataPersistenceObjects();

		// push the loaded data to all other scripts that need it
		foreach (IDataPersistence dataPersistenceObj in dataPersistenceObjects)
		{
			dataPersistenceObj.LoadData(gameData);
		}
	}

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

		dataPersistenceObjects = FindAllDataPersistenceObjects();

		// pass the data to other scripts so they can update it
		foreach (IDataPersistence dataPersistenceObj in dataPersistenceObjects)
		{
			dataPersistenceObj.SaveData(gameData);
		}

		// timestamp the data so we know when it was last saved
		gameData.lastUpdated = System.DateTime.Now.ToBinary();

		// save that data to a file using the data handler
		dataHandler.Save(gameData, selectedProfileId);
	}

	private List<IDataPersistence> FindAllDataPersistenceObjects()
	{
		IEnumerable<IDataPersistence> dataPersistenceObjects = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None).OfType<IDataPersistence>();

		return new List<IDataPersistence>(dataPersistenceObjects);
	}

	public Dictionary<string, GameData> GetAllProfilesGameData()
	{
		return dataHandler.LoadAllProfiles();
	}

	private void OnApplicationQuit()
	{
		//SaveGame();
	}
}
