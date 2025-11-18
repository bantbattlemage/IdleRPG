public class ReelStripDataManager : DataManager<ReelStripDataManager, ReelStripData>
{
	public override void LoadData(GameData persistantData)
	{
		LocalData = persistantData.CurrentReelStripData;
	}

	public override void SaveData(GameData persistantData)
	{
		persistantData.CurrentReelStripData = LocalData;
	}
}