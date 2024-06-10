using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using TMPro;
using UnityEditor.U2D.Animation;

public class DisplayCharacterPanel : MonoBehaviour
{
	public TextMeshProUGUI CharacterNameText;
	public TextMeshProUGUI MaxHealthPoints;
	public TextMeshProUGUI CharacterLevel;
	public TextMeshProUGUI CharacterClass;

	public GameObject HeaderGroup;
	public GameObject BodyGroup;
	public GameObject FooterGroup;
	public GameObject[] DisplayCharacterPanelObjects;

	public void InitializeCharacterPanel(CharacterData characterData)
	{
		HeaderGroup.SetActive(false);
		BodyGroup.SetActive(true);
		FooterGroup.SetActive(false);

		DisplayCharacterPanelObjects.ToList().ForEach(x => { x.SetActive(true); });

		UpdateDataDisplay(characterData);
	}

	public void UpdateDataDisplay(CharacterData characterData)
	{
		CharacterNameText.text = characterData.Name;
		MaxHealthPoints.text = string.Format("{0}/{1}", characterData.CurrentHealthPoints, characterData.MaxHealthPoints.ToString());
		CharacterLevel.text = characterData.Level.ToString();
		CharacterClass.text = characterData.CharacterClass.ToString();
	}
}
