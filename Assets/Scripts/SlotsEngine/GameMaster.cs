using UnityEngine;

public class GameMaster : Singleton<GameMaster>
{
	[SerializeField] private GamePlayer player;
	public GamePlayer Player => player;

	private StateMachine stateMachine;

	void Awake()
	{
		Application.runInBackground = true;

		stateMachine = new StateMachine();
	}

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
