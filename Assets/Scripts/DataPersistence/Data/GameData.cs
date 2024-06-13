using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class GameData
{
    public SerializableDictionary<string, CharacterData> CurrentCharacterData;
    public SerializableDictionary<int, QuestData> CurrentQuestData;
    public SerializableDictionary<int, EnemyData> CurrentEnemyData;

    public long lastUpdated;

    public GameData() 
    {

    }
}
