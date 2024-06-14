using System;
using System.Collections.Generic;
using UnityEngine;


[Serializable]
public class GameEntityData : Data, IGameEntityData
{
	public string Name;
	public int Level;

	public int MaxHealthPoints;
	public int CurrentHealthPoints;
	public int HealthRegenPerSecond;

	public int MaxManaPoints;
	public int CurrentManaPoints;
	public int ManaRegenPerSecond;

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

	/// <summary>
	/// Target to attack. Stored as a string because Characters use name as ID. Converted to Int for enemies.
	/// </summary>
	public string CurrentTargetId;

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

		MaxManaPoints = 100;
		CurrentManaPoints = MaxManaPoints;
		ManaRegenPerSecond = 10;

		AttackSpeed = 1;
	}

	/// <summary>
	/// Set the current target to a player character. Takes the characters name, which is used to reference the character data.
	/// </summary>
	public virtual void SetCurrentTarget(CharacterData target)
	{
		CurrentTargetId = target.Name;
	}

	/// <summary>
	/// Sets the current target to an enemy. Takes the enemies AccessorID, which is used to reference it's data.
	/// </summary>
	public virtual void SetCurrentTarget(EnemyData target)
	{
		CurrentTargetId = target.EntityId.ToString();
	}

	/// <summary>
	/// Progresses the entity's swing timer for the current attack using the time since the last frame.
	/// </summary>
	public virtual void IterateSwingTimer(float timeElapsed)
	{
		SwingTimer += timeElapsed;

		if (SwingTimer >= AttackSpeed)
		{
			PerformBasicAttack(new List<string>() { CurrentTargetId });

			float overflow = SwingTimer - AttackSpeed;
			SwingTimer = overflow;
		}
	}

	public virtual void PerformBasicAttack(List<string> targets)
	{
		foreach (string target in targets)
		{
			string targetName;

			//	value is an int, the target is an enemy
			try
			{
				int value = Convert.ToInt32(target);
				EnemyData targetEnemy = EnemyDataManager.Instance.GetData(value);
				targetName = targetEnemy.Name;
			}
			//	value is a string, the target is a character
			catch
			{
				CharacterData characterData = CharacterDataManager.Instance.GetCharacterData(target);
				targetName = characterData.Name;
			}

			Debug.Log(string.Format("{0} attacks {1}!", Name, targetName));
		}
	}
}

public interface IGameEntityData
{
	public void IterateSwingTimer(float timeElapsed);
	public void PerformBasicAttack(List<string> targets);
	public void SetCurrentTarget(CharacterData target);
	public void SetCurrentTarget(EnemyData target);
}
