using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Simple generic MonoBehaviour-based singleton.
/// - Assigns the first instance found in the scene to `Instance` on first access or Awake.
/// - Logs a warning when duplicates are detected and keeps the first instance.
/// - Clears the instance reference when the owning object is destroyed.
///
/// Notes:
/// - Does not automatically create or persist itself; consumers are responsible for scene placement.
/// </summary>
public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
	private static T instance;
	public static T Instance
	{
		get
		{
			if (instance == null)
			{
				instance = (T)FindFirstObjectByType(typeof(T));
			}
			return instance;
		}
	}

	/// <summary>
	/// Base Awake logic: assigns the singleton instance if not already set.
	/// Derived classes that implement Awake should call <c>base.Awake()</c>.
	/// </summary>
	protected virtual void Awake()
	{
		if (instance == null)
		{
			instance = this as T;
		}
		else if (instance != this)
		{
			Debug.LogWarning($"Multiple instances of singleton {typeof(T).Name} detected. Keeping the first instance.", this);
		}
	}

	/// <summary>
	/// Base OnDestroy logic: clears the instance reference when the owning object is destroyed.
	/// Derived classes that implement OnDestroy should call <c>base.OnDestroy()</c>.
	/// </summary>
	protected virtual void OnDestroy()
	{
		if (instance == this)
		{
			instance = null;
		}
	}

}