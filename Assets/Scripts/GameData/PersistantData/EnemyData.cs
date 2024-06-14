using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class EnemyData : GameEntityData
{
	public string EnemyDataObjectAccessor;

	public EnemyData(EnemyDataObject baseEnemyDefinition, int entityId, int questId)
	{
		InitializeBasicValues();

		EnemyDataObjectAccessor = baseEnemyDefinition.name;
		ActiveQuestId = questId;
		EntityId = entityId;
		Name = baseEnemyDefinition.EnemyName;
	}
}
