using UnityEngine;

public class GamePlayer : Singleton<GamePlayer>
{
	[SerializeField] private PlayerDefinition definition;
	public PlayerDefinition PlayerDefinition => definition;

	public BetLevelDefinition CurrentBet => definition.CurrentBet;
	public int CurrentCredits => definition.Credits;

	public void InitializePlayer()
	{
		EventManager.Instance.RegisterEvent("BetUpPressed", OnBetUpPressed);
		EventManager.Instance.RegisterEvent("BetDownPressed", OnBetDownPressed);
		EventManager.Instance.RegisterEvent("SpinButtonPressed", OnPlayerInputPressed);
		EventManager.Instance.RegisterEvent("StopButtonPressed", OnPlayerInputPressed);
		EventManager.Instance.RegisterEvent("PlayerInputPressed", OnPlayerInputPressed);

		if (definition.CurrentBet == null)
		{
			SetCurrentBet(SlotsEngine.Instance.SlotsDefinition.BetLevelDefinitions[0]);
		}
		else
		{
			SetCurrentBet(definition.CurrentBet);
		}

		EventManager.Instance.BroadcastEvent("CreditsChanged", CurrentCredits);
	}

	void Update()
	{
		if (Input.GetKeyDown(KeyCode.Space))
		{
			EventManager.Instance.BroadcastEvent("PlayerInputPressed");
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
		definition.SetCurrentCredits(CurrentCredits + value);
		EventManager.Instance.BroadcastEvent("CreditsChanged", CurrentCredits);
	}

	public void SetCurrentBet(BetLevelDefinition bet)
	{
		if (StateMachine.Instance.CurrentState != State.Idle && StateMachine.Instance.CurrentState != State.Init)
		{
			return;
		}

		definition.SetCurrentBet(bet);
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
