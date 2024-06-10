using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class CharacterData : IDataPersistence
{
	public string Name;
	public int MaxHealthPoints;

	public void LoadData(GameData data)
	{
		MaxHealthPoints = data.CurrentCharacterData[Name].MaxHealthPoints;
	}

	public void SaveData(GameData data)
	{
		data.CurrentCharacterData[Name].MaxHealthPoints = MaxHealthPoints;
	}
}
