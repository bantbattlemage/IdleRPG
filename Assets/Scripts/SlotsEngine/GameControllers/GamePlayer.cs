using System.Collections.Generic;
using UnityEngine;

public class GamePlayer : Singleton<GamePlayer>
{
	[SerializeField] private PlayerData playerData;
	public PlayerData PlayerData => playerData;

	public BetLevelDefinition CurrentBet => playerData.CurrentBet;
	public int CurrentCredits => playerData.Credits;

	private List<SlotsEngine> playerSlots = new List<SlotsEngine>();

	private SlotsEngine primarySlots => playerSlots[0];

	public bool CheckAllSlotsState(State state)
	{
		return playerSlots.TrueForAll(x => x.CurrentState == state);
	}

	private void SetAllSlotsState(State state)
	{
		playerSlots.ForEach(x => x.SetState(state));
	}

	public void InitializePlayer()
	{
		int testSlots = 2;
		for (int i = 0; i < testSlots; i++)
		{
			SlotsEngine slotsEngine = SlotsEngineController.Instance.CreateSlots();
			playerSlots.Add(slotsEngine);
		}

		GlobalEventManager.Instance.RegisterEvent("BetUpPressed", OnBetUpPressed);
		GlobalEventManager.Instance.RegisterEvent("BetDownPressed", OnBetDownPressed);
		GlobalEventManager.Instance.RegisterEvent("SpinButtonPressed", OnPlayerInputPressed);
		GlobalEventManager.Instance.RegisterEvent("StopButtonPressed", OnPlayerInputPressed);
		GlobalEventManager.Instance.RegisterEvent("PlayerInputPressed", OnPlayerInputPressed);

		playerData = PlayerDataManager.Instance.GetPlayerData();
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
		foreach (SlotsEngine slots in playerSlots)
		{
			slots.BeginSlots();
		}

		SetCurrentBet(playerData.CurrentBet);
		GlobalEventManager.Instance.BroadcastEvent("CreditsChanged", CurrentCredits);
	}

	public bool RequestSpinPurchase()
	{
		if (!CheckAllSlotsState(State.Idle))
		{
			return false;
		}

		if (CurrentCredits < CurrentBet.CreditCost)
		{
			return false;
		}

		AddCredits(-CurrentBet.CreditCost);

		SetAllSlotsState(State.SpinPurchased);

		return true;
	}

	public void AddCredits(int value)
	{
		playerData.SetCurrentCredits(CurrentCredits + value);
		GlobalEventManager.Instance.BroadcastEvent("CreditsChanged", CurrentCredits);
	}

	public void SetCurrentBet(BetLevelDefinition bet)
	{
		if (!CheckAllSlotsState(State.Idle))
		{
			return;
		}

		playerData.SetCurrentBet(bet);
		GlobalEventManager.Instance.BroadcastEvent("BetChanged", bet);
	}

	private void OnBetDownPressed(object obj)
	{
		var betLevels = primarySlots.SlotsDefinition.BetLevelDefinitions;

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
		var betLevels = primarySlots.SlotsDefinition.BetLevelDefinitions;

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
		bool spinPurchase = RequestSpinPurchase();

		foreach (SlotsEngine slots in playerSlots)
		{
			slots.SpinOrStopReels(spinPurchase);
		}
	}
}
