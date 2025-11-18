using UnityEngine;

public class GamePlayer : Singleton<GamePlayer>
{
	[SerializeField] private SlotsEngine slotsEngine;
	public SlotsEngine SlotsEngine => slotsEngine;

	[SerializeField] private PlayerData playerData;
	public PlayerData PlayerData => playerData;

	public BetLevelDefinition CurrentBet => playerData.CurrentBet;
	public int CurrentCredits => playerData.Credits;

	public void InitializePlayer()
	{
		slotsEngine = SlotsEngineController.Instance.CreateSlots();

		GlobalEventManager.Instance.RegisterEvent("BetUpPressed", OnBetUpPressed);
		GlobalEventManager.Instance.RegisterEvent("BetDownPressed", OnBetDownPressed);
		GlobalEventManager.Instance.RegisterEvent("SpinButtonPressed", OnPlayerInputPressed);
		GlobalEventManager.Instance.RegisterEvent("StopButtonPressed", OnPlayerInputPressed);
		GlobalEventManager.Instance.RegisterEvent("PlayerInputPressed", OnPlayerInputPressed);

		playerData = PlayerDataManager.Instance.GetPlayerData();

		if (playerData.CurrentBet == null)
		{
			SetCurrentBet(slotsEngine.SlotsDefinition.BetLevelDefinitions[0]);
		}
		else
		{
			SetCurrentBet(playerData.CurrentBet);
		}

		GlobalEventManager.Instance.BroadcastEvent("CreditsChanged", CurrentCredits);
	}

	void Update()
	{
		if (Input.GetKeyDown(KeyCode.Space))
		{
			GlobalEventManager.Instance.BroadcastEvent("PlayerInputPressed");
		}

		if (Input.GetKeyDown(KeyCode.Alpha1))
		{
			Debug.LogWarning("Adding credits for testing.");
			AddCredits(100);
		}

		if (Input.GetKeyDown(KeyCode.Minus))
		{
			Time.timeScale -= 0.1f;
			Debug.LogWarning(Time.timeScale);
		}

		if (Input.GetKeyDown(KeyCode.Equals))
		{
			Time.timeScale += 0.1f;
			Debug.LogWarning(Time.timeScale);
		}
	}

	public void BeginGame()
	{
		slotsEngine.BeginSlots();
	}

	public bool RequestSpinPurchase()
	{
		if (slotsEngine.CurrentState != State.Idle)
		{
			return false;
		}

		if (CurrentCredits < CurrentBet.CreditCost)
		{
			return false;
		}

		AddCredits(-CurrentBet.CreditCost);

		slotsEngine.SetState(State.SpinPurchased);

		return true;
	}

	public void AddCredits(int value)
	{
		playerData.SetCurrentCredits(CurrentCredits + value);
		GlobalEventManager.Instance.BroadcastEvent("CreditsChanged", CurrentCredits);
	}

	public void SetCurrentBet(BetLevelDefinition bet)
	{
		if (slotsEngine.CurrentState != State.Idle && slotsEngine.CurrentState != State.Init)
		{
			return;
		}

		playerData.SetCurrentBet(bet);
		GlobalEventManager.Instance.BroadcastEvent("BetChanged", bet);
	}

	private void OnBetDownPressed(object obj)
	{
		var betLevels = slotsEngine.SlotsDefinition.BetLevelDefinitions;

		int targetLevel = -1;
		for (int i = 0; i < betLevels.Length; i++)
		{
			if (CurrentBet == betLevels[i] && i - 1 >= 0)
			{
				targetLevel = i - 1;
				break;
			}
		}

		if (targetLevel == -1)
		{
			return;
		}

		SetCurrentBet(betLevels[targetLevel]);
	}

	private void OnBetUpPressed(object obj)
	{
		var betLevels = slotsEngine.SlotsDefinition.BetLevelDefinitions;

		int targetLevel = -1;
		for (int i = 0; i < betLevels.Length; i++)
		{
			if (CurrentBet == betLevels[i] && i + 1 < betLevels.Length)
			{
				targetLevel = i + 1;
				break;
			}
		}

		if (targetLevel == -1)
		{
			return;
		}

		SetCurrentBet(betLevels[targetLevel]);
	}
	private void OnPlayerInputPressed(object obj)
	{
		slotsEngine.SpinOrStopReels(RequestSpinPurchase());
	}
}
