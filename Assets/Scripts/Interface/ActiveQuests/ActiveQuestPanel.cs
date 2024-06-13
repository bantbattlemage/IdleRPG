using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class ActiveQuestPanel : MonoBehaviour
{
	public TextMeshProUGUI QuestTitle;
	public TextMeshProUGUI QuestDescription;

	public Button FullViewButton;
	public Button AbandonQuestButton;

	public CharacterView[] CharacterOverviewDisplays;

	public GameEntityView[] CharacterEntityDisplays;
	public GameEntityView[] EnemyEntityDisplays;

	public List<GameObject> OverviewObjects;
	public List<GameObject> DetailsObjects;

	public int CurrentQuestIndexId { get; private set; }
	public QuestData CurrentQuestData { get { return QuestDataManager.Instance.LocalData[CurrentQuestIndexId]; } }

	private void Update()
	{
		if (!gameObject.activeSelf)
		{
			return;
		}

		UpdateEntityDisplays();
	}

	public void InitializeQuestPanel(int questAccessorId, System.Action<ActiveQuestPanel> fullViewCallback, System.Action<ActiveQuestPanel> abandonCallback)
	{
		DisplayOverviewInfo();

		CurrentQuestIndexId = questAccessorId;

		QuestTitle.text = CurrentQuestData.BaseQuestDefinition.QuestName;
		QuestDescription.text = CurrentQuestData.BaseQuestDefinition.QuestDescription;

		foreach (CharacterView characterView in CharacterOverviewDisplays) 
		{
			characterView.CharacterName.text = string.Empty;
			characterView.HP.text = string.Empty;
			characterView.XP.text = string.Empty;
		}

		foreach (GameEntityView entity in CharacterEntityDisplays)
		{
			entity.EntityName.text = string.Empty;
			entity.HP.text = string.Empty;
			entity.MP.text = string.Empty;
			entity.XP.text = string.Empty;
			entity.SwingTimerText.text = string.Empty;
			entity.transform.parent.gameObject.SetActive(false);
		}

		foreach (GameEntityView entity in EnemyEntityDisplays)
		{
			entity.EntityName.text = string.Empty;
			entity.HP.text = string.Empty;
			entity.MP.text = string.Empty;
			entity.XP.text = string.Empty;
			entity.SwingTimerText.text = string.Empty;
			//entity.transform.parent.gameObject.SetActive(false);
		}

		UpdateEntityDisplays();

		AbandonQuestButton.onClick.RemoveAllListeners();
		AbandonQuestButton.onClick.AddListener(() => 
		{
			abandonCallback(this);
		});

		FullViewButton.onClick.RemoveAllListeners();
		FullViewButton.onClick.AddListener(() =>
		{
			fullViewCallback(this);
		});
	}

	private void UpdateEntityDisplays()
	{
		for (int i = 0; i < CurrentQuestData.ActiveCharacters.Length; i++)
		{
			string characterName = CurrentQuestData.ActiveCharacters[i];
			CharacterData characterData = CharacterDataManager.Instance.LocalData[characterName];

			CharacterOverviewDisplays[i].CharacterName.text = characterName;
			CharacterOverviewDisplays[i].HP.text = (characterData.CurrentHealthPoints / characterData.MaxHealthPoints).ToString("0%");
			CharacterOverviewDisplays[i].XP.text = "0";

			CharacterEntityDisplays[i].EntityName.text = characterName;
			CharacterEntityDisplays[i].HP.text = (characterData.CurrentHealthPoints / characterData.MaxHealthPoints).ToString("0%");
			CharacterEntityDisplays[i].MP.text = "0";
			CharacterEntityDisplays[i].XP.text = "0";
			CharacterEntityDisplays[i].SwingTimerText.text = (characterData.SwingTimer / characterData.AttackSpeed).ToString("0%");
			CharacterEntityDisplays[i].transform.parent.gameObject.SetActive(true);
		}
	}

	public void DisplayOverviewInfo()
	{
		DetailsObjects.ForEach(o => { o.SetActive(false); });
		OverviewObjects.ForEach(o => { o.SetActive(true); });
	}

	public void DisplayFullQuestInfo()
	{
		OverviewObjects.ForEach(o => { o.SetActive(false); });
		DetailsObjects.ForEach(o => { o.SetActive(true); });
	}
}
