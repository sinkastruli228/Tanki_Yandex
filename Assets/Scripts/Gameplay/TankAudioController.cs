using UnityEngine;

[DisallowMultipleComponent]
public sealed class TankAudioController : MonoBehaviour
{
    [SerializeField] private TankController tankController;
    [SerializeField] private TankShooter tankShooter;
    [SerializeField] private Transform muzzlePoint;
    [SerializeField] private AudioClip movementClip;
    [SerializeField] private AudioClip shotClip;
    [SerializeField] private AudioSource movementSource;
    [SerializeField] private AudioSource shotSource;
    [SerializeField] private float stoppedVolume = 0.16f;
    [SerializeField] private float movingVolume = 0.72f;
    [SerializeField] private float volumeChangeSpeed = 3.5f;
    [SerializeField] private float shotVolume = 1f;

    private TankShooter subscribedShooter;

    public void Configure(
        TankController controller,
        TankShooter shooter,
        Transform muzzle,
        AudioClip movement,
        AudioClip shot)
    {
        tankController = controller;
        tankShooter = shooter;
        muzzlePoint = muzzle;
        movementClip = movement;
        shotClip = shot;

        EnsureMovementSource();
        EnsureShotSource();
        SubscribeToShooter(tankShooter);
    }

    private void OnEnable()
    {
        SubscribeToShooter(tankShooter);
    }

    private void OnDisable()
    {
        SubscribeToShooter(null);
    }

    private void Update()
    {
        UpdateMovementAudio();
    }

    private void UpdateMovementAudio()
    {
        if (movementSource == null || movementClip == null || tankController == null)
        {
            return;
        }

        if (!movementSource.isPlaying)
        {
            movementSource.Play();
        }

        float speed01 = tankController.enabled ? tankController.CurrentSpeedNormalized : 0f;
        float targetVolume = Mathf.Lerp(stoppedVolume, movingVolume, speed01);
        movementSource.volume = Mathf.MoveTowards(
            movementSource.volume,
            targetVolume,
            volumeChangeSpeed * Time.unscaledDeltaTime);
        movementSource.pitch = Mathf.Lerp(0.82f, 1.12f, speed01);
    }

    private void PlayShotAudio()
    {
        if (shotSource == null || shotClip == null)
        {
            return;
        }

        if (muzzlePoint != null)
        {
            shotSource.transform.position = muzzlePoint.position;
        }

        shotSource.PlayOneShot(shotClip, shotVolume);
    }

    private void EnsureMovementSource()
    {
        if (movementSource == null)
        {
            Transform existing = transform.Find("Movement Audio");
            GameObject sourceObject = existing != null ? existing.gameObject : new GameObject("Movement Audio");
            sourceObject.transform.SetParent(transform, false);
            movementSource = sourceObject.GetComponent<AudioSource>();
            if (movementSource == null)
            {
                movementSource = sourceObject.AddComponent<AudioSource>();
            }
        }

        movementSource.clip = movementClip;
        movementSource.loop = true;
        movementSource.playOnAwake = false;
        movementSource.volume = 0f;
        movementSource.spatialBlend = 1f;
        movementSource.rolloffMode = AudioRolloffMode.Linear;
        movementSource.minDistance = 8f;
        movementSource.maxDistance = 180f;
        movementSource.dopplerLevel = 0.15f;
    }

    private void EnsureShotSource()
    {
        Transform parent = muzzlePoint != null ? muzzlePoint : transform;
        if (shotSource == null || shotSource.transform.parent != parent)
        {
            Transform existing = parent.Find("Shot Audio");
            GameObject sourceObject = existing != null ? existing.gameObject : new GameObject("Shot Audio");
            sourceObject.transform.SetParent(parent, false);
            sourceObject.transform.localPosition = Vector3.zero;
            sourceObject.transform.localRotation = Quaternion.identity;
            shotSource = sourceObject.GetComponent<AudioSource>();
            if (shotSource == null)
            {
                shotSource = sourceObject.AddComponent<AudioSource>();
            }
        }

        shotSource.playOnAwake = false;
        shotSource.loop = false;
        shotSource.spatialBlend = 1f;
        shotSource.rolloffMode = AudioRolloffMode.Linear;
        shotSource.minDistance = 28f;
        shotSource.maxDistance = 260f;
        shotSource.dopplerLevel = 0.1f;
    }

    private void SubscribeToShooter(TankShooter shooter)
    {
        if (subscribedShooter == shooter)
        {
            return;
        }

        if (subscribedShooter != null)
        {
            subscribedShooter.Shot -= PlayShotAudio;
        }

        subscribedShooter = shooter;
        if (subscribedShooter != null)
        {
            subscribedShooter.Shot += PlayShotAudio;
        }
    }
}
