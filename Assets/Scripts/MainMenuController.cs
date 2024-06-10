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
			DataPersistenceManager.Instance.ChangeSelectedProfileId("test");
			BeginGame();
		});

		NewGameButton.onClick.AddListener(() =>
		{
			DataPersistenceManager.Instance.NewGame();
			DataPersistenceManager.Instance.SaveGame();
			DataPersistenceManager.Instance.ChangeSelectedProfileId("test");
			DataPersistenceManager.Instance.SaveGame();
			BeginGame();
		});

		LoadGameButton.onClick.AddListener(() =>
		{
			DataPersistenceManager.Instance.ChangeSelectedProfileId("test");
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
		gameObject.SetActive(false);
	}
}
