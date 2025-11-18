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

	public void InitializeConsole(EventManager slotsEventManager)
	{
		SpinButton.onClick.AddListener(OnSpinPressed);
		StopButton.onClick.AddListener(OnStopPressed);
		BetDownButton.onClick.AddListener(OnBetDownPressed);
		BetUpButton.onClick.AddListener(OnBetUpPressed);

		slotsEventManager.RegisterEvent("IdleEnter", OnIdleEnter);
		slotsEventManager.RegisterEvent("SpinPurchasedEnter", OnSpinPurchased);
		slotsEventManager.RegisterEvent("SpinningEnter", OnSpinningEnter);
		slotsEventManager.RegisterEvent("SpinningExit", OnSpinningExit);
		slotsEventManager.RegisterEvent("StoppingReels", OnStoppingReels);

		GlobalEventManager.Instance.RegisterEvent("BetChanged", OnBetChanged);
		GlobalEventManager.Instance.RegisterEvent("CreditsChanged", OnCreditsChanged);

		WinText.text = string.Empty;
		SetConsoleMessage(string.Empty);

		BetText.text = string.Empty;
		CreditsText.text = string.Empty;
	}

	private void OnCreditsChanged(object obj)
	{
		int value = (int)obj;

		CreditsText.text = value.ToString();
	}

	private void OnBetUpPressed()
	{
		GlobalEventManager.Instance.BroadcastEvent("BetUpPressed");
	}

	private void OnBetDownPressed()
	{
		GlobalEventManager.Instance.BroadcastEvent("BetDownPressed");
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
				if (AutoSpinToggle.isOn && GamePlayer.Instance.SlotsEngine.CurrentState == State.Idle)
				{
					var currentWinData = WinlineEvaluator.Instance.CurrentSpinWinData;

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
				GlobalEventManager.Instance.BroadcastEvent("SpinButtonPressed");
			});
		}
	}

	void OnSpinPressed()
	{
		GlobalEventManager.Instance.BroadcastEvent("SpinButtonPressed");
	}

	void OnStopPressed()
	{
		GlobalEventManager.Instance.BroadcastEvent("StopButtonPressed");
	}
}
