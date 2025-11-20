using System;

public static class EventKey
{
	public static string Compose(Enum baseEnum, string suffix = null)
	{
		if (baseEnum == null) throw new ArgumentNullException(nameof(baseEnum));
		string basePart = baseEnum.GetType().Name + "." + baseEnum.ToString();
		if (string.IsNullOrEmpty(suffix)) return basePart;
		return basePart + "." + suffix;
	}
}
