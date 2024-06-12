using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyDataManager : MonoBehaviour, IDataPersistence
{
	public SerializableDictionary<int, EnemyDataObject> LocalData;
	public Dictionary<string, EnemyDataObject> EnemyDataObjects;

	public static EnemyDataManager Instance { get { if (instance == null) instance = FindObjectOfType<EnemyDataManager>(); return instance; } private set { instance = value; } }
	private static EnemyDataManager instance;

	void Start()
	{
		EnemyDataObjects = new Dictionary<string, EnemyDataObject>();

		var loadedQuests = Resources.LoadAll<EnemyDataObject>("GameData/Enemies");

		foreach (EnemyDataObject obj in loadedQuests)
		{
			EnemyDataObjects.Add(obj.name, obj);
		}
	}

	public void LoadData(GameData persistantData)
	{

	}

	public void SaveData(GameData persistantData)
	{

	}
}
