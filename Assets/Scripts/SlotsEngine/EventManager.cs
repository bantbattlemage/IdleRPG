using System;
using System.Collections.Generic;
using UnityEngine;

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lightweight event manager that supports enum-based event channels with optional string suffixes.
/// Internally it maps composed string keys to lists of handlers.
/// </summary>
public class EventManager
{
	// internal string-keyed channels
	private Dictionary<string, List<Action<object>>> events = new Dictionary<string, List<Action<object>>>();

	// Internal helpers that operate on composed string keys
	/// <summary>
	/// Register a handler for a composed event key. Creates the list if missing.
	/// </summary>
	private void RegisterByKey(string eventKey, Action<object> action)
	{
		if (string.IsNullOrEmpty(eventKey)) throw new ArgumentNullException(nameof(eventKey));
		if (action == null) throw new ArgumentNullException(nameof(action));

		if (events.TryGetValue(eventKey, out List<Action<object>> currentRegisteredEvents))
		{
			currentRegisteredEvents.Add(action);
		}
		else
		{
			events.Add(eventKey, new List<Action<object>>() { action });
		}
	}

	/// <summary>
	/// Unregisters a handler from a composed event key. If no handlers remain the key is removed.
	/// </summary>
	private void UnregisterByKey(string eventKey, Action<object> action)
	{
		if (string.IsNullOrEmpty(eventKey)) throw new ArgumentNullException(nameof(eventKey));
		if (action == null) throw new ArgumentNullException(nameof(action));

		if (events.TryGetValue(eventKey, out List<Action<object>> currentRegisteredEvents))
		{
			currentRegisteredEvents.Remove(action);
			if (currentRegisteredEvents.Count == 0)
			{
				events.Remove(eventKey);
			}
		}
		else
		{
			Debug.LogWarning($"tried to unregister {eventKey} but not registered!");
		}
	}

	/// <summary>
	/// Broadcasts a value to all handlers registered under the composed key.
	/// Iterates over a snapshot of the handler list so handlers may safely modify subscriptions.
	/// Exceptions thrown by handlers are caught and logged to avoid breaking the broadcast loop.
	/// </summary>
	private void BroadcastByKey(string eventKey, object value = null)
	{
		if (string.IsNullOrEmpty(eventKey)) throw new ArgumentNullException(nameof(eventKey));

		if (events.TryGetValue(eventKey, out List<Action<object>> currentRegisteredEvents))
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
			// No listeners is not an error; useful for debug tracing
			Debug.Log($"{eventKey} broadcast with no listeners.");
		}
	}

	/// <summary>
	/// Returns whether any handlers are registered for the composed key.
	/// </summary>
	private bool HasSubscribersByKey(string eventKey)
	{
		if (string.IsNullOrEmpty(eventKey)) return false;
		return events.TryGetValue(eventKey, out var list) && list.Count > 0;
	}

	// -------------------- Public enum-based API --------------------
	/// <summary>
	/// Register a handler for an enum-based event channel with an optional suffix.
	/// The enum and suffix are composed into a single string key using EventKey.Compose.
	/// </summary>
	public void RegisterEvent(Enum baseEnum, string suffix, Action<object> action)
	{
		RegisterByKey(EventKey.Compose(baseEnum, suffix), action);
	}

	/// <summary>
	/// Register a handler for an enum-based event channel without a suffix.
	/// </summary>
	public void RegisterEvent(Enum baseEnum, Action<object> action)
	{
		RegisterEvent(baseEnum, null, action);
	}

	/// <summary>
	/// Unregister a previously registered handler.
	/// </summary>
	public void UnregisterEvent(Enum baseEnum, string suffix, Action<object> action)
	{
		UnregisterByKey(EventKey.Compose(baseEnum, suffix), action);
	}

	/// <summary>
	/// Unregister a previously registered handler (no suffix variant).
	/// </summary>
	public void UnregisterEvent(Enum baseEnum, Action<object> action)
	{
		UnregisterEvent(baseEnum, null, action);
	}

	/// <summary>
	/// Broadcast an event value to subscribers of the composed enum/suffix key.
	/// </summary>
	public void BroadcastEvent(Enum baseEnum, string suffix, object value = null)
	{
		BroadcastByKey(EventKey.Compose(baseEnum, suffix), value);
	}

	/// <summary>
	/// Broadcast without suffix.
	/// </summary>
	public void BroadcastEvent(Enum baseEnum, object value = null)
	{
		BroadcastEvent(baseEnum, null, value);
	}

	/// <summary>
	/// Returns true if any subscribers exist for the composed enum/suffix.
	/// </summary>
	public bool HasSubscribers(Enum baseEnum, string suffix = null)
	{
		return HasSubscribersByKey(EventKey.Compose(baseEnum, suffix));
	}
}
