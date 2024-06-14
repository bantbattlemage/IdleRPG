using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameStartController : Singleton<GameStartController>
{
	public MainMenuController MainMenu;
	public GameObject LeftPanelGroup;
	public GameObject RightPanelGroup;

	private void Start()
	{
		LeftPanelGroup.SetActive(false); 
		RightPanelGroup.SetActive(false);

		MainMenu.gameObject.SetActive(true);
	}

	public void EnablePanels()
	{
		LeftPanelGroup.SetActive(true);
		RightPanelGroup.SetActive(true);
	}

	public void BeginGame()
	{
		MainMenu.CloseMainMenu();
		InitializeRandomTestQuests();
		QuestController.Instance.Active = true;
	}

	public void InitializeRandomTestQuests()
	{
		DataPersistenceManager.Instance.LoadGame();

		if (QuestDataManager.Instance.GetAllData().Count > 0)
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
