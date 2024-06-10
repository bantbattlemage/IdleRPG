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

	private void OnEnable()
	{
		InitializeCharacterPanels();
	}

	public void InitializeCharacterPanels()
	{
		RemoveAllPanels();

		displayCharacterPanels = new List<DisplayCharacterPanel>();
		newCharacterPanels = new List<NewCharacterPanel>();

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
			DataPersistenceManager.Instance.LoadGame();

			foreach (var panel in displayCharacterPanels)
			{
				Destroy(panel.gameObject);
			}
			displayCharacterPanels = new List<DisplayCharacterPanel>();

			beginCreateNewCharacterCallback = null;
		};

		confirmNewCharacterCallback += (NewCharacterPanel sender) =>
		{
			RemoveAllPanels();

			confirmNewCharacterCallback = null;

			InitializeCharacterPanels();
		};

		newPanel.InitializeNewCharacterButtonPanel(beginCreateNewCharacterCallback, confirmNewCharacterCallback);
	}

	private void RemoveAllPanels()
	{
		foreach (var panel in newCharacterPanels)
		{
			Destroy(panel.gameObject);
		}

		foreach (var panel in displayCharacterPanels)
		{
			Destroy(panel.gameObject);
		}

		newCharacterPanels = new List<NewCharacterPanel>();
		displayCharacterPanels = new List<DisplayCharacterPanel>();
	}

	public void OnDisable()
	{
		RemoveAllPanels();
	}
}
