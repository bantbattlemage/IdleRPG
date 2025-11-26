using System;
using UnityEngine;

public interface IEventTriggerScript
{
	// Execute custom presentation/action when the symbol is awarded.
	// Must invoke onComplete when finished so slot presentation can continue.
	void Execute(WinData winData, Action onComplete);
}
