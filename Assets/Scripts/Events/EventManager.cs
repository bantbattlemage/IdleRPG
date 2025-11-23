using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lightweight event manager that supports enum-based event channels with optional string suffixes.
/// Internally it maps composed string keys to lists of handlers and provides register/unregister/broadcast operations.
///
/// Recommended usage:
/// - Prefer using enum + optional suffix to create distinct event topics.
/// - Use <see cref="GlobalEventManager"/> for app-wide events; use a private instance (e.g., in `SlotsEngine`) for scoped events.
///
/// Notes:
/// - Exceptions thrown by handlers are caught and logged to avoid breaking the broadcast loop.
/// - Logging can be toggled via <see cref="LoggingEnabled"/> for development diagnostics.
///
/// Performance:
/// - Broadcasts used to allocate a new array snapshot on every call (ToArray()). This created significant
///   GC pressure when many broadcasts occurred each frame. This implementation caches a snapshot per
///   event channel and only rebuilds it when handlers change (on register/unregister).
/// </summary>
public class EventManager
{
	// Global toggle to control whether this class emits Debug logging. Default off to avoid noisy logs.
	public static bool LoggingEnabled = false;

	// internal string-keyed channels
	private Dictionary<string, EventChannel> events = new Dictionary<string, EventChannel>();

	// Internal per-channel container that holds the mutable handler list and a cached snapshot array.
	private class EventChannel
	{
		public readonly List<Action<object>> Handlers = new List<Action<object>>();
		public Action<object>[] Snapshot;
		public bool Dirty = true;

		public void Add(Action<object> a)
		{
			Handlers.Add(a);
			Dirty = true;
		}

		public void Remove(Action<object> a)
		{
			Handlers.Remove(a);
			Dirty = true;
		}

		public bool HasHandlers() => Handlers.Count > 0;

		public Action<object>[] GetSnapshot()
		{
			if (Dirty)
			{
				Snapshot = Handlers.Count == 0 ? Array.Empty<Action<object>>() : Handlers.ToArray();
				Dirty = false;
			}
			return Snapshot;
		}
	}

	// Internal helpers that operate on composed string keys
	/// <summary>
	/// Register a handler for a composed event key. Creates the list if missing.
	/// </summary>
	private void RegisterByKey(string eventKey, Action<object> action)
	{
		if (string.IsNullOrEmpty(eventKey)) throw new ArgumentNullException(nameof(eventKey));
		if (action == null) throw new ArgumentNullException(nameof(action));

		if (events.TryGetValue(eventKey, out EventChannel currentChannel))
		{
			currentChannel.Add(action);
		}
		else
		{
			var channel = new EventChannel();
			channel.Add(action);
			events.Add(eventKey, channel);
		}
	}

	/// <summary>
	/// Unregisters a handler from a composed event key. If no handlers remain the key is removed.
	/// </summary>
	private void UnregisterByKey(string eventKey, Action<object> action)
	{
		if (string.IsNullOrEmpty(eventKey)) throw new ArgumentNullException(nameof(eventKey));
		if (action == null) throw new ArgumentNullException(nameof(action));

		if (events.TryGetValue(eventKey, out EventChannel currentChannel))
		{
			currentChannel.Remove(action);
			if (!currentChannel.HasHandlers())
			{
				events.Remove(eventKey);
			}
		}
		else
		{
			if (LoggingEnabled) Debug.LogWarning($"tried to unregister {eventKey} but not registered!");
		}
	}

	/// <summary>
	/// Broadcasts a value to all handlers registered under the composed key.
	/// Iterates over a cached snapshot so handlers may safely modify subscriptions. Exceptions thrown by handlers are caught and logged to avoid breaking the broadcast loop.
	/// </summary>
	private void BroadcastByKey(string eventKey, object value = null)
	{
		if (string.IsNullOrEmpty(eventKey)) throw new ArgumentNullException(nameof(eventKey));

		if (events.TryGetValue(eventKey, out EventChannel currentChannel))
		{
			var snapshot = currentChannel.GetSnapshot();
			for (int i = 0; i < snapshot.Length; i++)
			{
				var e = snapshot[i];
				if (e == null) continue;
				try
				{
					e.Invoke(value);
				}
				catch (Exception ex)
				{
					if (LoggingEnabled) Debug.LogException(ex);
				}
			}
		}
		else
		{
			// No listeners is not an error; useful for debug tracing
			if (LoggingEnabled) Debug.Log($"{eventKey} broadcast with no listeners.");
		}
	}

	/// <summary>
	/// Returns whether any handlers are registered for the composed key.
	/// </summary>
	private bool HasSubscribersByKey(string eventKey)
	{
		if (string.IsNullOrEmpty(eventKey)) return false;
		return events.TryGetValue(eventKey, out var channel) && channel.HasHandlers();
	}

	// -------------------- Public enum-based API --------------------
	/// <summary>
	/// Register a handler for an enum-based event channel with an optional suffix.
	/// The enum and suffix are composed into a single string key using <see cref="EventKey.Compose"/>.
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
