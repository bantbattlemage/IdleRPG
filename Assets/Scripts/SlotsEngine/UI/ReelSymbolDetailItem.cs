using UnityEngine;
using TMPro;

public class ReelSymbolDetailItem : MonoBehaviour
{
	[SerializeField] private TMP_Text nameText;
	[SerializeField] private TMP_Text metaText;

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
}
