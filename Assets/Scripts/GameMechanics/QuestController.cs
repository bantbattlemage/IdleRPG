using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class QuestController : MonoBehaviour
{
	public bool Active = false;

	public static QuestController Instance { get { if (instance == null) instance = FindObjectOfType<QuestController>(); return instance; } private set { instance = value; } }
	private static QuestController instance;

	public void Awake()
	{
		Active = false;
	}

	public void Update() 
	{
        if (!Active)
        {
			return;
        }

		DataPersistenceManager.Instance.LoadGame();

		RunQuestLoopIteration();

		DataPersistenceManager.Instance.SaveGame();
	}

	public void RunQuestLoopIteration()
	{
		List<QuestData> allActiveQuests = QuestDataManager.Instance.GetAllActiveQuests();

		foreach(QuestData questData in allActiveQuests)
		{
			List<CharacterData> characters = CharacterDataManager.Instance.GetAllCharacterData().Where(x => x.ActiveQuestId == questData.AccessorId).ToList();
			List<EnemyData> enemies = EnemyDataManager.Instance.GetAllEnemyData().Where(x => x.ActiveQuestId == questData.AccessorId).ToList();
			List<IGameEntityData> orderedGameEntities = new List<IGameEntityData>();
			orderedGameEntities.AddRange(characters);
			orderedGameEntities.AddRange(enemies);

			foreach (IGameEntityData gameEntity in orderedGameEntities)
			{
				gameEntity.IterateSwingTimer(Time.deltaTime, new List<IGameEntityData>());
			}
		}
	}
}
