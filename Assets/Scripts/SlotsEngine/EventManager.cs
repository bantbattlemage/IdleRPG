using System;
using System.Collections.Generic;
using UnityEngine;

public class EventManager : Singleton<EventManager>
{
	private Dictionary<string, List<Action<object>>> events = new Dictionary<string, List<Action<object>>>();

	void Start()
	{
		events = new Dictionary<string, List<Action<object>>>();
	}

	public void RegisterEvent(string eventName, Action<object> action)
	{
		if(events.TryGetValue(eventName, out List<Action<object>> currentRegisteredEvents))
		{
			currentRegisteredEvents.Add(action);
		}
		else
		{
			List<Action<object>> newEvent = new List<Action<object>>() { action };
			events.Add(eventName, newEvent);
		}
	}

	public void BroadcastEvent(string eventName, object value)
	{
		if (events.TryGetValue(eventName, out List<Action<object>> currentRegisteredEvents))
		{
			foreach (Action<object> e in currentRegisteredEvents)
			{
				e.Invoke(value);
			}
		}
		else
		{
			throw new System.Exception($"Tried to broadcast event {eventName} but event is not registered!");
		}
	}
}
