using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.VisualScripting;
using UnityEditor.U2D.Animation;
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

	private int[] fullViewPopulateOrder = new int[] { 2, 1, 3, 0, 4 };
	private bool fullView = false;

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

		ResetAllDisplays();
		UpdateEntityDisplays();

		AbandonQuestButton.onClick.RemoveAllListeners();
		AbandonQuestButton.onClick.AddListener(() => 
		{
			ResetAllDisplays();
			abandonCallback(this);
		});

		FullViewButton.onClick.RemoveAllListeners();
		FullViewButton.onClick.AddListener(() =>
		{
			ResetAllDisplays();
			fullViewCallback(this);
		});
	}

	private void ResetAllDisplays()
	{
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
			entity.gameObject.SetActive(false);
		}

		foreach (GameEntityView entity in EnemyEntityDisplays)
		{
			entity.EntityName.text = string.Empty;
			entity.HP.text = string.Empty;
			entity.MP.text = string.Empty;
			entity.XP.text = string.Empty;
			entity.SwingTimerText.text = string.Empty;
			entity.gameObject.SetActive(false);
		}
	}

	private void UpdateEntityDisplays()
	{
		int[] populationOrder = new int[] {0, 1, 2, 3, 4 };
		if(fullView)
		{
			populationOrder = fullViewPopulateOrder;
		}

		for (int i = 0; i < CurrentQuestData.ActiveCharacters.Length; i++)
		{
			int displayIndex = populationOrder[i];

			string characterName = CurrentQuestData.ActiveCharacters[i];
			CharacterData characterData = CharacterDataManager.Instance.LocalData[characterName];

			if(!fullView)
			{
				CharacterOverviewDisplays[displayIndex].CharacterName.text = characterName;
				CharacterOverviewDisplays[displayIndex].HP.text = (characterData.CurrentHealthPoints / characterData.MaxHealthPoints).ToString("0%");
				CharacterOverviewDisplays[displayIndex].XP.text = "0";
			}
			else
			{
				UpdateDisplay(CharacterEntityDisplays[displayIndex], characterData);
			}
		}

		var enemyData = EnemyDataManager.Instance.GetAllEnemyData().Where(x => x.ActiveQuestId == CurrentQuestIndexId).ToArray();
		for(int i = 0; i < enemyData.Length; i++)
		{
			int displayIndex = populationOrder[i];

			UpdateDisplay(EnemyEntityDisplays[displayIndex], enemyData[i]);
		}
	}

	public void UpdateDisplay(GameEntityView gameEntityView, CharacterData characterData)
	{
		gameEntityView.EntityName.text = characterData.Name;
		gameEntityView.HP.text = (characterData.CurrentHealthPoints / characterData.MaxHealthPoints).ToString("0%");
		gameEntityView.HealthBar.fillAmount = (characterData.CurrentHealthPoints / characterData.MaxHealthPoints);
		gameEntityView.MP.text = "0";
		gameEntityView.XP.text = "0";
		gameEntityView.SwingTimerText.text = (characterData.SwingTimer / characterData.AttackSpeed).ToString("0%");
		gameEntityView.SwingTimerBar.fillAmount = (characterData.SwingTimer / characterData.AttackSpeed);
		gameEntityView.gameObject.SetActive(true);
	}

	public void UpdateDisplay(GameEntityView gameEntityView, EnemyData enemyData)
	{
		gameEntityView.EntityName.text = enemyData.Name;
		gameEntityView.HP.text = (enemyData.CurrentHealthPoints / enemyData.MaxHealthPoints).ToString("0%");
		gameEntityView.HealthBar.fillAmount = (enemyData.CurrentHealthPoints / enemyData.MaxHealthPoints);
		gameEntityView.MP.text = "0";
		gameEntityView.XP.text = "0";
		gameEntityView.SwingTimerText.text = (enemyData.SwingTimer / enemyData.AttackSpeed).ToString("0%");
		gameEntityView.SwingTimerBar.fillAmount = (enemyData.SwingTimer / enemyData.AttackSpeed);
		gameEntityView.gameObject.SetActive(true);
	}

	public void DisplayOverviewInfo()
	{
		fullView = false;
		DetailsObjects.ForEach(o => { o.SetActive(false); });
		OverviewObjects.ForEach(o => { o.SetActive(true); });
	}

	public void DisplayFullQuestInfo()
	{
		fullView = true;
		OverviewObjects.ForEach(o => { o.SetActive(false); });
		DetailsObjects.ForEach(o => { o.SetActive(true); });
	}
}
