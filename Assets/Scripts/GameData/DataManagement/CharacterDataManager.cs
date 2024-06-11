using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
	}

	public void SaveData(GameData persistantData)
	{
		persistantData.CurrentCharacterData = LocalData;
	}

	public List<CharacterData> GetAllCharacterData()
	{
		return LocalData.Values.ToList();
	}

	public bool CreateNewCharacter(string name, GameClassEnum classType)
	{
		DataPersistenceManager.Instance.LoadGame();

		if (LocalData.ContainsKey(name))
		{
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

		return true;
	}
}