using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TextCore.Text;

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

	public void AssignCharacter(string character) 
	{
		ActiveCharacters = new string[1];
		ActiveCharacters[0] = character;

		DataPersistenceManager.Instance.LoadGame();

		CharacterDataManager.Instance.LocalData[character].ActiveQuestId = AccessorId;

		DataPersistenceManager.Instance.SaveGame();
	}

	public void AssignCharacters(string[] characters) 
	{
		DataPersistenceManager.Instance.LoadGame();

		ActiveCharacters = new string[characters.Length];
		for(int i = 0; i < characters.Length; i++)
		{
			ActiveCharacters[i] = characters[i];
			CharacterDataManager.Instance.LocalData[characters[i]].ActiveQuestId = AccessorId;
		}

		DataPersistenceManager.Instance.SaveGame();
	}
}
