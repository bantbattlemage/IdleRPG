using System;
using System.Collections.Generic;
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

	// legacy string-based API
	public void RegisterEvent(string eventName, Action<object> action)
	{
		eventManager.RegisterEvent(eventName, action);
	}

	public void UnregisterEvent(string eventName, Action<object> action)
	{
		eventManager.UnregisterEvent(eventName, action);
	}

	public void BroadcastEvent(string eventName, object value = null)
	{
		eventManager.BroadcastEvent(eventName, value);
	}

	// enum convenience passthroughs
	public void RegisterEvent(Enum baseEnum, string suffix, Action<object> action)
	{
		eventManager.RegisterEvent(baseEnum, suffix, action);
	}

	public void RegisterEvent(Enum baseEnum, Action<object> action)
	{
		eventManager.RegisterEvent(baseEnum, action);
	}

	public void UnregisterEvent(Enum baseEnum, string suffix, Action<object> action)
	{
		eventManager.UnregisterEvent(baseEnum, suffix, action);
	}

	public void UnregisterEvent(Enum baseEnum, Action<object> action)
	{
		eventManager.UnregisterEvent(baseEnum, action);
	}

	public void BroadcastEvent(Enum baseEnum, string suffix, object value = null)
	{
		eventManager.BroadcastEvent(baseEnum, suffix, value);
	}

	public void BroadcastEvent(Enum baseEnum, object value = null)
	{
		eventManager.BroadcastEvent(baseEnum, value);
	}

	public bool HasSubscribers(string eventName) => eventManager.HasSubscribers(eventName);
	public bool HasSubscribers(Enum baseEnum, string suffix = null) => eventManager.HasSubscribers(baseEnum, suffix);
}
