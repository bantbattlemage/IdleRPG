using UnityEngine;
using DG.Tweening;

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

		// Initialize DOTween early and pre-allocate tweens/sequences capacity to avoid runtime growth
		try
		{
			DOTween.Init();
			// Pre-size tweeners and sequences to reduce per-frame work in DOTweenComponent.Update()
			// Values chosen conservatively for many-slots scenarios; adjust if your game needs more.
			DOTween.SetTweensCapacity(2048, 512);
		}
		catch (System.Exception ex)
		{
			Debug.LogWarning($"DOTween initialization failed: {ex.Message}");
		}
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

		// Pass the newGame flag so the player knows whether to spawn default slots when none exist
		player.InitializePlayer(defaultBetLevel, newGame);
		player.BeginGame();
	}
}
