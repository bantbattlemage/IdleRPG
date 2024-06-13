using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ActiveQuestsPanelController : MonoBehaviour
{
	public GameObject ActiveQuestPanelPrefab;
	public Transform ContentRoot;

	private List<ActiveQuestPanel> activeQuestPanels = new List<ActiveQuestPanel>();

	private System.Action<ActiveQuestPanel> fullViewCallback;
	private System.Action<ActiveQuestPanel> abandonCallback;

	private void OnEnable()
	{
		activeQuestPanels = new List<ActiveQuestPanel>();

		fullViewCallback = OnQuestFullViewButtonPressedCallback;
		abandonCallback = OnReturnButtonPressedCallback;

		DisplayActiveQuests();
	}

	private void OnDisable()
	{
		foreach(ActiveQuestPanel p in activeQuestPanels)
		{
			Destroy(p.gameObject);
		}

		activeQuestPanels = new List<ActiveQuestPanel>();
	}

	public void DisplayActiveQuests()
	{
		DataPersistenceManager.Instance.LoadGame();

		List<QuestData> activeQuests = QuestDataManager.Instance.LocalData.Values.Where(x => x.Active).ToList();

		foreach(QuestData data in activeQuests)
		{
			AddActiveQuest(data.AccessorId);
		};
	}

	public void AddActiveQuest(int questAccessorId)
	{
		GameObject newPanel = Instantiate(ActiveQuestPanelPrefab, ContentRoot);
		ActiveQuestPanel panel = newPanel.GetComponent<ActiveQuestPanel>();

		panel.InitializeQuestPanel(questAccessorId, fullViewCallback, abandonCallback);

		activeQuestPanels.Add(panel);
	}

	private void OnQuestFullViewButtonPressedCallback(ActiveQuestPanel sender)
	{
		for (int i = 0; i < activeQuestPanels.Count; i++) 
		{
			if (activeQuestPanels[i] != sender)
			{
				Destroy(activeQuestPanels[i].gameObject);
			}
		}

		activeQuestPanels = new List<ActiveQuestPanel>() { sender };

		sender.DisplayFullQuestInfo();
	}

	private void OnReturnButtonPressedCallback(ActiveQuestPanel sender)
	{
		for (int i = 0; i < activeQuestPanels.Count; i++)
		{
			Destroy(activeQuestPanels[i].gameObject);
		}

		activeQuestPanels = new List<ActiveQuestPanel>();

		DisplayActiveQuests();
	}
}
