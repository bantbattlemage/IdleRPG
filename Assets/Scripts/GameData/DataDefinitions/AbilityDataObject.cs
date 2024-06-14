using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu]
public class AbilityDataObject : ScriptableObject
{
	public string AbilityName;
	public int BaseDamageLow;
	public int BaseDamageHigh;
	public int ManaCost;
	public float BaseCastTime;
}
