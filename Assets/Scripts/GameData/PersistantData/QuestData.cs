using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.TextCore.Text;
using Random = UnityEngine.Random;

[Serializable]
public class QuestData : Data
{
	public string QuestDataObjectAccessor;
	public bool Active;
	public string[] ActiveCharacters;

	public QuestData(QuestDataObject questDataObject, int questAccessorId)
	{
		QuestDataObjectAccessor = questDataObject.name;
		Active = false;
		ActiveCharacters = new string[0];
		AccessorId = questAccessorId;
	}

	public QuestDataObject GetBaseQuestData()
	{
		return QuestDataManager.Instance.QuestDataObjects[QuestDataObjectAccessor];
	}
}
