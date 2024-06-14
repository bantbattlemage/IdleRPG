using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
	public static T Instance { get { if (instance == null) instance = (T)FindObjectOfType(typeof(T)); return instance; } private set { instance = value; } }
	private static T instance;

}