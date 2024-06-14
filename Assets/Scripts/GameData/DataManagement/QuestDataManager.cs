using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using System.Linq;
using Random = UnityEngine.Random;

public class QuestDataManager : DataManager<QuestDataManager, QuestData>
{
	public Dictionary<string, QuestDataObject> QuestDataObjects;

	void Start()
	{
		QuestDataObjects = new Dictionary<string, QuestDataObject>();

		var loadedQuests = Resources.LoadAll<QuestDataObject>("GameData/Quests");

		foreach (QuestDataObject obj in loadedQuests)
		{
			QuestDataObjects.Add(obj.name, obj);
		}
	}

	public override void LoadData(GameData persistantData)
	{
		LocalData = persistantData.CurrentQuestData;
	}

	public override void SaveData(GameData persistantData)
	{
		persistantData.CurrentQuestData = LocalData;
	}

	public List<QuestData> GetAllActiveQuests(bool active = true) 
	{
		return LocalData.Values.Where(x => x.Active == active).ToList();
	}

	public QuestData AddNewQuest(QuestDataObject questDefinition)
	{
		DataPersistenceManager.Instance.LoadGame();

		int newQuestId = GenerateUniqueAccessorId(LocalData.Keys.ToList());
		QuestData newQuest = new QuestData(questDefinition, newQuestId);
		LocalData.Add(newQuestId, newQuest);

		DataPersistenceManager.Instance.SaveGame();

		return newQuest;
	}

	public QuestData AddRandomNewQuest()
	{
		int randomIndex = Random.Range(0, QuestDataObjects.Count);

		return AddNewQuest(QuestDataObjects.ToArray()[randomIndex].Value);
	}

	public void ActivateQuest(int questToActivate, List<string> characterNames)
	{
		DataPersistenceManager.Instance.LoadGame();

		LocalData[questToActivate].ActiveCharacters = characterNames.ToArray();

		foreach (string characterName in characterNames) 
		{
			CharacterDataManager.Instance.GetCharacterData(characterName).ActiveQuestId = questToActivate;
		}

		int enemiesToSpawn = Random.Range(1, LocalData[questToActivate].GetBaseQuestData().PartySize);
		for (int i = 0; i < enemiesToSpawn; i++)
		{
			SpawnNewEnemy(questToActivate);
		}

		LocalData[questToActivate].Active = true;

		DataPersistenceManager.Instance.SaveGame();
	}

	public void SpawnNewEnemy(int quest)
	{
		QuestDataObject baseQuestdata = LocalData[quest].GetBaseQuestData();
		EnemyDataManager.Instance.AddNewEnemy(baseQuestdata.Enemies[Random.Range(0, baseQuestdata.Enemies.Length)], quest);
	}
}
