using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class OverlayMessage : MonoBehaviour
{
	[SerializeField] private Image fadeLayer;
	[SerializeField] private TextMeshProUGUI headerText;
	[SerializeField] private OverlayPanelGroup basePanelGroup;
	[SerializeField] private OverlayButtonGroup baseButtonGroup;

	private List<OverlayPanelGroup> overlayPanels = new List<OverlayPanelGroup>();
	private List<OverlayButtonGroup> overlayButtons = new List<OverlayButtonGroup>();

	void Start()
	{
		GenerateTestPanel();
	}

	public void InitializeMessage(string header = "", List<OverlayPanelSettings> panels = null, List<OverlayButtonSettings> buttons = null)
	{
		basePanelGroup.gameObject.SetActive(false);
		baseButtonGroup.gameObject.SetActive(false);

		headerText.text = header;

		foreach (OverlayPanelSettings panel in panels)
		{
			OverlayPanelGroup newPanel = Instantiate(basePanelGroup, basePanelGroup.transform.parent).GetComponent<OverlayPanelGroup>();
			newPanel.gameObject.SetActive(true);
			newPanel.Initialize(panel);
			overlayPanels.Add(newPanel);
		}

		foreach (OverlayButtonSettings button in buttons)
		{
			OverlayButtonGroup newButton = Instantiate(baseButtonGroup, baseButtonGroup.transform.parent).GetComponent<OverlayButtonGroup>();
			newButton.gameObject.SetActive(true);
			newButton.Initialize(button);
			overlayButtons.Add(newButton);
		}
	}

	private void GenerateTestPanel()
	{
		List<OverlayPanelSettings> panelGroups = new List<OverlayPanelSettings>();
		for (int i = 0; i < 3; i++)
		{
			string message = $"Test message {i}";
			panelGroups.Add(new OverlayPanelSettings(){ Message = message });
		}

		List<OverlayButtonSettings> buttonGroups = new List<OverlayButtonSettings>();
		for (int i = 0; i < 3; i++)
		{
			UnityAction buttonCallback = () => { Debug.Log("Button clicked"); DestroyMessage(); };
			string label = $"test{i}";

			buttonGroups.Add(new OverlayButtonSettings(){ ButtonCallback = buttonCallback , ButtonLabel = label });
		}

		InitializeMessage("test", panelGroups, buttonGroups);
	}

	private void DestroyMessage()
	{
		Destroy(gameObject);
	}
}

public struct OverlayPanelSettings
{
	public string Message;
	public Sprite Image;
	public UnityAction ButtonCallback;
}

public struct OverlayButtonSettings
{
	public string ButtonLabel;
	public UnityAction ButtonCallback;
}