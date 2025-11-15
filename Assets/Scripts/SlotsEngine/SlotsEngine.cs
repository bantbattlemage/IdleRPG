using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class SlotsEngine : Singleton<MonoBehaviour>
{
	[SerializeField] private Canvas gameCanvas;
	[SerializeField] private int reelCount; 
	[SerializeField] private GameObject reelPrefab;
	[SerializeField] private ReelDefinition reelDefinition;

	List<GameReel> reels = new List<GameReel>();

	void Start()
	{
		SpawnReels();
	}

	void Update()
	{
		if (Input.GetKeyDown(KeyCode.Space))
		{
			SpinReel();
		}
	}

	private void SpinReel()
	{
		for (int i = 0; i < reels.Count; i++)
		{
			int index = i;

			reels[i].transform.DOLocalMoveY(-500, 1).SetEase(Ease.OutBounce).OnComplete(() =>
			{
				Transform t = reels[index].gameObject.transform;
				t.localPosition = new Vector3(t.localPosition.x, 0, t.localPosition.z);
			});
		}
	}

	private void SpawnReels()
	{
		GameObject reelsGroup = Instantiate(new GameObject(), gameCanvas.transform);
		reelsGroup.name = "ReelsGroup";

		for (int i = 0; i < reelCount; i++)
		{
			GameObject r = Instantiate(reelPrefab, reelsGroup.transform);
			GameReel reel = r.GetComponent<GameReel>();
			reel.InitializeReel(reelDefinition);
			r.transform.localPosition = new Vector3((reelDefinition.ReelsSpacing + reelDefinition.SymbolSize) * i, 0, 0);
			reels.Add(reel);
		}

		reelsGroup.transform.localPosition = new Vector3(-((reelCount-1) * (reelDefinition.ReelsSpacing + reelDefinition.SymbolSize))/2f, -((reelDefinition.SymbolCount-1) * (reelDefinition.SymbolSpacing + reelDefinition.SymbolSize))/2f, 0);
	}
}
