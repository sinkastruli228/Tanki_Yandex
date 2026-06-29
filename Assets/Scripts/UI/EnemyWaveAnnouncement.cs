using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class EnemyWaveAnnouncement : MonoBehaviour
{
    [SerializeField] private Text waveText;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float fadeInTime = 0.45f;
    [SerializeField] private float holdTime = 1.05f;
    [SerializeField] private float fadeOutTime = 0.65f;

    public void Configure(Text text, CanvasGroup group)
    {
        waveText = text;
        canvasGroup = group;
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        gameObject.SetActive(false);
    }

    public IEnumerator Play(int waveNumber)
    {
        if (waveText == null || canvasGroup == null)
        {
            yield break;
        }

        waveText.text = $"{waveNumber} волна";
        gameObject.SetActive(true);
        yield return Fade(0f, 1f, fadeInTime);

        if (holdTime > 0f)
        {
            float elapsed = 0f;
            while (elapsed < holdTime)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        yield return Fade(1f, 0f, fadeOutTime);
        gameObject.SetActive(false);
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            canvasGroup.alpha = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t);
            canvasGroup.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }

        canvasGroup.alpha = to;
    }

    private void OnValidate()
    {
        fadeInTime = Mathf.Max(0f, fadeInTime);
        holdTime = Mathf.Max(0f, holdTime);
        fadeOutTime = Mathf.Max(0f, fadeOutTime);
    }
}
