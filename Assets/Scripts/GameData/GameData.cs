using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class GameData
{
    public SerializableDictionary<int, CharacterData> CurrentCharacterData;
    public SerializableDictionary<int, QuestData> CurrentQuestData;
    public SerializableDictionary<int, EnemyData> CurrentEnemyData;

    public long lastUpdated;

	//  NEW BELOW

	public SerializableDictionary<int, PlayerData> CurrentPlayerData;
}