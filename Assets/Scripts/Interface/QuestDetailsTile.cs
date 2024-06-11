using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class QuestDetailsTile : QuestBoardTile
{
	public TMP_Dropdown[] CharacterSelectDropdowns;

	public override void InitializeQuestTile(int questIndexId, Action<QuestBoardTile> buttonCallbackOne, Action<QuestBoardTile> buttonCallbackTwo)
	{
		CurrentQuestIndexId = questIndexId;

		QuestTitle.text = CurrentQuestData.BaseQuestDefinition.QuestName;
		QuestDescription.text = CurrentQuestData.BaseQuestDefinition.QuestDescription;

		AcceptButton.onClick.AddListener(() =>
		{
			buttonCallbackOne(this);
		});

		RejectButton.onClick.AddListener(() =>
		{
			buttonCallbackTwo(this);
		});
	}
}
