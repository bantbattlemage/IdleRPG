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

/// <summary>
/// Minimal state machine that maps enum states to `GameState` holders and broadcasts enter/exit events via an `EventManager`.
/// Consumers should derive and call `RegisterState` per state they intend to use, then drive transitions via `SetState`.
/// </summary>
public class StateMachine
{
	public State CurrentState => currentState;
	protected State currentState;
	protected EventManager eventManager;

	protected Dictionary<State, GameState> states = new Dictionary<State, GameState>();

	/// <summary>
	/// Assign the `EventManager` used to broadcast state enter/exit.
	/// </summary>
	public virtual void InitializeStateMachine(EventManager eventManagerToUse)
	{
		eventManager = eventManagerToUse;
	}

	/// <summary>
	/// Called by derived classes to kick off the first state; base throws to ensure override.
	/// </summary>
	public virtual void BeginStateMachine()
	{
		throw new NotImplementedException();
	}

	/// <summary>
	/// Registers a state with optional enter/exit callbacks.
	/// </summary>
	protected void RegisterState(State state, Action enterAction = null, Action exitAction = null)
	{
		GameState newState = new GameState(state, eventManager, enterAction, exitAction);

		states.Add(state, newState);
	}

	/// <summary>
	/// Transition to the provided state, broadcasting Exit/Enter events.
	/// </summary>
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

/// <summary>
/// Holds enter/exit callbacks for a specific state and is responsible for broadcasting state lifecycle events.
/// </summary>
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
		eventManager.BroadcastEvent(state, "Enter");
		if (enterAction != null) enterAction();
	}

	public void ExitState()
	{
		eventManager.BroadcastEvent(state, "Exit");
		if (exitAction != null) exitAction();
	}
}