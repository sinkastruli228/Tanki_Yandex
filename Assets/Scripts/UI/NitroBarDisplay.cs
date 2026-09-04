using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class NitroBarDisplay : MonoBehaviour
{
    [SerializeField] private TankNitro nitro;
    [SerializeField] private Image fillImage;

    public void Configure(TankNitro tankNitro, Image fill)
    {
        nitro = tankNitro;
        fillImage = fill;
        Refresh();
    }

    private void Update()
    {
        Refresh();
    }

    private void Refresh()
    {
        if (nitro != null && fillImage != null)
        {
            fillImage.fillAmount = nitro.Normalized;
        }
    }
}
