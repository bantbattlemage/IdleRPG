using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class ReelSymbolDetailItem : MonoBehaviour
{
	[SerializeField] private TMP_Text nameText;
	[SerializeField] private TMP_Text metaText;
	[SerializeField] private Button MenuButton;

	private ReelStripData cachedStrip;
	private SlotsData cachedSlot;
	private System.Action<ReelStripData, SlotsData> openMenuCallback;

	public void Setup(SymbolData data, int index)
	{
		if (nameText != null) nameText.text = (data != null ? data.Name : "(null)") + $" [{index}]";
		if (metaText != null)
		{
			string m = string.Empty;
			if (data != null)
			{
				m += $"Value:{data.BaseValue} ";
				m += $"MinDepth:{data.MinWinDepth} ";
				m += $"MatchGroup:{data.MatchGroupId} ";
			}
			metaText.text = m;
		}
	}

	public void Setup(SymbolDefinition def, int index)
	{
		if (nameText != null) nameText.text = (def != null ? def.SymbolName : "(null)") + $" [{index}]";
		if (metaText != null)
		{
			string m = string.Empty;
			if (def != null)
			{
				m += $"Value:{def.BaseValue} ";
				m += $"MinDepth:{def.MinWinDepth} ";
				m += $"MatchGroup:{def.MatchGroupId} ";
			}
			metaText.text = m;
		}
	}

	/// <summary>
	/// Configure the optional menu button so clicking it will request the AddRemoveSymbolsMenu
	/// be shown with the provided reel-strip and slot context.
	/// </summary>
	public void ConfigureMenu(ReelStripData strip, SlotsData slot, System.Action<ReelStripData, SlotsData> onOpen)
	{
		cachedStrip = strip;
		cachedSlot = slot;
		openMenuCallback = onOpen;
		if (MenuButton != null)
		{
			MenuButton.onClick.RemoveAllListeners();
			MenuButton.onClick.AddListener(() => { openMenuCallback?.Invoke(cachedStrip, cachedSlot); });
		}
	}
}
