using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class QuestData
{
	public QuestDataObject BaseQuestDefinition;
	public int AccessorId;
	public bool Active;
	public string[] ActiveCharacters;

	public QuestData(QuestDataObject questDataObject, int questAccessorId)
	{
		BaseQuestDefinition = questDataObject;
		Active = false;
		ActiveCharacters = new string[0];
		AccessorId = questAccessorId;
	}
}
