using System.Collections;
using UnityEngine;

namespace Bluff.Game
{
    /// <summary>Tween sederhana berbasis coroutine (tanpa library luar). Memakai waktu game, jadi ikut berhenti saat pause.</summary>
    public static class Tween
    {
        public static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);

        public static float EaseInOutCubic(float t) => t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;

        public static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        /// <summary>Panggil <paramref name="step"/> dengan t 0..1 selama <paramref name="duration"/> detik.</summary>
        public static IEnumerator Run(float duration, System.Action<float> step)
        {
            if (duration <= 0f)
            {
                step(1f);
                yield break;
            }
            float t = 0f;
            while (t < duration)
            {
                step(Mathf.Clamp01(t / duration));
                yield return null;
                t += Time.deltaTime;
            }
            step(1f);
        }

        public static IEnumerator Fade(CanvasGroup group, float to, float duration)
        {
            if (group == null)
                yield break;
            float from = group.alpha;
            yield return Run(duration, t =>
            {
                if (group != null)
                    group.alpha = Mathf.Lerp(from, to, t);
            });
        }

        /// <summary>Kejutan kecil: skala naik lalu kembali.</summary>
        public static IEnumerator Punch(Transform target, float amount, float duration)
        {
            if (target == null)
                yield break;
            Vector3 baseScale = target.localScale;
            yield return Run(duration, t =>
            {
                if (target != null)
                    target.localScale = baseScale * (1f + Mathf.Sin(t * Mathf.PI) * amount);
            });
        }

        public static IEnumerator Shake(RectTransform target, float strength, float duration)
        {
            if (target == null)
                yield break;
            Vector2 origin = target.anchoredPosition;
            yield return Run(duration, t =>
            {
                if (target == null)
                    return;
                float falloff = 1f - t;
                target.anchoredPosition = origin + Random.insideUnitCircle * strength * falloff;
            });
            if (target != null)
                target.anchoredPosition = origin;
        }
    }
}
