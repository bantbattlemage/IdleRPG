using DG.Tweening;
using UnityEngine;

public static class DOTweenBoing
{
	public static Tweener DOBoing(this Transform target, Vector3 direction, float strength = 1f, float duration = 0.8f, float flatness = 0f)
	{
		Vector3 startPos = target.localPosition;
		flatness = Mathf.Clamp01(flatness);

		return DOTween.To(() => 0f, t =>
			{
				float angle = t * 4f * Mathf.PI;
				float envelope = 1f - Mathf.Pow(2f * t - 1f, 4f);
				float raw = Mathf.Sin(angle - Mathf.PI / 2f);
				float curve = Mathf.Lerp(raw * envelope, 0f, flatness);
				target.localPosition = startPos + direction * curve * strength;

			}, 1f, duration)
			.SetEase(Ease.Linear);
	}

	public static Tweener DOHump(this Transform target,
		Vector3 direction,
		float strength = 1f,
		float duration = 0.8f,
		float flatness = 0f)
	{
		Vector3 startPos = target.localPosition;
		flatness = Mathf.Clamp01(flatness);

		return DOTween.To(() => 0f, t =>
			{
				float hump = Mathf.Sin(t * Mathf.PI);
				float curve = Mathf.Lerp(hump, 0f, flatness);
				target.localPosition = startPos + direction * curve * strength;

			}, 1f, duration)
			.SetEase(Ease.Linear);
	}

	public static Tweener DOPulseUp(this Transform target,
		Vector3 direction,
		float strength = 1f,
		float duration = 0.8f,
		float sharpness = 0.5f,
		float peakPosition = 0.4f)   // NEW
	{
		Vector3 startPos = target.localPosition;

		sharpness = Mathf.Clamp01(sharpness);
		peakPosition = Mathf.Clamp01(peakPosition);

		return DOTween.To(() => 0f, t =>
		{
			// ---- NORMALIZED TO PEAK ------------------------------------------

			// t normalized to [0 .. 1] before the peak
			float tRise = Mathf.Clamp01(t / peakPosition);

			// t normalized to [0 .. 1] after the peak
			float tFall = Mathf.Clamp01((t - peakPosition) / (1f - peakPosition));

			// ---- RISE CURVE (InBack-style, adjustable sharpness) -------------

			// cubic-ish overshoot rise ? gives backward dip look
			float rise = 1f - Mathf.Pow(1f - tRise, 3f + sharpness * 4f);

			// ---- FALL CURVE (perfect linear exit) ----------------------------

			float fall = 1f - tFall;    // straight line down from peak

			// ---- BLEND REGION AROUND PEAK -----------------------------------

			// blend window ?10% of total duration
			float blendWidth = 0.1f;

			float blend = Mathf.InverseLerp(peakPosition - blendWidth,
				peakPosition + blendWidth,
				t);

			blend = Mathf.Clamp01(blend);

			float curve = Mathf.Lerp(rise, fall, blend);

			target.localPosition = startPos + direction * curve * strength;

		}, 1f, duration).SetEase(Ease.Linear);
	}
}