using UnityEngine;

/// <summary>
/// Central UI entry point for overlay messages (e.g., transient toasts).
/// Provides a simple hotkey (T) to spawn a message for quick testing.
/// </summary>
public class GameInterfaceController : Singleton<GameInterfaceController>
{
	[SerializeField] private Canvas overlayCanvas;

	[SerializeField] private GameObject overlayMessagePrefab;

	[SerializeField] private InventoryInterface inventoryInterface;

	void Update()
	{
		if (Input.GetKeyDown(KeyCode.I))
		{
			inventoryInterface.OpenInventory();
		}
	}

	/// <summary>
	/// Instantiate an overlay message as a child of the overlay canvas.
	/// </summary>
	public void CreateOverlayMessage()
	{
		OverlayMessage newMessage = Instantiate(overlayMessagePrefab, overlayCanvas.transform).GetComponent<OverlayMessage>();
	}
}
