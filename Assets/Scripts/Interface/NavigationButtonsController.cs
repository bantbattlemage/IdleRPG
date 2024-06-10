using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class NavigationButtonsController : MonoBehaviour
{
	public Button ActiveQuestsButton;
	public Button QuestBoardButton;
	public Button CharactersButton;
	public Button InventoryButton;
	public Button ProfessionsButton;
	public Button GuildButton;

	public GameObject QuestsPanel;
	public GameObject QuestBoardPanel;
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
			ActiveQuestsButton, QuestBoardButton, CharactersButton, InventoryButton, ProfessionsButton, GuildButton
		};

		panels = new List<GameObject>()
		{
			QuestsPanel, QuestBoardPanel, CharactersPanel, InventoryPanel, ProfessionsPanel, GuildPanel
		};

		ActiveQuestsButton.onClick.AddListener(ActiveQuestButtonClicked);
		QuestBoardButton.onClick.AddListener(QuestBoardButtonClicked);
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

	private void ActiveQuestButtonClicked()
	{
		SetAllButtons(true);
		SetAllPanels(false);

		ActiveQuestsButton.interactable = false;
		QuestsPanel.SetActive(true);
	}

	private void QuestBoardButtonClicked()
	{
		SetAllButtons(true);
		SetAllPanels(false);

		QuestBoardButton.interactable = false;
		QuestBoardPanel.SetActive(true);
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
