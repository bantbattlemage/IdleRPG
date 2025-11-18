public class BetLevelDataManager : DataManager<BetLevelDataManager, BetLevelData>
{
	public override void LoadData(GameData persistantData)
	{
		LocalData = persistantData.BetLevelData;
	}

	public override void SaveData(GameData persistantData)
	{
		persistantData.BetLevelData = LocalData;
	}
}