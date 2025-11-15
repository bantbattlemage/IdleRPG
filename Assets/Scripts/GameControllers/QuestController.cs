using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class QuestController : Singleton<QuestController>
{
	[HideInInspector]
	public bool Active = false;

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
			List<CharacterData> characters = CharacterDataManager.Instance.GetAllData().Where(x => x.ActiveQuestId == questData.AccessorId).ToList();
			List<EnemyData> enemies = EnemyDataManager.Instance.GetAllData().Where(x => x.ActiveQuestId == questData.AccessorId).ToList();

			List<IGameEntityData> orderedGameEntities = new List<IGameEntityData>();
			orderedGameEntities.AddRange(characters);
			orderedGameEntities.AddRange(enemies);

			foreach(CharacterData character in characters)
			{
				character.SetCurrentTarget(enemies.GetRandom());
			}
			foreach(EnemyData enemy in enemies)
			{
				enemy.SetCurrentTarget(characters.GetRandom());
			}

			foreach (IGameEntityData gameEntity in orderedGameEntities)
			{
				gameEntity.IterateSwingTimer(Time.deltaTime);
			}
		}
	}
}
