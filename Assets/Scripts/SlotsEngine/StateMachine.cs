using System.Collections.Generic;
using System;
using UnityEngine;

public enum State
{
	None = 0,
	Init,
	Idle,
	SpinPurchased,
	Spinning,
	Presentation
}

public class StateMachine
{
	public State CurrentState => currentState;
	private State currentState;

	private Dictionary<State, GameState> states = new Dictionary<State, GameState>();
	private EventManager eventManager;
	private SlotsEngine slotsEngine;

	void Awake()
	{
		Application.runInBackground = true;
	}

	public void InitializeStateMachine(SlotsEngine parentSlots, EventManager slotsEventManager)
	{
		eventManager = slotsEventManager;
		slotsEngine = parentSlots;
	}

	public void BeginStateMachine()
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
		GamePlayer.Instance.InitializePlayer();
		PresentationController.Instance.InitializeWinPresentation(eventManager, slotsEngine);

		SetState(State.Idle);
	}

	private void RegisterState(State state, Action enterAction = null, Action exitAction = null)
	{
		GameState newState = new GameState(state, eventManager, enterAction, exitAction);

		states.Add(state, newState);
	}

	public void SetState(State state)
	{
		if (currentState == State.None)
		{
			throw new Exception("StateMachine not yet initialized!");
		}

		states[currentState].ExitState();
		currentState = state;
		states[state].EnterState();
	}
}

public class GameState
{
	private State state;
	private Action enterAction;
	private Action exitAction;

	private EventManager eventManager;

	public GameState(State s, EventManager slotsEventManager, Action enter, Action exit)
	{
		state = s;
		enterAction = enter;
		exitAction = exit;
		eventManager = slotsEventManager;
	}

	public void EnterState()
	{
		eventManager.BroadcastEvent($"{state.ToString()}Enter");
		if (enterAction != null) enterAction();
	}

	public void ExitState()
	{
		eventManager.BroadcastEvent($"{state.ToString()}Exit");
		if (exitAction != null) exitAction();
	}
}