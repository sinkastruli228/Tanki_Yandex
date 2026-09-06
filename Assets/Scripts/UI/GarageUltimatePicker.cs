using UnityEngine;
using UnityEngine.UI;

public sealed class GarageUltimatePicker : MonoBehaviour
{
    private static readonly string[] Numerals = { "I", "II", "III", "IV" };

    private Button trigger;
    private Text triggerMark;
    private RectTransform choices;
    private Button[] buttons;
    private Image[] backgrounds;
    private Text[] labels;
    private Text[] names;
    private Text[] descriptions;
    private CanvasGroup[] groups;
    private Color selectedColor, idleColor, selectedTextColor;
    private bool open, interactable = true;
    private float progress;

    public bool IsOpen => open;

    public void Configure(
        Button triggerButton,
        Text mark,
        RectTransform choicesRoot,
        Button[] choiceButtons,
        Image[] choiceBackgrounds,
        Text[] choiceLabels,
        Text[] choiceNames,
        Text[] choiceDescriptions,
        CanvasGroup[] choiceGroups,
        Color highlight,
        Color selectedText)
    {
        trigger = triggerButton;
        triggerMark = mark;
        choices = choicesRoot;
        buttons = choiceButtons;
        backgrounds = choiceBackgrounds;
        labels = choiceLabels;
        names = choiceNames;
        descriptions = choiceDescriptions;
        groups = choiceGroups;
        selectedColor = highlight;
        idleColor = new Color(.18f, .25f, .24f);
        selectedTextColor = selectedText;
        progress = 0;
        choices.gameObject.SetActive(false);
        RefreshSelection();
    }

    private void OnEnable()
    {
        TankUltimateLoadout.Changed += RefreshSelection;
        RefreshSelection();
    }

    private void OnDisable()
    {
        TankUltimateLoadout.Changed -= RefreshSelection;
        open = false;
        progress = 0;
        if (choices != null) choices.gameObject.SetActive(false);
    }

    public void Toggle()
    {
        if (!interactable) return;
        open = !open;
        if (open) choices.gameObject.SetActive(true);
    }

    public void Select(int slot)
    {
        if (!interactable) return;
        TankUltimateLoadout.Select(slot);
        RefreshSelection();
        open = false;
    }

    public void SetInteractable(bool value)
    {
        interactable = value;
        if (trigger != null) trigger.interactable = value;
        if (buttons != null)
            for (int i = 0; i < buttons.Length; i++)
                buttons[i].interactable = value && TankUltimateLoadout.IsAvailable(i);
        if (!value) open = false;
    }

    private void RefreshSelection()
    {
        if (triggerMark == null || backgrounds == null) return;
        int selected = TankUltimateLoadout.Selected;
        triggerMark.text = Numerals[selected];
        for (int i = 0; i < backgrounds.Length; i++)
        {
            bool active = i == selected;
            backgrounds[i].color = active ? selectedColor : idleColor;
            labels[i].color = active ? selectedColor : Color.white;
            if (names != null && i < names.Length && names[i] != null)
            {
                names[i].color = active ? selectedTextColor : new Color(.67f, .73f, .69f);
            }
            if (descriptions != null && i < descriptions.Length && descriptions[i] != null)
            {
                descriptions[i].color = active ? new Color(.12f, .17f, .16f) : new Color(.67f, .73f, .69f);
            }
        }
    }

    private void Update()
    {
        if (choices == null || groups == null) return;
        float target = open ? 1 : 0;
        progress = Mathf.MoveTowards(progress, target, Time.unscaledDeltaTime / .22f);
        for (int i = 0; i < groups.Length; i++)
        {
            float delay = i * .11f;
            float item = open
                ? Mathf.Clamp01((progress - delay) / (1 - delay))
                : Mathf.Clamp01(progress / (1 - delay));
            bool available = TankUltimateLoadout.IsAvailable(i);
            groups[i].alpha = item * (available ? 1f : .48f);
            groups[i].interactable = open && item > .92f && interactable && available;
            groups[i].blocksRaycasts = open && item > .92f && interactable && available;
            groups[i].transform.localScale = Vector3.one * Mathf.LerpUnclamped(.72f, 1, GarageUiMotion.OutBack(item));
        }
        if (!open && progress <= 0) choices.gameObject.SetActive(false);
    }
}
