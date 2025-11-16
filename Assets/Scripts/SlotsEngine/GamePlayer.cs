using UnityEngine;

public class GamePlayer : Singleton<GamePlayer>
{
	[SerializeField] private PlayerDefinition definition;
	public PlayerDefinition PlayerDefinition => definition;

	public void InitializePlayer()
	{
		EventManager.Instance.RegisterEvent("BetUpPressed", OnBetUpPressed);
		EventManager.Instance.RegisterEvent("BetDownPressed", OnBetDownPressed);

		if (definition.CurrentBet == null)
		{
			SetCurrentBet(SlotsEngine.Instance.SlotsDefinition.BetLevelDefinitions[0]);
		}
		else
		{
			SetCurrentBet(definition.CurrentBet);
		}
	}

	void Update()
	{
		if (Input.GetKeyDown(KeyCode.Space))
		{
			EventManager.Instance.BroadcastEvent("PlayerInputPressed");
		}
	}

	public BetLevelDefinition CurrentBet
	{
		get
		{
			return definition.CurrentBet;
		}
	}

	public void SetCurrentBet(BetLevelDefinition bet)
	{
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
