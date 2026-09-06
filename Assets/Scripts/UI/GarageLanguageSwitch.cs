using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public sealed class GarageLanguageSwitch : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    public RectTransform Thumb;
    public Text Russian, English;
    private float from, to, elapsed;
    public void Configure(RectTransform thumb, Text ru, Text en)
    {
        Thumb = thumb; Russian = ru; English = en;
        Refresh(); Thumb.anchoredPosition = new Vector2(to, 0);
    }
    private void OnEnable() { GameLanguage.Changed += Refresh; Refresh(); }
    private void OnDisable() => GameLanguage.Changed -= Refresh;
    private void Refresh()
    {
        if (Thumb == null) return;
        from = Thumb.anchoredPosition.x; to = GameLanguage.IsEnglish ? 58 : -58; elapsed = 0;
        Russian.color = GameLanguage.IsEnglish ? Color.white : new Color(.095f, .14f, .14f);
        English.color = GameLanguage.IsEnglish ? new Color(.095f, .14f, .14f) : Color.white;
    }
    private void Update()
    {
        if (Thumb == null) return;
        elapsed += Time.unscaledDeltaTime;
        Thumb.anchoredPosition = new Vector2(Mathf.LerpUnclamped(from, to, GarageUiMotion.OutBack(elapsed / .2f)), 0);
    }
    public void OnPointerDown(PointerEventData e) => Choose(e);
    public void OnDrag(PointerEventData e) => Choose(e);
    private void Choose(PointerEventData e)
    {
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)Thumb.parent, e.position, e.pressEventCamera, out var point))
            GameLanguage.SetEnglish(point.x > 0);
    }
}
