using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharactersPanelController : MonoBehaviour
{
	public GameObject NewCharacterPanelPrefab;
	public GameObject DisplayCharacterPrefab;

	private List<DisplayCharacterPanel> displayCharacterPanels = new List<DisplayCharacterPanel>();
	private List<NewCharacterPanel> newCharacterPanels = new List<NewCharacterPanel>();

	private System.Action<NewCharacterPanel> beginCreateNewCharacterCallback = null;
	private System.Action<NewCharacterPanel> confirmNewCharacterCallback = null;

	public void InitailizeCharacterPanels()
	{
		//	create a new character button panel if there are no characters
		if (CharacterDataManager.Instance.LocalData == null || CharacterDataManager.Instance.LocalData.Values.Count == 0)
		{
			AddCreateNewCharacterPanel();
		}
		//	load characters
		else
		{
			foreach (var character in CharacterDataManager.Instance.LocalData.Values)
			{
				GameObject newPanelObject = Instantiate(DisplayCharacterPrefab, transform);
				DisplayCharacterPanel newPanel = newPanelObject.GetComponent<DisplayCharacterPanel>();
				newPanel.InitializeCharacterPanel(character);
				displayCharacterPanels.Add(newPanel);
			}

			if (CharacterDataManager.Instance.LocalData.Values.Count < 5)
			{
				AddCreateNewCharacterPanel();
			}
		}
	}

	private void AddCreateNewCharacterPanel()
	{
		GameObject newPanelObject = Instantiate(NewCharacterPanelPrefab, transform);
		NewCharacterPanel newPanel = newPanelObject.GetComponent<NewCharacterPanel>();
		newCharacterPanels.Add(newPanel);

		beginCreateNewCharacterCallback += (NewCharacterPanel sender) =>
		{
			foreach (DisplayCharacterPanel p in displayCharacterPanels)
			{
				Destroy(p.gameObject);
			}
			displayCharacterPanels = new List<DisplayCharacterPanel>();

			DataPersistenceManager.Instance.LoadGame();

			beginCreateNewCharacterCallback = null;
		};

		confirmNewCharacterCallback += (NewCharacterPanel sender) =>
		{
			foreach (DisplayCharacterPanel p in displayCharacterPanels)
			{
				Destroy(p.gameObject);
			}
			displayCharacterPanels = new List<DisplayCharacterPanel>();

			foreach (NewCharacterPanel p in newCharacterPanels)
			{
				Destroy(p.gameObject);
			}
			newCharacterPanels = new List<NewCharacterPanel>();

			confirmNewCharacterCallback = null;

			InitailizeCharacterPanels();
		};

		newPanel.InitializeNewCharacterButtonPanel(beginCreateNewCharacterCallback, confirmNewCharacterCallback);

		//DisplayCharacterPanel newPanel = newPanelObject.GetComponent<DisplayCharacterPanel>();
		//newPanel.InitializeCharacterPanel(CharacterDataController.Instance.loca)
		//characterPanels.Add(newPanel);

	}

	public void OnDisable()
	{
		foreach(var panel in newCharacterPanels)
		{
			Destroy(panel);
		}

		newCharacterPanels = new List<NewCharacterPanel>();
	}
}
