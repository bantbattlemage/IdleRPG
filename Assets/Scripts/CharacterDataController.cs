using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharacterDataController : MonoBehaviour, IDataPersistence
{
	public SerializableDictionary<string, CharacterData> LocalData;

	public static CharacterDataController Instance;

	private void Awake()
	{
		if(Instance == null)
		{
			Instance = this;
		}
		else
		{
			throw new System.Exception("more than one CharacterDataController");
		}

		LocalData = new SerializableDictionary<string, CharacterData>();
	}

	public void LoadData(GameData persistantData)
	{
		LocalData = persistantData.CurrentCharacterData;

		foreach (string key in persistantData.CurrentCharacterData.Keys)
		{
			Debug.Log(key);
		}

		Debug.Log("loaded character data");
	}

	public void SaveData(GameData persistantData)
	{
		if(LocalData == null)
		{
			return;
		}

		persistantData.CurrentCharacterData = LocalData;

		foreach(string key in persistantData.CurrentCharacterData.Keys)
		{
			Debug.Log(key);
		}

		Debug.Log("saved character data");
	}

	public void CreateNewCharacter(string name)
	{
		DataPersistenceManager.Instance.LoadGame();

		if (LocalData.ContainsKey(name))
		{
			Debug.LogWarning(name + " already exists");
			return;
		}

		CharacterData characterData = new CharacterData();
		characterData.Name = name;
		characterData.MaxHealthPoints = 100;

		LocalData.Add(name, characterData);
		DataPersistenceManager.Instance.SaveGame();

		Debug.Log("added new character");
	}
}