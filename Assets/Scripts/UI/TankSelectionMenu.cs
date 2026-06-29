using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class TankSelectionMenu : MonoBehaviour
{
    [SerializeField] private GameObject playerTank;
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Button normalButton;
    [SerializeField] private Button desertButton;
    [SerializeField] private Button snowButton;
    [SerializeField] private Button mausButton;

    private bool hasSelection;

    public void Configure(GameObject tank, GameObject panel, Button normal, Button desert, Button snow, Button maus, bool showImmediately = true)
    {
        playerTank = tank;
        panelRoot = panel;
        normalButton = normal;
        desertButton = desert;
        snowButton = snow;
        mausButton = maus;
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

        if (snowButton != null)
        {
            snowButton.onClick.RemoveListener(SelectSnowTank);
            snowButton.onClick.AddListener(SelectSnowTank);
        }

        if (mausButton != null)
        {
            mausButton.onClick.RemoveListener(SelectMausTank);
            mausButton.onClick.AddListener(SelectMausTank);
        }

        if (Application.isPlaying && showImmediately)
        {
            ShowSelection();
        }
        else if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
    }

    public void ShowSelection()
    {
        hasSelection = false;
        PlayerHealthBar.GameplayInputBlocked = true;
        SetPlayerControlEnabled(false);
        HideWaveAnnouncement();
        Time.timeScale = 0f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
            panelRoot.transform.SetAsLastSibling();
        }
    }

    private void SelectNormalTank()
    {
        TankiGameplayBootstrap.ApplyNormalTankSkin(playerTank);
        CompleteSelection();
    }

    private void SelectDesertTank()
    {
        TankiGameplayBootstrap.ApplyDesertTankSkin(playerTank);
        CompleteSelection();
    }

    private void SelectSnowTank()
    {
        TankiGameplayBootstrap.ApplySnowTankSkin(playerTank);
        CompleteSelection();
    }

    private void SelectMausTank()
    {
        TankiGameplayBootstrap.ApplyMausTank(playerTank);
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
        TankiGameplayBootstrap.StartWavesAfterTankSelection();
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

        TankTurretAim turretAim = playerTank.GetComponent<TankTurretAim>();
        if (turretAim != null)
        {
            turretAim.enabled = isEnabled;
        }
    }

    private void HideWaveAnnouncement()
    {
        if (panelRoot == null || panelRoot.transform.parent == null)
        {
            return;
        }

        Transform waveAnnouncement = panelRoot.transform.parent.Find("Wave Announcement");
        if (waveAnnouncement != null)
        {
            waveAnnouncement.gameObject.SetActive(false);
        }
    }
}
