using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SlotConsoleController : Singleton<SlotConsoleController>
{
	public Button SpinButton;
	public Button StopButton;

	public Toggle AutoSpinToggle;

	public Text WinText;
	public Text ConsoleMessageText;

	public void InitializeConsole()
	{
		SpinButton.onClick.AddListener(OnSpinPressed);
		StopButton.onClick.AddListener(OnStopPressed);

		EventManager.Instance.RegisterEvent("IdleEnter", OnIdleEnter);
		EventManager.Instance.RegisterEvent("SpinPurchasedEnter", OnSpinPurchased);
		EventManager.Instance.RegisterEvent("SpinningEnter", OnSpinningEnter);
		EventManager.Instance.RegisterEvent("SpinningExit", OnSpinningExit);
		EventManager.Instance.RegisterEvent("StoppingReels", OnStoppingReels);

		WinText.text = string.Empty;
		SetConsoleMessage(string.Empty);
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
				if (AutoSpinToggle.isOn && StateMachine.Instance.CurrentState == State.Idle)
				{
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
			DOTween.Sequence().AppendInterval(1f).AppendCallback(OnStopPressed);
		}
	}

	void OnSpinPressed()
	{
		EventManager.Instance.BroadcastEvent("SpinButtonPressed");
	}

	void OnStopPressed()
	{
		EventManager.Instance.BroadcastEvent("StopButtonPressed");
	}
}
