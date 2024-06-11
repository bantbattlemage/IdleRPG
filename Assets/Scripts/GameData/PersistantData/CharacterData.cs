using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static EnumDefinitions;

[Serializable]
public class CharacterData
{
	public string Name;
	public GameClassEnum CharacterClass;
	public int Level;
	public int MaxHealthPoints;
	public int CurrentHealthPoints;
}
