using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ActiveQuestPanel : MonoBehaviour
{
	public TextMeshProUGUI QuestTitle;
	public TextMeshProUGUI QuestDescription;

	public Button SelectQuestButton { get { return GetComponent<Button>(); } }
	public Button AbandonQuestButton;

	public List<GameObject> OverviewObjects;
	public List<GameObject> DetailsObjects;

	public int CurrentQuestIndexId { get; private set; }
	public QuestData CurrentQuestData { get { return QuestDataManager.Instance.LocalData[CurrentQuestIndexId]; } }


	public void InitializeQuestPanel(int questAccessorId)
	{
		DisplayOverviewInfo();

		CurrentQuestIndexId = questAccessorId;

		QuestTitle.text = CurrentQuestData.BaseQuestDefinition.QuestName;
		QuestDescription.text = CurrentQuestData.BaseQuestDefinition.QuestDescription;

		AbandonQuestButton.onClick.AddListener(() => 
		{

		});

		SelectQuestButton.onClick.AddListener(() => 
		{
			DisplayFullQuestInfo();
		});
	}

	public void DisplayOverviewInfo()
	{
		DetailsObjects.ForEach(o => { o.SetActive(false); });
		OverviewObjects.ForEach(o => { o.SetActive(true); });
	}

	public void DisplayFullQuestInfo()
	{
		OverviewObjects.ForEach(o => { o.SetActive(false); });
		DetailsObjects.ForEach(o => { o.SetActive(true); });
	}
}
