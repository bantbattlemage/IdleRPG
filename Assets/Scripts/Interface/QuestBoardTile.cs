using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class QuestBoardTile : MonoBehaviour
{
	public TextMeshProUGUI QuestTitle;
	public TextMeshProUGUI QuestDescription;
	public Button QuestButton;

	public QuestData CurrentQuestData;

	public void InitializeQuestTile(QuestData questData)
	{
		CurrentQuestData = questData;

		QuestTitle.text = CurrentQuestData.BaseQuestDefinition.QuestName;
		QuestDescription.text = CurrentQuestData.BaseQuestDefinition.QuestDescription;
	}
}
