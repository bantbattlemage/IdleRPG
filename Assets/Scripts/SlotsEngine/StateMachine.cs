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

public class StateMachine : Singleton<StateMachine>
{
	public State CurrentState => currentState;
	private State currentState;

	private Dictionary<State, GameState> states = new Dictionary<State, GameState>();

	void Awake()
	{
		Application.runInBackground = true;

		RegisterState(State.Init);
		RegisterState(State.Idle);
		RegisterState(State.SpinPurchased);
		RegisterState(State.Spinning);
		RegisterState(State.Presentation);

		EventManager.Instance.RegisterEvent("InitEnter", OnInitEnter);
	}

	public void BeginStateMachine()
	{
		currentState = State.Init;
		states[currentState].EnterState();
	}

	private void OnInitEnter(object obj)
	{
		SlotsEngine.Instance.InitializeSlotsEngine();
		SlotConsoleController.Instance.InitializeConsole();
		GamePlayer.Instance.InitializePlayer();

		SetState(State.Idle);
	}

	private void RegisterState(State state, Action enterAction = null, Action exitAction = null)
	{
		GameState newState = new GameState(state, enterAction, exitAction);

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

	public GameState(State s, Action enter, Action exit)
	{
		state = s;
		enterAction = enter;
		exitAction = exit;
	}

	public void EnterState()
	{
		EventManager.Instance.BroadcastEvent($"{state.ToString()}Enter");
		if (enterAction != null) enterAction();
	}

	public void ExitState()
	{
		EventManager.Instance.BroadcastEvent($"{state.ToString()}Exit");
		if (exitAction != null) exitAction();
	}
}