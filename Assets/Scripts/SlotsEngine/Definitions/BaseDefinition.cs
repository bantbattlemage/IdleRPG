using UnityEngine;

/// <summary>
/// Generic base for ScriptableObject definitions that can produce runtime data instances.
/// Subclasses should override CreateInstance() and optionally override InitializeDefaults() to populate default asset data.
/// </summary>
public abstract class BaseDefinition<TData> : ScriptableObject
{
	/// <summary>
	/// Create a runtime data instance from this definition (must be implemented).
	/// </summary>
	public abstract TData CreateInstance();

	/// <summary>
	/// Optional hook called immediately after the asset is created by the editor tool.
	/// Override to set sensible defaults on the ScriptableObject asset.
	/// Default implementation does nothing.
	/// </summary>
	public virtual void InitializeDefaults() { }
}