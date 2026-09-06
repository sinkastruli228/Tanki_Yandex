using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Text))]
public sealed class LocalizedGameText : MonoBehaviour
{
    private string russian, english;
    public void Configure(string ru, string en) { russian = ru; english = en; Refresh(); }
    private void OnEnable() { GameLanguage.Changed += Refresh; Refresh(); }
    private void OnDisable() => GameLanguage.Changed -= Refresh;
    private void Refresh()
    {
        if (russian != null) GetComponent<Text>().text = GameLanguage.Text(russian, english);
    }
}
