using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OverlayPanelGroup : MonoBehaviour
{
	[SerializeField] private TextMeshProUGUI messageText;
	[SerializeField] private Image messageImage;
	[SerializeField] private Button panelButton;

	public void Initialize(OverlayPanelSettings settings)
	{
		if (settings.Image == null)
		{
			messageImage.gameObject.SetActive(false);
		}
		else
		{
			messageImage.sprite = settings.Image;
		}

		if (string.IsNullOrEmpty(settings.Message))
		{
			messageText.gameObject.SetActive(false);
		}
		else
		{
			messageText.text = settings.Message;
		}

		if (settings.ButtonCallback != null)
		{
			panelButton.onClick.AddListener(settings.ButtonCallback);
		}
		else
		{
			panelButton.gameObject.SetActive(false);
		}
	}
}
