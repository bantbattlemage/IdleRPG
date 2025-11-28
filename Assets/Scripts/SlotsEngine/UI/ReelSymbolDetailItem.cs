using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class ReelSymbolDetailItem : MonoBehaviour
{
	[SerializeField] private TMP_Text nameText;
	[SerializeField] private TMP_Text metaText;
	[SerializeField] private Button MenuButton;
	[SerializeField] private Image symbolImage;

	private ReelStripData cachedStrip;
	private SlotsData cachedSlot;
	private System.Action<ReelStripData, SlotsData> openMenuCallback;

	public void Setup(SymbolData data, int index)
	{
		// Diagnostic: report incoming data
		Debug.Log($"ReelSymbolDetailItem.Setup(SymbolData) index={index} name={(data != null ? data.Name : "<null>")} hasSprite={(data != null && data.Sprite != null)}");

		Sprite sprite = null;
		if (data != null)
		{
			// Only use the explicit runtime sprite (resolved via spriteKey) — do not attempt to resolve by name
			sprite = data.Sprite; // getter resolves from spriteKey if present
		}

		// If we have symbol data and a sprite, show the sprite and hide the text.
		if (data != null && symbolImage != null && sprite != null)
		{
			symbolImage.enabled = true;
			symbolImage.sprite = sprite;
			// when displaying a sprite we want neutral tint so sprite colors are correct
			symbolImage.color = Color.white;
			if (nameText != null)
			{
				nameText.enabled = false;
				nameText.text = string.Empty; // clear residual name
			}
		}
		else
		{
			// No associated symbol data sprite: show the "Random" text (or name if available) and keep the image's color as a placeholder.
			if (nameText != null)
			{
				nameText.enabled = true;
				nameText.text = (data != null && !string.IsNullOrEmpty(data.Name) ? data.Name : "Random") + $" [{index}]";
			}
			if (symbolImage != null)
			{
				// Keep the image visible so its configured color acts as the placeholder background; clear any sprite used for symbol display.
				symbolImage.enabled = true;
				symbolImage.sprite = null;
				// preserve symbolImage.color so authoring color remains for the placeholder
			}
		}

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
		// Diagnostic
		Debug.Log($"ReelSymbolDetailItem.Setup(SymbolDefinition) index={index} name={(def != null ? def.SymbolName : "<null>")} hasSprite={(def != null && def.SymbolSprite != null)}");

		Sprite sprite = null;
		if (def != null)
		{
			// Only use the explicit authoring sprite if present. Do not resolve by definition name.
			sprite = def.SymbolSprite;
		}

		if (def != null && symbolImage != null && sprite != null)
		{
			symbolImage.enabled = true;
			symbolImage.sprite = sprite;
			// when displaying a sprite we want neutral tint so sprite colors are correct
			symbolImage.color = Color.white;
			if (nameText != null)
			{
				nameText.enabled = false;
				nameText.text = string.Empty; // clear residual name
			}
		}
		else
		{
			// No associated definition: show the "Random" text (or symbol name if available) and keep image color as placeholder.
			if (nameText != null)
			{
				nameText.enabled = true;
				nameText.text = (def != null && !string.IsNullOrEmpty(def.SymbolName) ? def.SymbolName : "Random") + $" [{index}]";
			}
			if (symbolImage != null)
			{
				symbolImage.enabled = true;
				symbolImage.sprite = null;
				// preserve symbolImage.color so authoring color remains for the placeholder
			}
		}

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

	public void DisableMenuInteraction()
	{
		// Remove callbacks and disable interaction, but preserve full visual appearance (no dimming)
		if (MenuButton != null)
		{
			MenuButton.onClick.RemoveAllListeners();

			// Adjust the ColorBlock so the disabledColor matches the normal color to avoid Unity's dimming.
			try
			{
				var cb = MenuButton.colors;
				cb.disabledColor = cb.normalColor;
				cb.colorMultiplier = 1f;
				MenuButton.colors = cb;
			}
			catch { }

			// force non-interactable so clicks do nothing
			MenuButton.interactable = false;
		}

		// Ensure the symbol itself is displayed with full brightness
		if (symbolImage != null)
		{
			symbolImage.color = Color.white;
			symbolImage.enabled = true;
		}
		if (nameText != null)
		{
			// keep name text enabled/disabled as configured by Setup; do not dim
			nameText.enabled = nameText.enabled;
		}
	}
}
