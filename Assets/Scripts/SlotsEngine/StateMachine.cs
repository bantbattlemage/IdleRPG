using System.Collections.Generic;
using System;
using UnityEngine;

public enum State
{
	//	standard
	None = 0,
	Init = 1,
	Idle = 2,

	//	slots
	SpinPurchased,
	Spinning,
	Presentation,

	//	game master
	MainMenu
}

public class StateMachine
{
	public State CurrentState => currentState;
	protected State currentState;
	protected EventManager eventManager;

	protected Dictionary<State, GameState> states = new Dictionary<State, GameState>();

	public virtual void InitializeStateMachine(EventManager eventManagerToUse)
	{
		eventManager = eventManagerToUse;
	}

	public virtual void BeginStateMachine()
	{
		throw new NotImplementedException();
	}

	protected void RegisterState(State state, Action enterAction = null, Action exitAction = null)
	{
		GameState newState = new GameState(state, eventManager, enterAction, exitAction);

		states.Add(state, newState);
	}

	public virtual void SetState(State state)
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