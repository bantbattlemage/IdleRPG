using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OverlayButtonGroup : MonoBehaviour
{
	[SerializeField] private Button button;
	[SerializeField] private TextMeshProUGUI buttonLabel;

	public void Initialize(OverlayButtonSettings settings)
	{
		if (string.IsNullOrEmpty(settings.ButtonLabel))
		{
			buttonLabel.gameObject.SetActive(false);
		}
		else
		{
			buttonLabel.text = settings.ButtonLabel;
		}

		if (settings.ButtonCallback != null)
		{
			button.onClick.AddListener(settings.ButtonCallback);
		}
		else
		{
			button.gameObject.SetActive(false);
		}

		if (settings.ButtonColor != null)
		{
			button.image.color = settings.ButtonColor;
			buttonLabel.color = Color.black;
		}
	}
}
