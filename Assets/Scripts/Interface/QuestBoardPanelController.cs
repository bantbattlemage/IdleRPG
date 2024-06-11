using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class QuestBoardPanelController : MonoBehaviour
{
	public GameObject QuestBoardTilePrefab;

	public Transform ContentRoot;

	private List<QuestBoardTile> questBoardTiles = new List<QuestBoardTile>();

	private Action<QuestBoardTile> acceptCallback;

	private void OnEnable()
	{
		acceptCallback = OnAcceptCallback;
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

	public void AddQuestToQuestBoard(int questAccessorIndex)
	{
		//	only add quests that haven't already been accepted
		if (QuestDataManager.Instance.LocalData[questAccessorIndex].Active)
		{
			return;
		}

		GameObject newInstance = Instantiate(QuestBoardTilePrefab, ContentRoot);
		QuestBoardTile newQuestTile = newInstance.GetComponent<QuestBoardTile>();

		newQuestTile.InitializeQuestTile(questAccessorIndex, acceptCallback);

		questBoardTiles.Add(newQuestTile);
	}

	private void OnAcceptCallback(QuestBoardTile quest)
	{
		questBoardTiles.Remove(quest);
		Destroy(quest.gameObject);
	}

	public void DisplayAvailableQuests()
	{
		DataPersistenceManager.Instance.LoadGame();

		foreach(var kvp in QuestDataManager.Instance.LocalData)
		{
			AddQuestToQuestBoard(kvp.Key);
		}
	}
}
