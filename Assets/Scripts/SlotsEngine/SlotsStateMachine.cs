
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

		eventManager.RegisterEvent("InitEnter", OnInitEnter);

		currentState = State.Init;
		states[currentState].EnterState();
	}

	private void OnInitEnter(object obj)
	{
		SlotConsoleController.Instance.InitializeConsole(eventManager);
		PresentationController.Instance.AddSlotsToPresentation(eventManager, slotsEngine);

		SetState(State.Idle);
	}
}