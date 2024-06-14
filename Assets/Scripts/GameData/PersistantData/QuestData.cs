using System;

[Serializable]
public class QuestData : Data
{
	public string QuestDataObjectAccessor;
	public bool Active;
	public string[] ActiveCharacters;

	public QuestData(QuestDataObject questDataObject, int questAccessorId)
	{
		QuestDataObjectAccessor = questDataObject.name;
		Active = false;
		ActiveCharacters = new string[0];
		AccessorId = questAccessorId;
	}

	public QuestDataObject GetBaseQuestData()
	{
		return QuestDataManager.Instance.QuestDataObjects[QuestDataObjectAccessor];
	}
}
