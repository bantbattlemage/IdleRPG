using System.Collections.Generic;
using System;
using UnityEngine;

public enum State
{
	Init,
	Idle,
	Spinning,
	Presentation
}

public class StateMachine : Singleton<StateMachine>
{
	public State CurrentState => currentState;
	private State currentState;

	private Dictionary<State, GameState> states = new Dictionary<State, GameState>();

	void Start()
	{
		RegisterState(State.Init, () => { SetState(State.Idle); });
		RegisterState(State.Idle);
		RegisterState(State.Spinning);
		RegisterState(State.Presentation);

		currentState = State.Init;
		states[State.Init].EnterState();
	}

	private void RegisterState(State state, Action enterAction = null, Action exitAction = null)
	{
		GameState newState = new GameState(state, enterAction, exitAction);

		states.Add(state, newState);
	}

	public void SetState(State state)
	{
		states[currentState].ExitState();
		states[state].EnterState();
		currentState = state;
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