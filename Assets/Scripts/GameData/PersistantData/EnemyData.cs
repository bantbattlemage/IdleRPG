using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class EnemyData : GameEntityData
{
	public EnemyDataObject BaseEnemyDefinition;

	public EnemyData(in EnemyDataObject baseEnemyDefinition, int entityId, int questId)
	{
		InitializeBasicValues();

		BaseEnemyDefinition = baseEnemyDefinition;
		ActiveQuestId = questId;
		EntityId = entityId;
		Name = BaseEnemyDefinition.EnemyName;
	}
}
