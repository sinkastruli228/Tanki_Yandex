using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Small hover/press spring. Menu travel is handled on parent RectTransforms.
public sealed class GarageUiMotion : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    private bool hover, pressed;
    private float scale = 1, velocity;
    private Button button;
    private void Awake() => button = GetComponent<Button>();
    public void OnPointerEnter(PointerEventData e) => hover = true;
    public void OnPointerExit(PointerEventData e) { hover = false; pressed = false; }
    public void OnPointerDown(PointerEventData e) => pressed = true;
    public void OnPointerUp(PointerEventData e) => pressed = false;
    private void OnDisable() { hover = pressed = false; scale = 1; velocity = 0; transform.localScale = Vector3.one; }
    private void Update()
    {
        float target = button != null && !button.interactable ? 1 : pressed ? 0.96f : hover ? 1.035f : 1;
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.033f);
        velocity += ((target - scale) * 240 - velocity * 20) * dt;
        scale += velocity * dt;
        transform.localScale = Vector3.one * scale;
    }

    public static float OutBack(float t)
    {
        t = Mathf.Clamp01(t) - 1;
        return 1 + 2.1f * t * t * t + 1.1f * t * t;
    }
    public static float InBack(float t) => 1 - OutBack(1 - t);
    public static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3 - 2 * t); }
}
