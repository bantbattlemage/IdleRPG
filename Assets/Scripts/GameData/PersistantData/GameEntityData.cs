using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static EnumDefinitions;

[Serializable]
public class GameEntityData : IGameEntityData
{
	public string Name;
	public int Level;

	public int MaxHealthPoints;
	public int CurrentHealthPoints;
	public int HealthRegenPerSecond;

	public int EntityId;

	/// <summary>
	/// Quest accessor id that this entity is assigned to
	/// </summary>
	public int ActiveQuestId;

	/// <summary>
	/// time in seconds for an attack to complete
	/// </summary>
	public float AttackSpeed;

	/// <summary>
	/// time since last attack
	/// </summary>
	public float SwingTimer;

	public GameEntityData()
	{
		InitializeBasicValues();
	}

	protected virtual void InitializeBasicValues()
	{
		Level = 1;
		MaxHealthPoints = 100;
		CurrentHealthPoints = MaxHealthPoints;
		HealthRegenPerSecond = 10;
		AttackSpeed = 1;
	}

	/// <summary>
	/// Progresses the entity's swing timer for the current attack using the time since the last frame.
	/// </summary>
	public virtual void IterateSwingTimer(float timeElapsed, List<IGameEntityData> targets)
	{
		SwingTimer += timeElapsed;

		if (SwingTimer >= AttackSpeed)
		{
			PerformBasicAttack(targets);

			float overflow = SwingTimer - AttackSpeed;
			SwingTimer = overflow;
		}
	}

	public virtual void PerformBasicAttack(List<IGameEntityData> targets)
	{
		Debug.Log(Name + " attacks!");
	}
}

public interface IGameEntityData
{
	public void IterateSwingTimer(float timeElapsed, List<IGameEntityData> targets);
	public void PerformBasicAttack(List<IGameEntityData> targets);
}
