using System;
using UnityEngine;

public class GlobalEventManager : Singleton<GlobalEventManager>
{
	private EventManager eventManager;

	protected override void Awake()
	{
		base.Awake();
		eventManager = new EventManager();
	}

	// Enum-based API only
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

	public bool HasSubscribers(Enum baseEnum, string suffix = null) => eventManager.HasSubscribers(baseEnum, suffix);
}
