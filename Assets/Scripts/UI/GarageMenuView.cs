using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class GarageMenuView : MonoBehaviour
{
    public sealed class Travel
    {
        public RectTransform rect;
        public Vector2 home;
        public int side;
    }

    public readonly List<Travel> Pieces = new List<Travel>();
    public CanvasGroup Group { get; private set; }
    public Button PlayButton, InfiniteButton, PreviousButton, NextButton, BuyButton, SecretButton;
    public Text CoinsLabel, SkinName, SkinNumber, SkinState, BuyLabel;
    public RectTransform Wallet { get; private set; }
    public GarageUltimatePicker UltimatePicker { get; private set; }
    private readonly Dictionary<Text, string> localizedLabels = new Dictionary<Text, string>();
    private int previewSkin;
    private bool previewBusy;
    public GameObject SettingsPanel;
    public Action Play, Infinite, Previous, Next, Buy, Secret, Exit;
    private readonly Color cream = new Color(0.98f, 0.95f, 0.87f);
    private readonly Color ink = new Color(0.095f, 0.14f, 0.14f);
    private readonly Color muted = new Color(0.67f, 0.73f, 0.69f);
    private readonly Color gold = new Color(0.96f, 0.71f, 0.30f);
    private Sprite rounded;
    private Texture2D roundedTexture;
    private Font font;
    private Image[] dots;

    public void Build()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        rounded = CreateRoundedSprite();
        Group = gameObject.AddComponent<CanvasGroup>();

        var title = Piece("Garage Title", new Vector2(0, 1), new Vector2(74, -63), new Vector2(440, 150), new Vector2(0, 1), -1);
        Label(title, "Title", "DESERT TANKS", 46, cream, new Vector2(0, -55), new Vector2(470, 65), TextAnchor.MiddleLeft, true);
        Label(title, "Subtitle", "Выбери раскраску и отправляйся в бой", 18, cream, new Vector2(0, -112), new Vector2(470, 30), TextAnchor.MiddleLeft);

        foreach (var label in title.GetComponentsInChildren<Text>())
        {
            var shadow = label.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(.04f, .07f, .06f, .65f);
            shadow.effectDistance = new Vector2(0, -1.5f);
        }
        var wallet = Rect(transform.parent, "Garage Wallet", new Vector2(1, 1), new Vector2(-56, -48), new Vector2(238, 76), new Vector2(1, 1));
        Wallet = wallet;
        Panel(wallet, ink, 0.96f);
        var coin = Rect(wallet, "Coin", new Vector2(0, .5f), new Vector2(25, 0), new Vector2(40, 40), new Vector2(0, .5f));
        Panel(coin, gold);
        CenterLabel(coin, "Coin Mark", "C", 24, ink, true);
        Label(wallet, "Currency", "МОНЕТЫ", 12, muted, new Vector2(84, -17), new Vector2(130, 18), TextAnchor.MiddleLeft);
        CoinsLabel = Label(wallet, "Balance", "0", 29, cream, new Vector2(84, -39), new Vector2(138, 30), TextAnchor.MiddleLeft, true);

        var actions = Piece("Garage Actions", new Vector2(1, .5f), new Vector2(-56, -20), new Vector2(344, 384), new Vector2(1, .5f), 1);
        Panel(actions, ink, .95f);
        PlayButton = ActionButton(actions, "Play", "ИГРАТЬ", -24, gold, ink, () => Play?.Invoke());
        InfiniteButton = ActionButton(actions, "Infinite", "БЕСКОНЕЧНЫЙ БОЙ", -102, new Color(.23f, .31f, .29f), cream, () => Infinite?.Invoke(), 19);
        ActionButton(actions, "Settings", "НАСТРОЙКИ", -180, new Color(.18f, .25f, .24f), cream, ToggleSettings, 20);
        ActionButton(actions, "Exit", "ВЫХОД", -258, new Color(.18f, .25f, .24f), cream, () => Exit?.Invoke(), 20);
        Label(actions, "Footer", "Аркадные танковые сражения", 13, muted, new Vector2(28, -343), new Vector2(288, 22), TextAnchor.MiddleCenter);

        var previous = Piece("Previous Skin", new Vector2(.085f, .43f), Vector2.zero, new Vector2(66, 76), new Vector2(.5f, .5f), -1);
        PreviousButton = ArrowButton(previous, -1, () => Previous?.Invoke());
        var next = Piece("Next Skin", new Vector2(.665f, .43f), Vector2.zero, new Vector2(66, 76), new Vector2(.5f, .5f), 1);
        NextButton = ArrowButton(next, 1, () => Next?.Invoke());

        var info = Piece("Garage Skin Info", new Vector2(.375f, 0), new Vector2(0, 36), new Vector2(392, 177), new Vector2(.5f, 0), -1);
        Panel(info, ink, .94f);
        SkinNumber = Label(info, "Index", "РАСКРАСКА  01 / 03", 12, muted, new Vector2(24, -13), new Vector2(250, 22), TextAnchor.MiddleLeft);
        SkinName = Label(info, "Skin", "ЛЕСНОЙ", 28, cream, new Vector2(24, -39), new Vector2(300, 40), TextAnchor.MiddleLeft, true);
        SkinState = Label(info, "State", "Классическая зелёная броня", 15, muted, new Vector2(24, -80), new Vector2(340, 22), TextAnchor.MiddleLeft);
        var buyRect = Rect(info, "Skin Purchase", new Vector2(.5f, 0), new Vector2(0, 14), new Vector2(344, 44), new Vector2(.5f, 0));
        BuyButton = ButtonOn(buyRect, "ВЫБРАНО", 17, gold, ink, () => Buy?.Invoke());
        BuyLabel = BuyButton.GetComponentInChildren<Text>();
        dots = new Image[3];
        for (int i = 0; i < 3; i++)
        {
            var dot = Rect(info, "Skin Dot " + i, new Vector2(1, 1), new Vector2(-51 + i * 13, -20), new Vector2(7, 7), new Vector2(.5f, .5f));
            dots[i] = Panel(dot, muted);
        }

        var ultimate = Piece("Ultimate Picker", new Vector2(.525f, 0), new Vector2(0, 36), new Vector2(92, 92), new Vector2(0, 0), 1);
        var choices = Rect(ultimate, "Ultimate Choices", new Vector2(0, 1), new Vector2(-365, 22), new Vector2(740, 226), new Vector2(0, 0));
        var choiceButtons = new Button[4];
        var choiceBackgrounds = new Image[4];
        var choiceLabels = new Text[4];
        var choiceNames = new Text[4];
        var choiceDescriptions = new Text[4];
        var choiceGroups = new CanvasGroup[4];
        string[] numerals = { "I", "II", "III", "IV" };
        string[] ultimateNames = { "РАКЕТА", "ЩИТ", "БОМБАРДИРОВКА", "СКОРО" };
        string[] ultimateDescriptions =
        {
            "Самонаводящийся удар\nпо выбранной цели",
            "Шесть плит защищают танк\nот вражеских снарядов",
            "Нарисуй зону для\nмассированного удара",
            "Новая ульта\nпоявится позже"
        };
        for (int i = 0; i < choiceButtons.Length; i++)
        {
            int selected = i;
            var choice = Rect(choices, "Ultimate " + (i + 1), new Vector2(0, 0), new Vector2(i * 188, 0), new Vector2(176, 226), new Vector2(0, 0));
            choiceButtons[i] = ButtonOn(choice, numerals[i], 16, new Color(.18f, .25f, .24f), cream, () => UltimatePicker.Select(selected));
            choiceBackgrounds[i] = choiceButtons[i].targetGraphic as Image;
            choiceLabels[i] = choiceButtons[i].GetComponentInChildren<Text>();
            var preview = Rect(choice, "Preview 4x3", new Vector2(.5f, 1), new Vector2(0, -10), new Vector2(156, 117), new Vector2(.5f, 1));
            Panel(preview, new Color(.09f, .14f, .14f));
            CenterLabel(preview, "Preview Icon", "▶", 30, new Color(.96f, .71f, .30f, .78f), true);
            Label(preview, "Preview Format", "ПРЕВЬЮ 4:3", 9, muted, new Vector2(8, -91), new Vector2(140, 18), TextAnchor.MiddleCenter, true);

            var numeralRect = choiceLabels[i].rectTransform;
            numeralRect.anchorMin = numeralRect.anchorMax = new Vector2(0, 1);
            numeralRect.pivot = new Vector2(0, 1);
            numeralRect.anchoredPosition = new Vector2(18, -16);
            numeralRect.sizeDelta = new Vector2(28, 24);
            choiceNames[i] = Label(choice, "Name", ultimateNames[i], 16, cream, new Vector2(10, -135), new Vector2(156, 24), TextAnchor.MiddleLeft, true);
            choiceDescriptions[i] = Label(choice, "Description", ultimateDescriptions[i], 11, muted, new Vector2(10, -161), new Vector2(156, 54), TextAnchor.UpperLeft);
            choiceNames[i].transform.SetAsLastSibling();
            choiceDescriptions[i].transform.SetAsLastSibling();
            choiceLabels[i].transform.SetAsLastSibling();
            choiceGroups[i] = choice.gameObject.AddComponent<CanvasGroup>();
        }
        var ultimateButton = ButtonOn(ultimate, "I", 28, ink, cream, () => UltimatePicker.Toggle());
        var ultimateMark = ultimate.Find("Label").GetComponent<Text>();
        var ultimateTitle = Label(ultimate, "Title", "УЛЬТА", 10, muted, new Vector2(10, -7), new Vector2(66, 18), TextAnchor.MiddleCenter, true);
        ultimateTitle.transform.SetAsLastSibling();
        ultimateMark.rectTransform.offsetMin = new Vector2(8, 10);
        ultimateMark.rectTransform.offsetMax = new Vector2(-8, -10);
        UltimatePicker = ultimate.gameObject.AddComponent<GarageUltimatePicker>();
        UltimatePicker.Configure(ultimateButton, ultimateMark, choices, choiceButtons, choiceBackgrounds, choiceLabels, choiceNames, choiceDescriptions, choiceGroups, gold, ink);

        var language = Piece("Language Switch", new Vector2(0, 0), new Vector2(74, 31), new Vector2(242, 56), new Vector2(0, 0), -1);
        Panel(language, ink, .96f).raycastTarget = true;
        var track = Rect(language, "Language Track", new Vector2(.5f, .5f), Vector2.zero, new Vector2(242, 56), new Vector2(.5f, .5f));
        var thumb = Rect(track, "Selection", new Vector2(.5f, .5f), Vector2.zero, new Vector2(112, 46), new Vector2(.5f, .5f));
        Panel(thumb, gold);
        var ru = Rect(track, "RU", new Vector2(.5f, .5f), new Vector2(-58, 0), new Vector2(112, 46), new Vector2(.5f, .5f));
        var en = Rect(track, "EN", new Vector2(.5f, .5f), new Vector2(58, 0), new Vector2(112, 46), new Vector2(.5f, .5f));
        var ruLabel = CenterLabel(ru, "Label", "RU", 20, cream, true);
        var enLabel = CenterLabel(en, "Label", "EN", 20, cream, true);
        foreach (var cell in new[] { ru, en })
        {
            var hitArea = cell.gameObject.AddComponent<Image>();
            hitArea.color = new Color(1, 1, 1, .001f); hitArea.raycastTarget = true;
            var button = cell.gameObject.AddComponent<Button>();
            button.targetGraphic = hitArea; button.transition = Selectable.Transition.None;
            bool english = cell == en;
            button.onClick.AddListener(() => GameLanguage.SetEnglish(english));
        }
        var switcher = language.gameObject.AddComponent<GarageLanguageSwitch>();
        switcher.Configure(thumb, ruLabel, enLabel);
        var secret = Piece("Secret Maus", new Vector2(0, 1), new Vector2(14, -14), new Vector2(34, 34), new Vector2(0, 1), -1);
        SecretButton = ButtonOn(secret, "M", 14, new Color(.08f, .12f, .12f, .12f), new Color(1, 1, 1, .22f), () => Secret?.Invoke());

        var settings = Rect(transform, "Garage Settings", new Vector2(1, .5f), new Vector2(-56, -20), new Vector2(344, 432), new Vector2(1, .5f));
        Panel(settings, ink);
        Label(settings, "Heading", "НАСТРОЙКИ", 26, cream, new Vector2(28, -29), new Vector2(290, 42), TextAnchor.MiddleLeft, true);
        bool mutedAudio = PlayerPrefs.GetInt("Tanki.AudioMuted", 0) != 0;
        AudioListener.volume = mutedAudio ? 0 : 1;
        Button sound = null;
        sound = ActionButton(settings, "Sound", mutedAudio ? "ЗВУК: ВЫКЛ" : "ЗВУК: ВКЛ", -102, gold, ink, () =>
        {
            bool mute = AudioListener.volume > 0;
            AudioListener.volume = mute ? 0 : 1;
            PlayerPrefs.SetInt("Tanki.AudioMuted", mute ? 1 : 0);
            PlayerPrefs.Save();
            localizedLabels[sound.GetComponentInChildren<Text>()] = mute ? "ЗВУК: ВЫКЛ" : "ЗВУК: ВКЛ";
            RefreshLanguage();
        }, 20);
        Label(settings, "Controls", "WASD — движение\nМышь — башня и выстрел\nEsc — вернуться в меню", 17, muted, new Vector2(28, -193), new Vector2(290, 96), TextAnchor.UpperLeft);
        ActionButton(settings, "Close Settings", "НАЗАД", -326, new Color(.23f, .31f, .29f), cream, ToggleSettings, 20);
        SettingsPanel = settings.gameObject;
        SettingsPanel.SetActive(false);
        GameLanguage.Changed += RefreshLanguage;
        RefreshLanguage();
    }

    public void Refresh(int skin, bool busy)
    {
        previewSkin = skin; previewBusy = busy;
        bool maus = skin == 3;
        bool owned = maus || TankGarageProgress.Owns(skin);
        CoinsLabel.text = TankGarageProgress.Coins.ToString("N0");
        SkinNumber.text = maus ? GameLanguage.Translate("СЕКРЕТНЫЙ ТАНК") : GameLanguage.Text($"РАСКРАСКА  {skin + 1:00} / 03", $"PAINT  {skin + 1:00} / 03");
        SkinName.text = GameLanguage.Translate(maus ? "MAUS" : new[] { "ЛЕСНОЙ", "ПУСТЫННЫЙ", "ПОЛЯРНЫЙ" }[skin]);
        SkinState.text = GameLanguage.Translate(maus ? "Тяжёлая броня. Большие планы." : new[] { "Классическая зелёная броня", "Тёплая палитра песчаных дюн", "Светлая броня северных широт" }[skin]);
        BuyLabel.text = GameLanguage.Translate(owned ? "ВЫБРАНО" : "ОТКРЫТЬ  •  1 000 МОНЕТ");
        BuyButton.interactable = !busy && !owned && TankGarageProgress.Coins >= TankGarageProgress.SkinPrice;
        if (!owned && TankGarageProgress.Coins < TankGarageProgress.SkinPrice)
            SkinState.text = GameLanguage.Text($"Нужно ещё {TankGarageProgress.SkinPrice - TankGarageProgress.Coins:N0} монет", $"Need {TankGarageProgress.SkinPrice - TankGarageProgress.Coins:N0} more coins");
        PlayButton.interactable = InfiniteButton.interactable = owned && !busy;
        PreviousButton.interactable = NextButton.interactable = SecretButton.interactable = !busy;
        if (UltimatePicker != null) UltimatePicker.SetInteractable(!busy);
        for (int i = 0; i < 3; i++) dots[i].color = skin == i ? gold : muted * .6f;
    }

    private void RefreshLanguage()
    {
        foreach (var entry in localizedLabels)
            if (entry.Key != null) entry.Key.text = GameLanguage.Translate(entry.Value);
        Refresh(previewSkin, previewBusy);
    }
    private Button ArrowButton(RectTransform parent, int direction, Action callback)
    {
        var button = ButtonOn(parent, "", 1, ink, cream, callback);
        var icon = Rect(parent, "Chevron", new Vector2(.5f, .5f), Vector2.zero, new Vector2(20, 26), new Vector2(.5f, .5f));
        for (int i = 0; i < 2; i++)
        {
            var bar = Rect(icon, "Stroke " + i, new Vector2(.5f, .5f), new Vector2(0, i == 0 ? 5 : -5), new Vector2(17, 4), new Vector2(.5f, .5f));
            bar.localRotation = Quaternion.Euler(0, 0, direction * (i == 0 ? -50 : 50));
            var image = bar.gameObject.AddComponent<Image>(); image.color = cream; image.raycastTarget = false;
        }
        return button;
    }
    public void ToggleSettings() => SettingsPanel.SetActive(!SettingsPanel.activeSelf);
    public void TravelTo(float progress, bool entering)
    {
        float width = ((RectTransform)transform).rect.width + 450;
        foreach (var piece in Pieces)
        {
            float eased = entering ? GarageUiMotion.OutBack(progress) : GarageUiMotion.InBack(progress);
            Vector2 outside = piece.home + Vector2.right * width * piece.side;
            piece.rect.anchoredPosition = entering ? Vector2.LerpUnclamped(outside, piece.home, eased) : Vector2.LerpUnclamped(piece.home, outside, eased);
        }
    }

    private RectTransform Piece(string name, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot, int side)
    {
        var rect = Rect(transform, name, anchor, position, size, pivot);
        Pieces.Add(new Travel { rect = rect, home = position, side = side });
        return rect;
    }
    private static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot; rect.sizeDelta = size; rect.anchoredPosition = position;
        return rect;
    }
    private Image Panel(RectTransform rect, Color color, float alpha = -1)
    {
        var image = rect.gameObject.AddComponent<Image>(); image.sprite = rounded; image.type = Image.Type.Sliced;
        if (alpha >= 0) color.a = alpha;
        image.color = color; image.raycastTarget = false; return image;
    }
    private Text Label(Transform parent, string name, string text, int size, Color color, Vector2 position, Vector2 bounds, TextAnchor alignment, bool bold = false)
    {
        var rect = Rect(parent, name, new Vector2(0, 1), position, bounds, new Vector2(0, 1));
        var label = rect.gameObject.AddComponent<Text>(); label.font = font; label.text = text; label.fontSize = size;
        localizedLabels[label] = text;
        label.color = color; label.alignment = alignment; label.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.raycastTarget = false; label.horizontalOverflow = HorizontalWrapMode.Wrap; return label;
    }
    private Text CenterLabel(RectTransform parent, string name, string text, int size, Color color, bool bold = false)
    {
        var label = Label(parent, name, text, size, color, Vector2.zero, parent.sizeDelta, TextAnchor.MiddleCenter, bold);
        label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new Vector2(8, 0); label.rectTransform.offsetMax = new Vector2(-8, 0); return label;
    }
    private Button ActionButton(Transform parent, string name, string text, float y, Color background, Color foreground, Action callback, int fontSize = 24)
    {
        var rect = Rect(parent, name, new Vector2(.5f, 1), new Vector2(0, y), new Vector2(288, 62), new Vector2(.5f, 1));
        return ButtonOn(rect, text, fontSize, background, foreground, callback);
    }
    private Button ButtonOn(RectTransform rect, string text, int size, Color background, Color foreground, Action callback)
    {
        var image = Panel(rect, background); image.raycastTarget = true;
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        var colors = button.colors; colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f); colors.selectedColor = Color.white;
        colors.disabledColor = new Color(.66f, .66f, .66f, .72f); colors.fadeDuration = .12f; button.colors = colors;
        var navigation = button.navigation; navigation.mode = Navigation.Mode.None; button.navigation = navigation;
        button.onClick.AddListener(() => callback()); CenterLabel(rect, "Label", text, size, foreground, true);
        rect.gameObject.AddComponent<GarageUiMotion>(); return button;
    }
    private Sprite CreateRoundedSprite()
    {
        const int size = 64; const float radius = 14;
        roundedTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Garage Rounded Panel", filterMode = FilterMode.Bilinear };
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
        {
            float dx = Mathf.Max(Mathf.Abs(x - 31.5f) - (32 - radius), 0);
            float dy = Mathf.Max(Mathf.Abs(y - 31.5f) - (32 - radius), 0);
            pixels[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy)));
        }
        roundedTexture.SetPixels(pixels); roundedTexture.Apply();
        return Sprite.Create(roundedTexture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, new Vector4(16, 16, 16, 16));
    }
    private void OnDestroy() { GameLanguage.Changed -= RefreshLanguage; if (rounded != null) Destroy(rounded); if (roundedTexture != null) Destroy(roundedTexture); }
}
