using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ActiveQuestsPanelController : MonoBehaviour
{
	public GameObject ActiveQuestPanelPrefab;
	public Transform ContentRoot;

	private List<ActiveQuestPanel> activeQuestPanels = new List<ActiveQuestPanel>();

	private void OnEnable()
	{
		activeQuestPanels = new List<ActiveQuestPanel>();

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

		panel.InitializeQuestPanel(questAccessorId);

		activeQuestPanels.Add(panel);
	}
}
