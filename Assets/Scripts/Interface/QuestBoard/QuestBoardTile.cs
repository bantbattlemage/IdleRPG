using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class QuestBoardTile : MonoBehaviour
{
	public TextMeshProUGUI QuestTitle;
	public TextMeshProUGUI QuestDescription;
	public TextMeshProUGUI QuestRewards;

	public Button AcceptButton;
	public Button RejectButton;

	public int CurrentQuestIndexId { get; protected set; }
	public QuestData CurrentQuestData { get { return QuestDataManager.Instance.LocalData[CurrentQuestIndexId]; } }

	public virtual void InitializeQuestTile(int questIndexId, System.Action<QuestBoardTile> buttonCallbackOne, System.Action<QuestBoardTile> buttonCallbackTwo)
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

		});
	}
}
