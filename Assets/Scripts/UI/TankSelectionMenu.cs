using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class TankSelectionMenu : MonoBehaviour
{
    [SerializeField] private GameObject playerTank;
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Button normalButton;
    [SerializeField] private Button desertButton;

    private bool hasSelection;

    public void Configure(GameObject tank, GameObject panel, Button normal, Button desert)
    {
        playerTank = tank;
        panelRoot = panel;
        normalButton = normal;
        desertButton = desert;
        hasSelection = false;

        if (normalButton != null)
        {
            normalButton.onClick.RemoveListener(SelectNormalTank);
            normalButton.onClick.AddListener(SelectNormalTank);
        }

        if (desertButton != null)
        {
            desertButton.onClick.RemoveListener(SelectDesertTank);
            desertButton.onClick.AddListener(SelectDesertTank);
        }

        if (Application.isPlaying)
        {
            ShowSelection();
        }
        else if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
    }

    private void ShowSelection()
    {
        PlayerHealthBar.GameplayInputBlocked = true;
        SetPlayerControlEnabled(false);
        Time.timeScale = 0f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
        }
    }

    private void SelectNormalTank()
    {
        CompleteSelection();
    }

    private void SelectDesertTank()
    {
        TankiGameplayBootstrap.ApplyDesertTankSkin(playerTank);
        CompleteSelection();
    }

    private void CompleteSelection()
    {
        if (hasSelection)
        {
            return;
        }

        hasSelection = true;
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }

        PlayerHealthBar.GameplayInputBlocked = false;
        Time.timeScale = 1f;
        SetPlayerControlEnabled(true);
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.None;
    }

    private void SetPlayerControlEnabled(bool isEnabled)
    {
        if (playerTank == null)
        {
            return;
        }

        TankController controller = playerTank.GetComponent<TankController>();
        if (controller != null)
        {
            controller.enabled = isEnabled;
        }

        TankShooter shooter = playerTank.GetComponent<TankShooter>();
        if (shooter != null)
        {
            shooter.enabled = isEnabled;
        }
    }
}
