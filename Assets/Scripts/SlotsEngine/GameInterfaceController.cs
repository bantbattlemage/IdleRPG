using UnityEngine;

public class GameInterfaceController : Singleton<GameInterfaceController>
{
	[SerializeField] private Canvas overlayCanvas;

	[SerializeField] private GameObject overlayMessagePrefab;

	void Update()
	{
		if (Input.GetKeyDown(KeyCode.T))
		{
			CreateOverlayMessage();
		}
	}

	public void CreateOverlayMessage()
	{
		OverlayMessage newMessage = Instantiate(overlayMessagePrefab, overlayCanvas.transform).GetComponent<OverlayMessage>();
	}
}
