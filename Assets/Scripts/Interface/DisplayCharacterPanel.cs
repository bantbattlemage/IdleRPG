using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using TMPro;

public class DisplayCharacterPanel : MonoBehaviour
{
	public TextMeshProUGUI CharacterNameText;
	public TextMeshProUGUI MaxHealthPoints;

	public GameObject[] DisplayCharacterPanelObjects;

	public void InitializeCharacterPanel(CharacterData characterData)
	{
		DisplayCharacterPanelObjects.ToList().ForEach(x => { x.SetActive(true); });
		CharacterNameText.text = characterData.Name;
		MaxHealthPoints.text = characterData.MaxHealthPoints.ToString();
	}
}
