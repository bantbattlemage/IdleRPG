using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class NavigationButtonsController : MonoBehaviour
{
	public Button QuestsButton;
	public Button CharactersButton;
	public Button InventoryButton;
	public Button ProfessionsButton;
	public Button GuildButton;

	public GameObject QuestsPanel;
	public GameObject CharactersPanel;
	public GameObject InventoryPanel;
	public GameObject ProfessionsPanel;
	public GameObject GuildPanel;

	private List<Button> buttons;
	private List<GameObject> panels;

	private void Start()
	{
		buttons = new List<Button>()
		{
			QuestsButton, CharactersButton, InventoryButton, ProfessionsButton, GuildButton
		};

		panels = new List<GameObject>()
		{
			QuestsPanel, CharactersPanel, InventoryPanel, ProfessionsPanel, GuildPanel
		};

		QuestsButton.onClick.AddListener(QuestButtonClicked);
		CharactersButton.onClick.AddListener(CharactersButtonClicked);
		InventoryButton.onClick.AddListener(InventoryButtonClicked);
		ProfessionsButton.onClick.AddListener(ProfessionsButtonClicked);
		GuildButton.onClick.AddListener(GuildButtonClicked);

		SetAllPanels(false);
		CharactersButtonClicked();
	}

	public void SetAllButtons(bool enable)
	{
		buttons.ForEach(button => { button.interactable = enable; });
	}

	public void SetAllPanels(bool enable)
	{
		panels.ForEach(panel => { panel.SetActive(enable); });
	}

	private void QuestButtonClicked()
	{
		SetAllButtons(true);
		SetAllPanels(false);

		QuestsButton.interactable = false;
		QuestsPanel.SetActive(true);
	}

	private void CharactersButtonClicked()
	{
		SetAllButtons(true);
		SetAllPanels(false);

		CharactersButton.interactable = false;
		CharactersPanel.SetActive(true);
	}
	private void InventoryButtonClicked()
	{
		SetAllButtons(true);
		SetAllPanels(false);

		InventoryButton.interactable = false;
		InventoryPanel.SetActive(true);
	}

	private void ProfessionsButtonClicked()
	{
		SetAllButtons(true);
		SetAllPanels(false);

		ProfessionsButton.interactable = false;
		ProfessionsPanel.SetActive(true);
	}

	private void GuildButtonClicked()
	{
		SetAllButtons(true);
		SetAllPanels(false);

		GuildButton.interactable = false;
		GuildPanel.SetActive(true);
	}
}
