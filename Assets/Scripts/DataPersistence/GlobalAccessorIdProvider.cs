using System.Threading;
using UnityEngine;

/// <summary>
/// Centralized provider for globally-unique integer AccessorIds.
/// - Thread-safe via Interlocked.
/// - Can be initialized from persisted GameData.LastAssignedAccessorId.
/// - Exposes a way for DataManagers to register existing ids so the provider can ensure monotonicity.
/// </summary>
public static class GlobalAccessorIdProvider
{
	private static int lastAssigned = 0;

	/// <summary>
	/// Initialize provider from persisted value. Should be called while loading GameData to resume counter.
	/// If called with a smaller value than currently observed registered ids, the provider will not decrease.
	/// </summary>
	public static void InitializeFromPersisted(int persistedLastAssigned)
	{
		// Ensure we only ever move forward
		int observed = lastAssigned;
		if (persistedLastAssigned > observed)
		{
			Interlocked.Exchange(ref lastAssigned, persistedLastAssigned);
		}
	}

	/// <summary>
	/// Registers an existing accessor id (e.g., from a manager's LocalData) so the provider's counter
	/// advances beyond it if necessary. Use during load to ensure next ids remain unique.
	/// </summary>
	public static void RegisterExistingId(int accessorId)
	{
		if (accessorId <= 0) return;
		int current = lastAssigned;
		while (accessorId > current)
		{
			// attempt to set lastAssigned to accessorId if still unchanged
			int original = Interlocked.CompareExchange(ref lastAssigned, accessorId, current);
			if (original == current) break; // success
			current = lastAssigned; // someone else changed it - re-evaluate
		}
	}

	/// <summary>
	/// Returns the next globally unique AccessorId (monotonic increasing integer).
	/// </summary>
	public static int GetNextId()
	{
		return Interlocked.Increment(ref lastAssigned);
	}

	/// <summary>
	/// Snapshot the current last assigned id (for persistence).
	/// </summary>
	public static int SnapshotLastAssigned() => lastAssigned;
}
