using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SlotConsoleController : Singleton<SlotConsoleController>
{
	public Button SpinButton;
	public Button StopButton;

	void Start()
	{
		SpinButton.onClick.AddListener(OnSpinPressed);
		StopButton.onClick.AddListener(OnStopPressed);

		EventManager.Instance.RegisterEvent("SpinningEnter", OnSpinningEnter);
		EventManager.Instance.RegisterEvent("SpinningExit", OnSpinningExit);
		EventManager.Instance.RegisterEvent("StoppingReels", OnStoppingReels);

		StopButton.gameObject.SetActive(false);
	}

	private void OnStoppingReels(object obj)
	{
		StopButton.GetComponentInChildren<TextMeshProUGUI>().text = "STOPPING";
	}

	private void OnSpinningExit(object obj)
	{
		SpinButton.gameObject.SetActive(true);
		StopButton.gameObject.SetActive(false);
	}

	private void OnSpinningEnter(object obj)
	{
		SpinButton.gameObject.SetActive(false);

		StopButton.GetComponentInChildren<TextMeshProUGUI>().text = "STOP";
		StopButton.gameObject.SetActive(true);
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
