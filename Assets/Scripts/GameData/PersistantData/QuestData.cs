using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class QuestData : IDataPersistence
{
	public QuestDataObject BaseQuestDefinition;

	public QuestData(QuestDataObject questDataObject)
	{
		BaseQuestDefinition = questDataObject;
	}

	public void LoadData(GameData persistantData)
	{

	}

	public void SaveData(GameData persistantData)
	{

	}
}
