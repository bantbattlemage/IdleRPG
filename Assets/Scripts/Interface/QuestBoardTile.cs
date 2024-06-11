using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class QuestBoardTile : MonoBehaviour
{
	public TextMeshProUGUI QuestTitle;
	public TextMeshProUGUI QuestDescription;
	public Button AcceptButton;
	public Button RejectButton;

	public int CurrentQuestIndexId { get; private set; }
	public QuestData CurrentQuestData { get { return QuestDataManager.Instance.LocalData[CurrentQuestIndexId]; } }

	public void InitializeQuestTile(int questIndexId, System.Action<QuestBoardTile> acceptCallback)
	{
		CurrentQuestIndexId = questIndexId;

		QuestTitle.text = CurrentQuestData.BaseQuestDefinition.QuestName;
		QuestDescription.text = CurrentQuestData.BaseQuestDefinition.QuestDescription;

		AcceptButton.onClick.AddListener(() => 
		{
			QuestDataManager.Instance.ActivateQuest(questIndexId, new List<CharacterData>());
			acceptCallback(this);
		});

		RejectButton.onClick.AddListener(() => 
		{

		});
	}
}
