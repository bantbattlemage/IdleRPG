using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static EnumDefinitions;

public class CharacterDataManager : MonoBehaviour, IDataPersistence
{
	public SerializableDictionary<string, CharacterData> LocalData;

	public static CharacterDataManager Instance { get { if (instance == null) instance = FindObjectOfType<CharacterDataManager>(); return instance; } private set{ instance = value; } }
	private static CharacterDataManager instance;

	private void Awake()
	{
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
		persistantData.CurrentCharacterData = LocalData;

		foreach(string key in persistantData.CurrentCharacterData.Keys)
		{
			Debug.Log(key);
		}

		Debug.Log("saved character data");
	}

	public bool CreateNewCharacter(string name, GameClassEnum classType)
	{
		DataPersistenceManager.Instance.LoadGame();

		if (LocalData.ContainsKey(name))
		{
			Debug.LogWarning(name + " already exists");
			return false;
		}

		CharacterData characterData = new CharacterData();
		characterData.Name = name;
		characterData.MaxHealthPoints = 100;
		characterData.CurrentHealthPoints= 100;
		characterData.CharacterClass = classType;
		characterData.Level = 1;

		LocalData.Add(name, characterData);
		DataPersistenceManager.Instance.SaveGame();

		Debug.Log("added new character");

		return true;
	}
}