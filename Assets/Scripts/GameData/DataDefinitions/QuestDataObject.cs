using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu]
public class QuestDataObject : ScriptableObject
{
	public string QuestName;
	public string QuestDescription;
	public int PartySize;
	public int Floors;
	public EnemyDataObject[] Enemies;
}
