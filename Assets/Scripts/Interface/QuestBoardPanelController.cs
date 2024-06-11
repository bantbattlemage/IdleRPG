using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class QuestBoardPanelController : MonoBehaviour
{
	public GameObject QuestBoardTilePrefab;

	public QuestDetailsTile QuestTileDetails;

	public Transform ContentRoot;

	private List<QuestBoardTile> questBoardTiles = new List<QuestBoardTile>();

	private Action<QuestBoardTile> viewCallback;
	private Action<QuestBoardTile> acceptCallback;
	private Action<QuestBoardTile> rejectCallback;

	private void OnEnable()
	{
		acceptCallback = OnAcceptCallback;
		viewCallback = OnViewCallback;
		rejectCallback = OnRejectCallback;

		DisplayAvailableQuests();

		QuestTileDetails.gameObject.SetActive(false);
	}

	private void OnDisable()
	{
		RemoveAllQuestTiles();
	}

	private void RemoveAllQuestTiles()
	{
		foreach (QuestBoardTile tile in questBoardTiles)
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

		newQuestTile.InitializeQuestTile(questAccessorIndex, viewCallback, rejectCallback);

		questBoardTiles.Add(newQuestTile);
	}

	private void OnAcceptCallback(QuestBoardTile quest)
	{
		QuestDataManager.Instance.ActivateQuest(quest.CurrentQuestIndexId, new List<CharacterData>());

		QuestTileDetails.gameObject.SetActive(false);

		RemoveAllQuestTiles();
		DisplayAvailableQuests();
	}

	private void OnViewCallback(QuestBoardTile quest)
	{
		RemoveAllQuestTiles();
		QuestTileDetails.gameObject.SetActive(true);
		QuestTileDetails.InitializeQuestTile(quest.CurrentQuestIndexId, acceptCallback, rejectCallback);
	}

	private void OnRejectCallback(QuestBoardTile quest)
	{
		QuestTileDetails.gameObject.SetActive(false);
		RemoveAllQuestTiles();
		DisplayAvailableQuests();
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
