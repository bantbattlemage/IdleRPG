using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using TMPro;

public class NewCharacterPanel : MonoBehaviour
{
	public TextMeshProUGUI CharacterNameText;
	public Button CreateNewCharacterButton;
	public Button ConfirmNewCharacterButton;
	public TMP_InputField NewCharacterNameInputField;

	public GameObject[] NewCharacterPanelObjects;
	
	public void InitializeNewCharacterButtonPanel(System.Action<NewCharacterPanel> beginCallback, System.Action<NewCharacterPanel> confirmCallback)
	{
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
					if (CharacterDataManager.Instance.CreateNewCharacter(name))
					{
						confirmCallback(this);
					}
				}
			});

			CreateNewCharacterButton.gameObject.SetActive(false);
			CreateNewCharacterButton.onClick.RemoveAllListeners();

			beginCallback(this);
		});
	}
}
