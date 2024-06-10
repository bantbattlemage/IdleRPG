using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharactersPanelController : MonoBehaviour
{
	public GameObject CharacterPanelPrefab;

	private List<CharacterPanel> characterPanels = new List<CharacterPanel>();

	private System.Action<CharacterPanel> beginCreateNewCharacterCallback = null;
	private System.Action confirmNewCharacterCallback = null;

	private void Start()
	{
		DataPersistenceManager.Instance.LoadGame();
	}

	private void OnEnable()
	{
		InitailizeCharacterPanels();
	}

	public void InitailizeCharacterPanels()
	{
		//	create a new character button panel if there are no characters
		if (CharacterDataController.Instance.LocalData == null || CharacterDataController.Instance.LocalData.Values.Count == 0)
		{
			AddCreateNewCharacterPanel();
		}
		//	load characters
		else
		{
			foreach (var character in CharacterDataController.Instance.LocalData.Values)
			{
				GameObject newPanelObject = Instantiate(CharacterPanelPrefab, transform);
				CharacterPanel newPanel = newPanelObject.GetComponent<CharacterPanel>();
				newPanel.InitializeCharacterPanel(character);
				characterPanels.Add(newPanel);
			}

			if (CharacterDataController.Instance.LocalData.Values.Count < 5)
			{
				AddCreateNewCharacterPanel();
			}
		}
	}

	private void AddCreateNewCharacterPanel()
	{
		GameObject newPanelObject = Instantiate(CharacterPanelPrefab, transform);
		confirmNewCharacterCallback += () =>
		{
			foreach (CharacterPanel p in characterPanels)
			{
				Destroy(p.gameObject);
			}

			characterPanels = new List<CharacterPanel>();

			DataPersistenceManager.Instance.LoadGame();
			InitailizeCharacterPanels();

			confirmNewCharacterCallback = null;
		};

		beginCreateNewCharacterCallback += (CharacterPanel sender) =>
		{
			//foreach (CharacterPanel p in characterPanels)
			//{
			//	if (sender != p)
			//	{
			//		Destroy(sender.gameObject);
			//	}
			//}

			//characterPanels = new List<CharacterPanel>
			//{
			//	sender
			//};

			//beginCreateNewCharacterCallback = null;
		};

		CharacterPanel newPanel = newPanelObject.GetComponent<CharacterPanel>();
		newPanel.InitializeNewCharacterButtonPanel(confirmNewCharacterCallback, beginCreateNewCharacterCallback);
		characterPanels.Add(newPanel);
	}

	public void OnDisable()
	{
		foreach(var panel in characterPanels)
		{
			Destroy(panel);
		}

		characterPanels = new List<CharacterPanel>();
	}
}
