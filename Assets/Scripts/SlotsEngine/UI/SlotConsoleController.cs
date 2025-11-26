using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SlotConsoleController : Singleton<SlotConsoleController>
{
	public Button SpinButton;
	public Button StopButton;
	public Button BetUpButton;
	public Button BetDownButton;

	public Toggle AutoSpinToggle;
	public Toggle AutoStopToggle;

	public TextMeshProUGUI WinText;
	public TextMeshProUGUI ConsoleMessageText;
	public TextMeshProUGUI BetText;
	public TextMeshProUGUI CreditsText;

	private EventManager eventManager;

	// Cached global event manager to avoid repeated Instance lookups
	private GlobalEventManager cachedGlobalEventManager;

	// Presentation buffering: when multiple SlotsEngine instances present wins together
	// buffer messages per-slot and display them one-slot-at-a-time when the group completes.
	private int presentationSessionDepth = 0;
	private Dictionary<SlotsEngine, List<string>> presentationMessagesBySlot = new Dictionary<SlotsEngine, List<string>>();
	private Sequence presentationSequence;

	public void InitializeConsole()
	{
		SpinButton.onClick.AddListener(OnSpinPressed);
		StopButton.onClick.AddListener(OnStopPressed);
		BetDownButton.onClick.AddListener(OnBetDownPressed);
		BetUpButton.onClick.AddListener(OnBetUpPressed);

		cachedGlobalEventManager = GlobalEventManager.Instance;

		cachedGlobalEventManager.RegisterEvent(SlotsEvent.BetChanged, OnBetChanged);
		cachedGlobalEventManager.RegisterEvent(SlotsEvent.CreditsChanged, OnCreditsChanged);

		// Clear console when a new spin begins
		cachedGlobalEventManager.RegisterEvent(SlotsEvent.SpinButtonPressed, OnAnySpinStarted);

		WinText.text = string.Empty;
		SetConsoleMessage(string.Empty);

		BetText.text = string.Empty;
		CreditsText.text = string.Empty;
	}

	public void RegisterSlotsToConsole(EventManager slotsEventManager)
	{
		slotsEventManager.RegisterEvent(State.Idle, "Enter", OnIdleEnter);
		slotsEventManager.RegisterEvent(State.SpinPurchased, "Enter", OnSpinPurchased);
		slotsEventManager.RegisterEvent(State.Spinning, "Enter", OnSpinningEnter);
		slotsEventManager.RegisterEvent(State.Spinning, "Exit", OnSpinningExit);
		slotsEventManager.RegisterEvent(SlotsEvent.StoppingReels, OnStoppingReels);
		// Enable Stop only after the engine notifies that all reels have started their kickup
		slotsEventManager.RegisterEvent(SlotsEvent.ReelsAllStarted, OnReelsAllStarted);
	}

	private void OnCreditsChanged(object obj)
	{
		int value = (int)obj;

		CreditsText.text = value.ToString();
	}

	private void OnBetUpPressed()
	{
		cachedGlobalEventManager?.BroadcastEvent(SlotsEvent.BetUpPressed);
	}

	private void OnBetDownPressed()
	{
		cachedGlobalEventManager?.BroadcastEvent(SlotsEvent.BetDownPressed);
	}

	private void OnBetChanged(object obj)
	{
		BetLevelDefinition definition = (BetLevelDefinition)obj;

		BetText.text = definition.CreditCost.ToString();
	}

	public void ToggleConsoleButtons(bool state)
	{
		SpinButton.interactable = state;
		StopButton.interactable = state;
		BetUpButton.interactable = state;
		BetDownButton.interactable = state;
	}

	/// <summary>
	/// Set a console message. When a presentation session is active, messages are buffered (per-slot) and
	/// will be cycled when the session ends to avoid different slots overwriting the console.
	/// </summary>
	public void SetConsoleMessage(string message)
	{
		if (presentationSessionDepth > 0)
		{
			// when in presentation mode, treat SetConsoleMessage as general (no slot) append
			AppendGeneralPresentationMessage(message);
			return;
		}

		ConsoleMessageText.text = message;
	}

	private void AppendGeneralPresentationMessage(string message)
	{
		if (string.IsNullOrEmpty(message)) return;
		SlotsEngine key = null; // null key used for general messages
		if (!presentationMessagesBySlot.TryGetValue(key, out var list))
		{
			list = new List<string>();
			presentationMessagesBySlot[key] = list;
		}
		list.Add(message);
	}

	/// <summary>
	/// Append a message tied to a specific slots instance during a presentation session.
	/// Messages are buffered and will be displayed when the presentation session cycles.
	/// </summary>
	public void AppendPresentationMessage(SlotsEngine slot, string message)
	{
		if (string.IsNullOrEmpty(message)) return;

		if (presentationSessionDepth > 0)
		{
			if (!presentationMessagesBySlot.TryGetValue(slot, out var list))
			{
				list = new List<string>();
				presentationMessagesBySlot[slot] = list;
			}
			list.Add(message);

			// Show live concatenated messages during presentation for immediate feedback
			if (ConsoleMessageText != null)
			{
				if (string.IsNullOrEmpty(ConsoleMessageText.text)) ConsoleMessageText.text = message;
				else ConsoleMessageText.text = ConsoleMessageText.text + "  |  " + message;
			}

			return;
		}

		// Not in a presentation session: immediate concatenation
		if (ConsoleMessageText != null)
		{
			if (string.IsNullOrEmpty(ConsoleMessageText.text)) ConsoleMessageText.text = message;
			else ConsoleMessageText.text = ConsoleMessageText.text + "  |  " + message;
		}
	}

	public void SetWinText(int value)
	{
		WinText.text = value.ToString();
	}

	/// <summary>
	/// Begin buffering messages for a multi-slots presentation. Call when a presentation participant starts.
	/// Multiple calls are allowed; buffering only begins on the first call and is ended when matching End is called.
	/// </summary>
	public void BeginPresentationSession()
	{
		presentationSessionDepth++;
		if (presentationSessionDepth == 1)
		{
			presentationMessagesBySlot.Clear();
			// stop any existing loop
			StopPresentationLoop();
			if (ConsoleMessageText != null) ConsoleMessageText.text = string.Empty;
		}
	}

	/// <summary>
	/// End buffering and start cycling per-slot messages when the last participant ends.
	/// </summary>
	public void EndPresentationSession()
	{
		presentationSessionDepth = Math.Max(0, presentationSessionDepth - 1);

		if (presentationSessionDepth > 0) return; // still active

		if (presentationMessagesBySlot == null || presentationMessagesBySlot.Count == 0)
		{
			ConsoleMessageText.text = string.Empty;
			return;
		}

		StartPresentationLoop();
	}

	private void StartPresentationLoop()
	{
		StopPresentationLoop();

		// Build deterministic ordered entries
		var entries = new List<KeyValuePair<SlotsEngine, List<string>>>(presentationMessagesBySlot);
		entries.Sort((a, b) =>
		{
			if (a.Key == b.Key) return 0;
			if (a.Key == null) return 1;
			if (b.Key == null) return -1;
			int ai = a.Key.CurrentSlotsData != null ? a.Key.CurrentSlotsData.Index : int.MaxValue;
			int bi = b.Key.CurrentSlotsData != null ? b.Key.CurrentSlotsData.Index : int.MaxValue;
			if (ai != bi) return ai.CompareTo(bi);
			return string.Compare(a.Key.name, b.Key.name, StringComparison.Ordinal);
		});

		if (entries.Count == 0) return;

		presentationSequence = DOTween.Sequence();
		const float perSlotDisplayTime = 1.5f;

		foreach (var kv in entries)
		{
			SlotsEngine slot = kv.Key;
			List<string> messages = kv.Value;
			if (messages == null || messages.Count == 0) continue;

			// Combine messages for this slot
			var sb = new StringBuilder();
			string last = null;
			for (int i = 0; i < messages.Count; i++)
			{
				var m = messages[i];
				if (string.IsNullOrEmpty(m)) continue;
				if (m == last) continue;
				if (sb.Length > 0) sb.Append("  |  ");
				sb.Append(m);
				last = m;
			}

			string header = slot != null ? GetSlotDisplayName(slot) + ": " : string.Empty;
			string displayText = header + sb.ToString();

			presentationSequence.AppendCallback(() => ConsoleMessageText.text = displayText);
			presentationSequence.AppendInterval(perSlotDisplayTime);
		}

		// Loop indefinitely until a spin begins
		presentationSequence.SetLoops(-1, LoopType.Restart);
	}

	private void StopPresentationLoop()
	{
		if (presentationSequence != null)
		{
			presentationSequence.Kill();
			presentationSequence = null;
		}
	}

	/// <summary>
	/// Remove any buffered messages for the given slot and rebuild the presentation loop if active.
	/// Call this when a SlotsEngine is destroyed to avoid holding references to destroyed objects.
	/// </summary>
	public void ClearMessagesForSlot(SlotsEngine slot)
	{
		if (slot == null) return;
		if (presentationMessagesBySlot.Remove(slot))
		{
			// If a loop is running, rebuild it to exclude the removed slot
			if (presentationSequence != null)
			{
				StartPresentationLoop();
			}
		}
	}

	private void OnAnySpinStarted(object obj)
	{
		// Stop looping and clear console when a new spin begins
		StopPresentationLoop();
		presentationMessagesBySlot.Clear();
		if (ConsoleMessageText != null) ConsoleMessageText.text = string.Empty;
	}

	private string GetSlotDisplayName(SlotsEngine slot)
	{
		if (slot == null) return "General";
		try
		{
			if (slot.CurrentSlotsData != null) return $"Slot {slot.CurrentSlotsData.Index}";
		}
		catch { }
		return slot.name ?? "Slot";
	}

	private void OnSpinPurchased(object obj)
	{
		WinText.text = string.Empty;
		SetConsoleMessage(string.Empty);
	}

	private void OnIdleEnter(object obj)
	{
		StopButton.gameObject.SetActive(false);

		SpinButton.gameObject.SetActive(true);
		SpinButton.interactable = true;

		//AutoSpinToggle.interactable = true;

		if (AutoSpinToggle.isOn)
		{
			DOTween.Sequence().AppendInterval(0.2f).AppendCallback(() => 
			{
				if (AutoSpinToggle.isOn && GamePlayer.Instance.CheckAllSlotsState(State.Idle))
				{
					var currentWinData = WinEvaluator.Instance.CurrentSpinWinData;

					if (AutoStopToggle.isOn && currentWinData is { Count: > 0 })
					{
						return;
					}

					OnSpinPressed();
				}
			});
		}
	}

	private void OnStoppingReels(object obj)
	{
		StopButton.GetComponentInChildren<TextMeshProUGUI>().text = "STOPPING";
		StopButton.interactable = false;
	}

	private void OnSpinningExit(object obj)
	{
		StopButton.gameObject.SetActive(false);

		SpinButton.gameObject.SetActive(true);
		SpinButton.interactable = false;
	}

	private void OnSpinningEnter(object obj)
	{
		StopButton.GetComponentInChildren<TextMeshProUGUI>().text = "STOP";
		StopButton.gameObject.SetActive(true);
		// Keep stop disabled until engine confirms all reels have started
		StopButton.interactable = false;

		SpinButton.gameObject.SetActive(false);

		//AutoSpinToggle.interactable = false;

		if (AutoSpinToggle.isOn)
		{
			DOTween.Sequence().AppendInterval(1f).AppendCallback(() =>
			{
				cachedGlobalEventManager?.BroadcastEvent(SlotsEvent.SpinButtonPressed);
			});
		}
	}

	void OnSpinPressed()
	{
		// Broadcast spin-specific event for listeners that need to know a spin was explicitly requested
		cachedGlobalEventManager?.BroadcastEvent(SlotsEvent.SpinButtonPressed);
		// Unified input pathway: also broadcast PlayerInputPressed so all input methods follow the same handler
		cachedGlobalEventManager?.BroadcastEvent(SlotsEvent.PlayerInputPressed);
	}

	void OnStopPressed()
	{
		// Unified input pathway: route Stop button presses through PlayerInputPressed so handling is identical
		cachedGlobalEventManager?.BroadcastEvent(SlotsEvent.PlayerInputPressed);
	}

	private void OnReelsAllStarted(object obj)
	{
		// Engine reports all reels have entered their visible kickup; enable the Stop button now
		try { StopButton.interactable = true; } catch { }
	}
}
