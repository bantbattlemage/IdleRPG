using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using System.Linq;
using Random = UnityEngine.Random;

public class QuestDataManager : MonoBehaviour, IDataPersistence
{
	public SerializableDictionary<int, QuestData> LocalData;
	public Dictionary<string, QuestDataObject> QuestDataObjects;

	public static QuestDataManager Instance { get { if (instance == null) instance = FindObjectOfType<QuestDataManager>(); return instance; } private set { instance = value; } }
	private static QuestDataManager instance;

	void Start()
	{
		QuestDataObjects = new Dictionary<string, QuestDataObject>();

		var loadedQuests = Resources.LoadAll<QuestDataObject>("GameData/Quests");

		foreach (QuestDataObject obj in loadedQuests)
		{
			QuestDataObjects.Add(obj.name, obj);
		}
	}

	public void LoadData(GameData persistantData)
	{
		LocalData = persistantData.CurrentQuestData;
	}

	public void SaveData(GameData persistantData)
	{
		persistantData.CurrentQuestData = LocalData;
	}

	public QuestData AddNewQuest(QuestDataObject questDefinition)
	{
		DataPersistenceManager.Instance.LoadGame();

		int newQuestId = Random.Range(0, int.MaxValue);

		while (LocalData.Keys.Contains(newQuestId))
		{
			newQuestId = Random.Range(0, int.MaxValue);
		}

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

	public void ActivateQuest(int questToActivate, List<CharacterData> characters)
	{
		DataPersistenceManager.Instance.LoadGame();

		LocalData[questToActivate].Active = true;
		LocalData[questToActivate].ActiveCharacters = characters.Select(c => c.Name).ToArray();

		DataPersistenceManager.Instance.SaveGame();
	}
}
