using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class EnemyDataManager : DataManager<EnemyDataManager, EnemyData>
{
	public Dictionary<string, EnemyDataObject> EnemyDataObjects;

	void Start()
	{
		EnemyDataObjects = new Dictionary<string, EnemyDataObject>();

		var loadedEnemyDataObjects = Resources.LoadAll<EnemyDataObject>("GameData/Enemies");
		foreach (EnemyDataObject obj in loadedEnemyDataObjects)
		{
			EnemyDataObjects.Add(obj.name, obj);
		}
	}

	public override void LoadData(GameData persistantData)
	{
		LocalData = persistantData.CurrentEnemyData;
	}

	public override void SaveData(GameData persistantData)
	{
		persistantData.CurrentEnemyData = LocalData;
	}

	public EnemyData AddNewEnemy(EnemyDataObject enemyDefinition, int questId)
	{
		//	assign a random int id, and don't use 0 since it is default value
		int newEnemyId = GenerateUniqueAccessorId(LocalData.Keys.ToList());
		EnemyData newEnemy = new EnemyData(enemyDefinition, newEnemyId, questId);
		LocalData.Add(newEnemyId, newEnemy);

		return newEnemy;
	}
}
