using System;
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

	public void InitializeConsole()
	{
		SpinButton.onClick.AddListener(OnSpinPressed);
		StopButton.onClick.AddListener(OnStopPressed);
		BetDownButton.onClick.AddListener(OnBetDownPressed);
		BetUpButton.onClick.AddListener(OnBetUpPressed);

		GlobalEventManager.Instance.RegisterEvent(SlotsEvent.BetChanged, OnBetChanged);
		GlobalEventManager.Instance.RegisterEvent(SlotsEvent.CreditsChanged, OnCreditsChanged);

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
	}

	private void OnCreditsChanged(object obj)
	{
		int value = (int)obj;

		CreditsText.text = value.ToString();
	}

	private void OnBetUpPressed()
	{
		GlobalEventManager.Instance.BroadcastEvent(SlotsEvent.BetUpPressed);
	}

	private void OnBetDownPressed()
	{
		GlobalEventManager.Instance.BroadcastEvent(SlotsEvent.BetDownPressed);
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

	public void SetConsoleMessage(string message)
	{
		ConsoleMessageText.text = message;
	}

	public void SetWinText(int value)
	{
		WinText.text = value.ToString();
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
		StopButton.interactable = true;

		SpinButton.gameObject.SetActive(false);

		//AutoSpinToggle.interactable = false;

		if (AutoSpinToggle.isOn)
		{
			DOTween.Sequence().AppendInterval(1f).AppendCallback(() =>
			{
				GlobalEventManager.Instance.BroadcastEvent(SlotsEvent.SpinButtonPressed);
			});
		}
	}

	void OnSpinPressed()
	{
		GlobalEventManager.Instance.BroadcastEvent(SlotsEvent.SpinButtonPressed);
	}

	void OnStopPressed()
	{
		GlobalEventManager.Instance.BroadcastEvent(SlotsEvent.StopButtonPressed);
	}
}
