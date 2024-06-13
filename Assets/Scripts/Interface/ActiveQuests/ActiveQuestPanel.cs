using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ActiveQuestPanel : MonoBehaviour
{
	public TextMeshProUGUI QuestTitle;
	public TextMeshProUGUI QuestDescription;
	public TextMeshProUGUI AssignedCharactersDisplay;

	public Button FullViewButton;
	public Button AbandonQuestButton;

	public List<GameObject> OverviewObjects;
	public List<GameObject> DetailsObjects;

	public int CurrentQuestIndexId { get; private set; }
	public QuestData CurrentQuestData { get { return QuestDataManager.Instance.LocalData[CurrentQuestIndexId]; } }

	public void InitializeQuestPanel(int questAccessorId, System.Action<ActiveQuestPanel> fullViewCallback, System.Action<ActiveQuestPanel> abandonCallback)
	{
		DisplayOverviewInfo();

		CurrentQuestIndexId = questAccessorId;

		QuestTitle.text = CurrentQuestData.BaseQuestDefinition.QuestName;
		QuestDescription.text = CurrentQuestData.BaseQuestDefinition.QuestDescription;

		AssignedCharactersDisplay.text = string.Empty;
		foreach(string s in CurrentQuestData.ActiveCharacters)
		{
			AssignedCharactersDisplay.text += s;

			if(s != CurrentQuestData.ActiveCharacters.Last())
			{
				AssignedCharactersDisplay.text += ", ";
			}
		}

		AbandonQuestButton.onClick.RemoveAllListeners();
		AbandonQuestButton.onClick.AddListener(() => 
		{
			abandonCallback(this);
		});

		FullViewButton.onClick.RemoveAllListeners();
		FullViewButton.onClick.AddListener(() =>
		{
			fullViewCallback(this);
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
