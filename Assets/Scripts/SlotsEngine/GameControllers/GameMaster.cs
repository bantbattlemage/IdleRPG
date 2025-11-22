using UnityEngine;

/// <summary>
/// Game-level coordinator responsible for player setup and starting a new or continued session.
/// Ensures the persistence system is initialized, the console is ready, and the player is initialized with a default bet.
/// </summary>
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

	/// <summary>
	/// Begin a game session. When <paramref name="newGame"/> is true a new profile is created, otherwise the most recent profile is loaded.
	/// Initializes UI console and the player using the configured <see cref="defaultBetLevel"/>.
	/// </summary>
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
