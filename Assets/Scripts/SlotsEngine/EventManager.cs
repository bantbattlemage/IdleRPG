using System;
using System.Collections.Generic;
using UnityEngine;

public class EventManager
{
	// string-based channels
	private Dictionary<string, List<Action<object>>> events = new Dictionary<string, List<Action<object>>>();

	// -------------------- Legacy string-based API --------------------
	public void RegisterEvent(string eventName, Action<object> action)
	{
		if (string.IsNullOrEmpty(eventName)) throw new ArgumentNullException(nameof(eventName));
		if (action == null) throw new ArgumentNullException(nameof(action));

		if (events.TryGetValue(eventName, out List<Action<object>> currentRegisteredEvents))
		{
			currentRegisteredEvents.Add(action);
		}
		else
		{
			List<Action<object>> newEvent = new List<Action<object>>() { action };
			events.Add(eventName, newEvent);
		}
	}

	public void UnregisterEvent(string eventName, Action<object> action)
	{
		if (string.IsNullOrEmpty(eventName)) throw new ArgumentNullException(nameof(eventName));
		if (action == null) throw new ArgumentNullException(nameof(action));

		if (events.TryGetValue(eventName, out List<Action<object>> currentRegisteredEvents))
		{
			currentRegisteredEvents.Remove(action);
			if (currentRegisteredEvents.Count == 0)
			{
				events.Remove(eventName);
			}
		}
		else
		{
			Debug.LogWarning($"tried to unregister {eventName} but not registered!");
		}
	}

	public void BroadcastEvent(string eventName, object value = null)
	{
		if (string.IsNullOrEmpty(eventName)) throw new ArgumentNullException(nameof(eventName));

		if (events.TryGetValue(eventName, out List<Action<object>> currentRegisteredEvents))
		{
			// iterate over a copy to avoid collection modified issues
			var copy = currentRegisteredEvents.ToArray();
			foreach (Action<object> e in copy)
			{
				if (e == null) continue;
				try
				{
					e.Invoke(value);
				}
				catch (Exception ex)
				{
					Debug.LogException(ex);
				}
			}
		}
		else
		{
			Debug.Log($"{eventName} broadcast with no listeners.");
		}
	}

	public bool HasSubscribers(string eventName)
	{
		if (string.IsNullOrEmpty(eventName)) return false;
		return events.TryGetValue(eventName, out var list) && list.Count > 0;
	}

	// -------------------- Enum convenience overloads --------------------
	public void RegisterEvent(Enum baseEnum, string suffix, Action<object> action)
	{
		RegisterEvent(EventKey.Compose(baseEnum, suffix), action);
	}

	public void RegisterEvent(Enum baseEnum, Action<object> action)
	{
		RegisterEvent(baseEnum, null, action);
	}

	public void UnregisterEvent(Enum baseEnum, string suffix, Action<object> action)
	{
		UnregisterEvent(EventKey.Compose(baseEnum, suffix), action);
	}

	public void UnregisterEvent(Enum baseEnum, Action<object> action)
	{
		UnregisterEvent(baseEnum, null, action);
	}

	public void BroadcastEvent(Enum baseEnum, string suffix, object value = null)
	{
		BroadcastEvent(EventKey.Compose(baseEnum, suffix), value);
	}

	public void BroadcastEvent(Enum baseEnum, object value = null)
	{
		BroadcastEvent(baseEnum, null, value);
	}

	public bool HasSubscribers(Enum baseEnum, string suffix = null)
	{
		return HasSubscribers(EventKey.Compose(baseEnum, suffix));
	}
}
