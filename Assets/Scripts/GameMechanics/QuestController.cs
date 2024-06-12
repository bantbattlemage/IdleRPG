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
		//	get all characters that are on an active quest
		List<CharacterData> characters = CharacterDataManager.Instance.GetAllCharacterData().Where(x => x.ActiveQuestId != 0).ToList();

		foreach (CharacterData character in characters)
		{
			character.IterateSwingTimer(Time.deltaTime);
		}
	}
}
