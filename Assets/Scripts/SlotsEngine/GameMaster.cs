using UnityEngine;

public class GameMaster : Singleton<GameMaster>
{
	[SerializeField] private GamePlayer player;
	public GamePlayer Player => player;

	public void BeginGame(bool newGame = true)
	{
		if (newGame)
		{
			DataPersistenceManager.Instance.NewGame();
		}
		else
		{
			DataPersistenceManager.Instance.LoadGame();
		}

		player.InitializePlayer();
		player.BeginGame();
	}
}
