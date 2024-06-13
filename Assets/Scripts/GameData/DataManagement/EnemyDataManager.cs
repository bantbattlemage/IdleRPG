using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class EnemyDataManager : MonoBehaviour, IDataPersistence
{
	public SerializableDictionary<int, EnemyData> LocalData;
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

	public List<EnemyData> GetAllEnemyData()
	{
		return LocalData.Values.ToList();
	}

	public void LoadData(GameData persistantData)
	{

	}

	public void SaveData(GameData persistantData)
	{

	}
}
