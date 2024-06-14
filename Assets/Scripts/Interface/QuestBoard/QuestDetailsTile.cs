using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

public class QuestDetailsTile : QuestBoardTile
{
	public Button[] CharacterAssignmentButtons;

	private string[] buttonCharacterAssignments;

	public List<string> SelectedCharacters { get; private set; }


	public void InitializeQuestTile(int questIndexId, Action<QuestDetailsTile> buttonCallbackOne, Action<QuestDetailsTile> buttonCallbackTwo)
	{
		CurrentQuestIndexId = questIndexId;

		QuestTitle.text = CurrentQuestData.GetBaseQuestData().QuestName;
		QuestDescription.text = CurrentQuestData.GetBaseQuestData().QuestDescription;

		buttonCharacterAssignments = new string[CharacterAssignmentButtons.Length];
		SelectedCharacters = new List<string>();

		foreach(Button button in CharacterAssignmentButtons)
		{
			button.GetComponentInChildren<TextMeshProUGUI>().text = "";
			button.onClick.RemoveAllListeners();
			button.image.color = button.colors.disabledColor;
			button.interactable = false;
			button.onClick.AddListener(() => 
			{
				OnCharacterAssignButtonClicked(button);
			});
		}

		List<CharacterData> characters = CharacterDataManager.Instance.GetAllData();
		for (int i = 0; i < characters.Count; i++)
		{
			if (characters[i].ActiveQuestId != 0)
			{
				CharacterAssignmentButtons[i].interactable = false;
				CharacterAssignmentButtons[i].GetComponentInChildren<TextMeshProUGUI>().text = characters[i].Name + " (Already On Quest)";
			}
			else
			{
				CharacterAssignmentButtons[i].interactable = true;
				CharacterAssignmentButtons[i].GetComponentInChildren<TextMeshProUGUI>().text = characters[i].Name;
			}

			buttonCharacterAssignments[i] = characters[i].Name;

		}

		AcceptButton.onClick.RemoveAllListeners();
		AcceptButton.onClick.AddListener(() =>
		{
			if(SelectedCharacters.Count > 0)
			{
				buttonCallbackOne(this);
			}
		});

		RejectButton.onClick.RemoveAllListeners();
		RejectButton.onClick.AddListener(() =>
		{
			buttonCallbackTwo(this);
		});
	}

	private void OnCharacterAssignButtonClicked(Button b)
	{
		string character = buttonCharacterAssignments[CharacterAssignmentButtons.ToList().IndexOf(b)];

		if(SelectedCharacters.Contains(character))
		{
			b.image.color = b.colors.disabledColor;
			SelectedCharacters.Remove(character);
		}
		else
		{
			b.image.color = b.colors.normalColor;
			SelectedCharacters.Add(character);
		}
	}
}
