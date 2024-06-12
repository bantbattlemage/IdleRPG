using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameController : MonoBehaviour
{
	public MainMenuController MainMenu;
	public GameObject LeftPanelGroup;
	public GameObject RightPanelGroup;
	public CharactersPanelController CharactersPanel;

	public static GameController Instance;

	private void Awake()
	{
		if (Instance == null)
		{
			Instance = this;
		}
		else
		{
			throw new System.Exception("more than one GameController");
		}
	}

	private void Start()
	{
		LeftPanelGroup.SetActive(false); 
		RightPanelGroup.SetActive(false);
		CharactersPanel.gameObject.SetActive(false);

		MainMenu.gameObject.SetActive(true);
	}

	private void Update()
	{
		
	}

	public void EnablePanels()
	{
		LeftPanelGroup.SetActive(true);
		RightPanelGroup.SetActive(true);
	}
}
