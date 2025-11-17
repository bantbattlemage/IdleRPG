using UnityEngine;

public class GamePlayer : Singleton<GamePlayer>
{
	[SerializeField] private PlayerData playerData;
	public PlayerData PlayerData => playerData;

	public BetLevelDefinition CurrentBet => playerData.CurrentBet;
	public int CurrentCredits => playerData.Credits;

	public void InitializePlayer()
	{
		EventManager.Instance.RegisterEvent("BetUpPressed", OnBetUpPressed);
		EventManager.Instance.RegisterEvent("BetDownPressed", OnBetDownPressed);
		EventManager.Instance.RegisterEvent("SpinButtonPressed", OnPlayerInputPressed);
		EventManager.Instance.RegisterEvent("StopButtonPressed", OnPlayerInputPressed);
		EventManager.Instance.RegisterEvent("PlayerInputPressed", OnPlayerInputPressed);

		playerData = PlayerDataManager.Instance.GetPlayerData();

		if (playerData.CurrentBet == null)
		{
			SetCurrentBet(SlotsEngine.Instance.SlotsDefinition.BetLevelDefinitions[0]);
		}
		else
		{
			SetCurrentBet(playerData.CurrentBet);
		}

		EventManager.Instance.BroadcastEvent("CreditsChanged", CurrentCredits);
	}

	void Update()
	{
		if (Input.GetKeyDown(KeyCode.Space))
		{
			EventManager.Instance.BroadcastEvent("PlayerInputPressed");
		}

		if (Input.GetKeyDown(KeyCode.Alpha1))
		{
			Debug.LogWarning("Adding credits for testing.");
			AddCredits(100);
		}
	}

	private void OnPlayerInputPressed(object obj)
	{
		SlotsEngine.Instance.SpinOrStopReels(RequestSpinPurchase());
	}

	public bool RequestSpinPurchase()
	{
		if (StateMachine.Instance.CurrentState != State.Idle)
		{
			return false;
		}

		if (CurrentCredits < CurrentBet.CreditCost)
		{
			return false;
		}

		AddCredits(-CurrentBet.CreditCost);

		StateMachine.Instance.SetState(State.SpinPurchased);

		return true;
	}

	public void AddCredits(int value)
	{
		playerData.SetCurrentCredits(CurrentCredits + value);
		EventManager.Instance.BroadcastEvent("CreditsChanged", CurrentCredits);
	}

	public void SetCurrentBet(BetLevelDefinition bet)
	{
		if (StateMachine.Instance.CurrentState != State.Idle && StateMachine.Instance.CurrentState != State.Init)
		{
			return;
		}

		playerData.SetCurrentBet(bet);
		EventManager.Instance.BroadcastEvent("BetChanged", bet);
	}

	private void OnBetDownPressed(object obj)
	{
		var betLevels = SlotsEngine.Instance.SlotsDefinition.BetLevelDefinitions;

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
		var betLevels = SlotsEngine.Instance.SlotsDefinition.BetLevelDefinitions;

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
}
