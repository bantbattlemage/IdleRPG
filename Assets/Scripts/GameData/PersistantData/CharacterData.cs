using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static EnumDefinitions;

[Serializable]
public class CharacterData : IDataPersistence
{
	public string Name;
	public GameClassEnum CharacterClass;
	public int Level;
	public int MaxHealthPoints;
	public int CurrentHealthPoints;

	public void LoadData(GameData data)
	{
		CharacterClass = data.CurrentCharacterData[Name].CharacterClass;
		Level = data.CurrentCharacterData[Name].Level;
		MaxHealthPoints = data.CurrentCharacterData[Name].MaxHealthPoints;
		CurrentHealthPoints = data.CurrentCharacterData[Name].CurrentHealthPoints;
	}

	public void SaveData(GameData data)
	{
		data.CurrentCharacterData[Name].CharacterClass = CharacterClass;
		data.CurrentCharacterData[Name].Level = Level;
		data.CurrentCharacterData[Name].MaxHealthPoints = MaxHealthPoints;
		data.CurrentCharacterData[Name].Level = CurrentHealthPoints;
	}
}
