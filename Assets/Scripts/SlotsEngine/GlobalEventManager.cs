using System.Collections.Generic;
using System;
using Unity.VisualScripting;
using UnityEditor.PackageManager;
using UnityEngine;

public class GlobalEventManager : Singleton<GlobalEventManager>
{
	private EventManager eventManager;

	void Awake()
	{
		eventManager = new EventManager();
	}

	public void RegisterEvent(string eventName, Action<object> action)
	{
		eventManager.RegisterEvent(eventName, action);
	}

	public void UnregisterEvent(string eventName, Action<object> action)
	{
		eventManager.RegisterEvent(eventName, action);
	}

	public void BroadcastEvent(string eventName, object value = null)
	{
		eventManager.BroadcastEvent(eventName, value);
	}
}
