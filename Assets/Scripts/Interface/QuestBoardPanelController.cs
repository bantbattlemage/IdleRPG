using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class QuestBoardPanelController : MonoBehaviour
{
	public GameObject QuestBoardTilePrefab;

	public Transform ContentRoot;

	private List<QuestBoardTile> questBoardTiles = new List<QuestBoardTile>();

	private void OnEnable()
	{
		DisplayAvailableQuests();
	}

	private void OnDisable()
	{
		foreach(QuestBoardTile tile in questBoardTiles)
		{
			Destroy(tile.gameObject);
		}

		questBoardTiles = new List<QuestBoardTile>();
	}

	public void AddQuestToQuestBoard(QuestData quest)
	{
		GameObject newInstance = Instantiate(QuestBoardTilePrefab, ContentRoot);
		QuestBoardTile newQuestTile = newInstance.GetComponent<QuestBoardTile>();

		newQuestTile.InitializeQuestTile(quest);

		questBoardTiles.Add(newQuestTile);
	}

	public void DisplayAvailableQuests()
	{
		DataPersistenceManager.Instance.LoadGame();

		foreach(QuestData quest in QuestDataManager.Instance.LocalData.Values)
		{
			AddQuestToQuestBoard(quest);
		}
	}
}
