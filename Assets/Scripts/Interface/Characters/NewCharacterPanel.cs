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
	public Button CancelButton;
	public TMP_InputField NewCharacterNameInputField;
	public Button[] ClassSelectButtons;

	public GameObject HeaderGroup;
	public GameObject BodyGroup;
	public GameObject FooterGroup;
	public GameObject[] NewCharacterPanelObjects;

	private GameClassEnum selectedClass;
	
	public void InitializeNewCharacterButtonPanel(System.Action<NewCharacterPanel> beginCallback, System.Action<NewCharacterPanel> confirmCallback)
	{
		HeaderGroup.SetActive(false);
		BodyGroup.SetActive(true);
		FooterGroup.SetActive(false);

		NewCharacterPanelObjects.ToList().ForEach(x => { x.SetActive(false); });

		ConfirmNewCharacterButton.gameObject.SetActive(false);
		CreateNewCharacterButton.gameObject.SetActive(true);

		ClassSelectButtons.ToList().ForEach(x => 
		{
			x.onClick.RemoveAllListeners();
			x.onClick.AddListener(() => 
			{
				OnClassButtonPressed(x);
			});
		});

		CreateNewCharacterButton.onClick.RemoveAllListeners();
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

				if(name != null && name.Length > 0)
				{
					if (CharacterDataManager.Instance.CreateNewCharacter(name, selectedClass))
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

	private void OnClassButtonPressed(Button b)
	{
		int index = ClassSelectButtons.ToList().IndexOf(b);
		selectedClass = (GameClassEnum)index;

		foreach(Button button in ClassSelectButtons)
		{
			button.interactable = true;
		}

		b.interactable = false;
	}
}
