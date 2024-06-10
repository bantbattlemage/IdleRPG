using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using TMPro;

public class CharacterPanel : MonoBehaviour
{
	public Text CharacterNameText;
	public Button CreateNewCharacterButton;
	public Button ConfirmNewCharacterButton;
	public TMP_InputField NewCharacterNameInputField;

	public GameObject[] NewCharacterPanelObjects;
	public GameObject[] DisplayCharacterPanelObjects;


	public void InitializeNewCharacterButtonPanel(System.Action confirmCallback, System.Action<CharacterPanel> beginCallback)
	{
		DisplayCharacterPanelObjects.ToList().ForEach(x => { x.SetActive(false); });
		NewCharacterPanelObjects.ToList().ForEach(x => { x.SetActive(false); });
		ConfirmNewCharacterButton.gameObject.SetActive(false);

		CreateNewCharacterButton.gameObject.SetActive(true);
		CreateNewCharacterButton.onClick.AddListener(() => 
		{
			NewCharacterPanelObjects.ToList().ForEach(x => { x.SetActive(true); });

			ConfirmNewCharacterButton.gameObject.SetActive(true);
			ConfirmNewCharacterButton.onClick.RemoveAllListeners();
			ConfirmNewCharacterButton.onClick.AddListener(() =>
			{
				string name = NewCharacterNameInputField.text;

				if(name != null && name.Length > 0)
				{
					CharacterDataController.Instance.CreateNewCharacter(name);
					confirmCallback();
				}
			});

			CreateNewCharacterButton.gameObject.SetActive(false);
			CreateNewCharacterButton.onClick.RemoveAllListeners();

			beginCallback(this);
		});
	}

	public void InitializeCharacterPanel(CharacterData characterData)
	{
		DisplayCharacterPanelObjects.ToList().ForEach(x => { x.SetActive(true); });
		NewCharacterPanelObjects.ToList().ForEach(x => { x.SetActive(false); });

		CreateNewCharacterButton.gameObject.SetActive(false);
		ConfirmNewCharacterButton.gameObject.SetActive(false);

		CharacterNameText.text = characterData.Name;
	}
}
