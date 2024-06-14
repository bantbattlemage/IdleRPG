using System.Linq;

public class CharacterDataManager : DataManager<CharacterDataManager, CharacterData>
{
	public override void LoadData(GameData persistantData)
	{
		LocalData = persistantData.CurrentCharacterData;
	}

	public override void SaveData(GameData persistantData)
	{
		persistantData.CurrentCharacterData = LocalData;
	}

	public CharacterData GetCharacterData(string name)
	{
		return LocalData.Values.Where(x => x.Name == name).FirstOrDefault();
	}

	public bool CreateNewCharacter(string name, GameClassEnum classType)
	{
		DataPersistenceManager.Instance.LoadGame();

		CharacterData characterData = new CharacterData();
		characterData.Name = name;
		characterData.CharacterClass = classType;
		AddNewData(characterData);

		DataPersistenceManager.Instance.SaveGame();

		return true;
	}
}