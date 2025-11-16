using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SlotConsoleController : Singleton<SlotConsoleController>
{
	public Button SpinButton;
	public Button StopButton;

	public Toggle AutoSpinToggle;

	public void InitializeConsole()
	{
		SpinButton.onClick.AddListener(OnSpinPressed);
		StopButton.onClick.AddListener(OnStopPressed);

		EventManager.Instance.RegisterEvent("IdleEnter", OnIdleEnter);
		EventManager.Instance.RegisterEvent("SpinningEnter", OnSpinningEnter);
		EventManager.Instance.RegisterEvent("SpinningExit", OnSpinningExit);
		EventManager.Instance.RegisterEvent("StoppingReels", OnStoppingReels);
	}

	private void OnIdleEnter(object obj)
	{
		StopButton.gameObject.SetActive(false);

		SpinButton.gameObject.SetActive(true);
		SpinButton.interactable = true;

		if (AutoSpinToggle.isOn)
		{
			DOTween.Sequence().AppendInterval(0.2f).AppendCallback(OnSpinPressed);
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
