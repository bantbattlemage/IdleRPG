using UnityEngine;

public class GameMaster : Singleton<GameMaster>
{
	[SerializeField] private GamePlayer player;
	public GamePlayer Player => player;

	[SerializeField] private BetLevelDefinition defaultBetLevel;
	public BetLevelDefinition DefaultBetLevel => defaultBetLevel;

	private StateMachine stateMachine;

	protected override void Awake()
	{
		base.Awake();

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

		SlotConsoleController.Instance.InitializeConsole();

		player.InitializePlayer(defaultBetLevel);
		player.BeginGame();
	}
}
