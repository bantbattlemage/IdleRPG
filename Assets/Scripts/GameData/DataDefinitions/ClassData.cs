using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static EnumDefinitions;

[CreateAssetMenu]
public class ClassData : ScriptableObject
{
	public string ClassName;
	public GameClassEnum ClassType; 
}