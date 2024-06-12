using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static EnumDefinitions;

[CreateAssetMenu]
public class EnemyDataObject : ScriptableObject
{
	public string EnemyName;
	public int BaseHealth;
	public int BaseAttack;
}