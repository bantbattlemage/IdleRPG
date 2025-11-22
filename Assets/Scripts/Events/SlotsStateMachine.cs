using System;

/// <summary>
/// Slot-specific state machine that owns its own `EventManager` and registers the set of states a slot uses.
/// Initializes console and presentation handlers upon entering Init, then transitions to Idle.
/// </summary>
public class SlotsStateMachine : StateMachine
{
	private SlotsEngine slotsEngine;

	public void InitializeStateMachine(SlotsEngine parentSlots, EventManager slotsEventManager)
	{
		eventManager = slotsEventManager;
		slotsEngine = parentSlots;
	}

	public override void BeginStateMachine()
	{
		RegisterState(State.Init);
		RegisterState(State.Idle);
		RegisterState(State.SpinPurchased);
		RegisterState(State.Spinning);
		RegisterState(State.Presentation);

		// register Init enter using enum+suffix so it matches BroadcastEvent in GameState.EnterState
		eventManager.RegisterEvent(State.Init, "Enter", OnInitEnter);

		currentState = State.Init;
		states[currentState].EnterState();
	}

	private void OnInitEnter(object obj)
	{
		SlotConsoleController.Instance.RegisterSlotsToConsole(eventManager);
		PresentationController.Instance.AddSlotsToPresentation(eventManager, slotsEngine);

		SetState(State.Idle);
	}
}