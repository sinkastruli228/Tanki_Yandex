using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class MenuButtonHighlight : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private Image targetImage;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color hoverColor = new Color(1.35f, 1.35f, 1.35f, 1f);
    [SerializeField] private Color pressedColor = new Color(0.55f, 0.55f, 0.55f, 1f);

    private bool isHovering;
    private bool isPressed;
    private Vector3 baseScale = Vector3.one;

    public void Configure(Image image, Color normal, Color hover, Color pressed)
    {
        targetImage = image;
        normalColor = normal;
        hoverColor = hover;
        pressedColor = pressed;
        baseScale = transform.localScale;
        RemoveOldOverlay();
        ApplyColor();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovering = true;
        ApplyColor();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovering = false;
        isPressed = false;
        ApplyColor();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isPressed = true;
        ApplyColor();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPressed = false;
        ApplyColor();
    }

    private void ApplyColor()
    {
        if (targetImage == null)
        {
            return;
        }

        targetImage.color = isPressed ? pressedColor : normalColor;
        targetImage.color = isPressed ? pressedColor : isHovering ? hoverColor : normalColor;
        transform.localScale = baseScale;
    }

    private void RemoveOldOverlay()
    {
        Transform oldOverlay = transform.Find("Hover Tint");
        if (oldOverlay == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(oldOverlay.gameObject);
        }
        else
        {
            DestroyImmediate(oldOverlay.gameObject);
        }
    }
}
