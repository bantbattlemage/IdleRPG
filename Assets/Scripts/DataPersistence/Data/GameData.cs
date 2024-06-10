using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class GameData
{
    public SerializableDictionary<string, CharacterData> CurrentCharacterData;

    public long lastUpdated;

    public GameData() 
    {
        CurrentCharacterData = new SerializableDictionary<string, CharacterData>();
    }
}
