using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ActiveQuestPanel : MonoBehaviour
{
	public int QuestId;

	public void InitializeQuestPanel(int questAccessorId)
	{
		QuestId = questAccessorId;
	}
}
