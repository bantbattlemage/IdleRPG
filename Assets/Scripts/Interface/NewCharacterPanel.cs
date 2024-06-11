using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using TMPro;
using static EnumDefinitions;

public class NewCharacterPanel : MonoBehaviour
{
	public TextMeshProUGUI CharacterNameText;
	public Button CreateNewCharacterButton;
	public Button ConfirmNewCharacterButton;
	public Button CancelButton;
	public TMP_InputField NewCharacterNameInputField;
	public TMP_Dropdown NewCharacterClassInputDropdown;

	public GameObject HeaderGroup;
	public GameObject BodyGroup;
	public GameObject FooterGroup;
	public GameObject[] NewCharacterPanelObjects;
	
	public void InitializeNewCharacterButtonPanel(System.Action<NewCharacterPanel> beginCallback, System.Action<NewCharacterPanel> confirmCallback)
	{
		HeaderGroup.SetActive(false);
		BodyGroup.SetActive(true);
		FooterGroup.SetActive(false);

		NewCharacterPanelObjects.ToList().ForEach(x => { x.SetActive(false); });

		ConfirmNewCharacterButton.gameObject.SetActive(false);
		CreateNewCharacterButton.gameObject.SetActive(true);

		CreateNewCharacterButton.onClick.AddListener(() => 
		{
			NewCharacterPanelObjects.ToList().ForEach(x => { x.SetActive(true); });
			HeaderGroup.SetActive(true);
			BodyGroup.SetActive(true);
			FooterGroup.SetActive(true);

			ConfirmNewCharacterButton.gameObject.SetActive(true);
			ConfirmNewCharacterButton.onClick.RemoveAllListeners();
			ConfirmNewCharacterButton.onClick.AddListener(() =>
			{
				string name = NewCharacterNameInputField.text;
				GameClassEnum gameClass = (GameClassEnum)NewCharacterClassInputDropdown.value;

				if(name != null && name.Length > 0)
				{
					if (CharacterDataManager.Instance.CreateNewCharacter(name, gameClass))
					{
						confirmCallback(this);
					}
				}
			});

			CreateNewCharacterButton.gameObject.SetActive(false);
			CreateNewCharacterButton.onClick.RemoveAllListeners();

			beginCallback(this);
		});

		CancelButton.onClick.AddListener(() =>
		{
			confirmCallback(this);
		});
	}
}
