using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
	public Button ContinueButton;
	public Button NewGameButton;
	public Button LoadGameButton;
	public Button ExitGameButton;

	public GameObject[] ObjectsToLoad;

	private void Start()
	{
		ContinueButton.onClick.AddListener(() => 
		{
			DataPersistenceManager.Instance.LoadGame();
			BeginGame();
		});

		NewGameButton.onClick.AddListener(() =>
		{
			DataPersistenceManager.Instance.NewGame();
			DataPersistenceManager.Instance.SaveGame();
			BeginGame();
		});

		LoadGameButton.onClick.AddListener(() =>
		{
			DataPersistenceManager.Instance.LoadGame();
			BeginGame();
		});

		ExitGameButton.onClick.AddListener(() =>
		{

		});

		var existingProfiles = DataPersistenceManager.Instance.GetAllProfilesGameData();
		if (existingProfiles.Any())
		{
			ContinueButton.gameObject.SetActive(true);
		}
		else
		{
			ContinueButton.gameObject.SetActive(false);
		}
	}

	private void BeginGame()
	{
		//DataPersistenceManager.Instance.BeginAutoSaveCoroutine();
		ObjectsToLoad.ToList().ForEach(x => { x.SetActive(true); });

		InitializeRandomTestQuests();
		QuestController.Instance.Active = true;

		gameObject.SetActive(false);
	}

	public void InitializeRandomTestQuests()
	{
		DataPersistenceManager.Instance.LoadGame();

		if (QuestDataManager.Instance.LocalData.Values.Count > 0)
		{
			return;
		}

		for (int i = 0; i < 5; i++)
		{
			QuestData newQuest = QuestDataManager.Instance.AddRandomNewQuest();
		}

		Debug.Log("Set up test quests");

		DataPersistenceManager.Instance.SaveGame();
	}
}
