using System;
using UnityEngine;

public interface IEventTriggerScript
{
	// Execute custom presentation/action when the symbol is awarded.
	// Must invoke onComplete when finished so slot presentation can continue.
	void Execute(WinData winData, Action onComplete);
	
	// Optional overload that accepts a SlotsEngine for additional context
	void Execute(WinData winData, Action onComplete, SlotsEngine slotsEngine)
	{
		// Default implementation delegates to the simpler overload for backwards compatibility
		Execute(winData, onComplete);
	}
}
