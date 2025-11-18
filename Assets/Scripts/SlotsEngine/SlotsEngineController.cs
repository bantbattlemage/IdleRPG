using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class SlotsEngineController : Singleton<SlotsEngineController>
{
	[SerializeField] private RectTransform reelsCanvasGroup;
	[SerializeField] private GameObject slotsEnginePrefab;
	[SerializeField] private GameObject reelsGroupPrefab;

	private List<SlotsEngine> slotsEngines = new();

	public SlotsEngine CreateSlots()
	{
		SlotsEngine newSlots = Instantiate(slotsEnginePrefab, transform).GetComponent<SlotsEngine>();
		GameObject newReelsGroup = Instantiate(reelsGroupPrefab, reelsCanvasGroup);
		newSlots.InitializeSlotsEngine(newReelsGroup.transform);

		return newSlots;
	}
}
