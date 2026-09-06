using System;
using System.Collections.Generic;
using UnityEngine;

public static class GameLanguage
{
    private const string PreferenceKey = "Tanki.Language";
    private static int language = -1;
    public static event Action Changed;
    public static bool IsEnglish => (language < 0 ? language = PlayerPrefs.GetInt(PreferenceKey, 0) : language) == 1;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { language = -1; Changed = null; }
    public static void SetEnglish(bool english)
    {
        if (IsEnglish == english) return;
        language = english ? 1 : 0;
        PlayerPrefs.SetInt(PreferenceKey, language);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }
    public static string Text(string russian, string english) => IsEnglish ? english : russian;
    public static string Translate(string source) => IsEnglish && English.TryGetValue(source, out var translated) ? translated : source;
    private static readonly Dictionary<string, string> English = new Dictionary<string, string>
    {
        { "Выбери раскраску и отправляйся в бой", "Choose your paint and head into battle" },
        { "МОНЕТЫ", "COINS" }, { "ИГРАТЬ", "PLAY" }, { "БЕСКОНЕЧНЫЙ БОЙ", "ENDLESS BATTLE" },
        { "УЛЬТА", "ULT" },
        { "РАКЕТА", "ROCKET" }, { "ЩИТ", "SHIELD" }, { "БОМБАРДИРОВКА", "BOMBARDMENT" }, { "СКОРО", "SOON" },
        { "ПРЕВЬЮ 4:3", "4:3 PREVIEW" },
        { "Самонаводящийся удар\nпо выбранной цели", "Homing strike against\nthe selected target" },
        { "Шесть плит защищают танк\nот вражеских снарядов", "Six plates protect the tank\nfrom enemy projectiles" },
        { "Нарисуй зону для\nмассированного удара", "Paint a zone for\na massive airstrike" },
        { "Новая ульта\nпоявится позже", "A new ultimate\nis coming later" },
        { "НАСТРОЙКИ", "SETTINGS" }, { "ВЫХОД", "EXIT" }, { "НАЗАД", "BACK" },
        { "Аркадные танковые сражения", "Arcade tank battles" },
        { "ЗВУК: ВЫКЛ", "SOUND: OFF" }, { "ЗВУК: ВКЛ", "SOUND: ON" },
        { "WASD — движение\nМышь — башня и выстрел\nEsc — вернуться в меню", "WASD — move\nMouse — aim and fire\nEsc — return to menu" },
        { "СЕКРЕТНЫЙ ТАНК", "SECRET TANK" }, { "ЛЕСНОЙ", "FOREST" }, { "ПУСТЫННЫЙ", "DESERT" }, { "ПОЛЯРНЫЙ", "POLAR" },
        { "Тяжёлая броня. Большие планы.", "Heavy armor. Big plans." },
        { "Классическая зелёная броня", "Classic green armor" }, { "Тёплая палитра песчаных дюн", "Warm colors of the desert dunes" },
        { "Светлая броня северных широт", "Light armor of the frozen north" },
        { "ВЫБРАНО", "SELECTED" }, { "ОТКРЫТЬ  •  1 000 МОНЕТ", "UNLOCK  •  1,000 COINS" }
    };
}
