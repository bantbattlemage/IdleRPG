using UnityEngine;

/// <summary>
/// Central UI entry point for overlay messages (e.g., transient toasts).
/// </summary>
public class GameInterfaceController : Singleton<GameInterfaceController>
{
	[SerializeField] private Canvas overlayCanvas;

	[SerializeField] private GameObject overlayMessagePrefab;

	[SerializeField] private SlotInventoryInterface inventoryInterface;
	public SlotInventoryInterface InventoryInterface => inventoryInterface;

	// Note: Inventory open shortcut moved to TestTool.cs GUI (toggle with F1)

	/// <summary>
	/// Instantiate an overlay message as a child of the overlay canvas.
	/// </summary>
	public void CreateOverlayMessage()
	{
		OverlayMessage newMessage = Instantiate(overlayMessagePrefab, overlayCanvas.transform).GetComponent<OverlayMessage>();
	}
}
