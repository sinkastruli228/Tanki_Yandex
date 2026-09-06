using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class MainMenuController : MonoBehaviour
{
    private GarageMenuView view;
    private Camera sceneCamera;
    private GameObject player, showcase;
    private Transform turret;
    private Quaternion turretHome, playerRotation;
    private Vector3 parking;
    private int skin;
    private bool busy = true, playing;
    private float framedAspect;
    private AudioSource shotAudio;
    private Material flashMaterial, smokeMaterial;
    public int PreviewSkin => skin;
    public bool IsBusy => busy;

    public void Configure(GarageMenuView menu, Camera camera, GameObject tank)
    {
        view = menu; sceneCamera = camera; player = tank;
        parking = player.transform.position; playerRotation = player.transform.rotation;
        skin = TankGarageProgress.SelectedSkin;
        view.Play = () => BeginBattle(false); view.Infinite = () => BeginBattle(true);
        view.Previous = () => ChangeSkin(-1); view.Next = () => ChangeSkin(1);
        view.Buy = () => { if (!busy && TankGarageProgress.TryBuy(skin)) Refresh(); };
        view.Secret = () => { if (!busy) StartCoroutine(SwitchSkin(3, 1)); };
        view.Exit = TankiGameplayBootstrap.QuitGame;
        TankGarageProgress.Changed += Refresh;
        AudioListener.volume = PlayerPrefs.GetInt("Tanki.AudioMuted", 0) == 1 ? 0 : 1;
        shotAudio = gameObject.AddComponent<AudioSource>();
        shotAudio.playOnAwake = false; shotAudio.spatialBlend = 0; shotAudio.volume = .35f;
        sceneCamera.GetComponent<TopDownCameraFollow>().enabled = false;
        player.SetActive(false);
        Time.timeScale = 0; PlayerHealthBar.GameplayInputBlocked = true;
        Cursor.visible = true; Cursor.lockState = CursorLockMode.None;
        PlaceCamera(); Refresh();
        StartCoroutine(OpenGarage());
    }

    private void Refresh() { if (view != null) view.Refresh(skin, busy); }
    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (playing) TankiGameplayBootstrap.ReturnToMainMenu();
            else if (!busy) view.ToggleSettings();
        }
        if (!busy && !playing && showcase != null && Mathf.Abs(framedAspect - sceneCamera.aspect) > .01f)
        {
            FrameShowcase(out var position, out var rotation);
            sceneCamera.transform.SetPositionAndRotation(position, rotation);
        }
        if (!busy && !playing && turret != null)
            turret.localRotation = Quaternion.AngleAxis(Mathf.Sin(Time.unscaledTime * .65f) * 3, Vector3.up) * turretHome;
    }
    private void PlaceCamera()
    {
        sceneCamera.fieldOfView = 40;
        sceneCamera.transform.position = parking + new Vector3(17, 10, -23);
        var look = parking + Vector3.up * 1.5f;
        var rotation = Quaternion.LookRotation(look - sceneCamera.transform.position);
        sceneCamera.transform.rotation = Quaternion.LookRotation(look + rotation * Vector3.right * 4 - sceneCamera.transform.position);
    }
    private IEnumerator OpenGarage()
    {
        view.TravelTo(0, true);
        StartCoroutine(AnimateMenu(true));
        yield return SwitchSkin(skin, 1);
    }
    private void ChangeSkin(int direction)
    {
        if (!busy) StartCoroutine(SwitchSkin((skin + direction + 3) % 3, direction));
    }
    private IEnumerator AnimateMenu(bool entering)
    {
        float duration = entering ? .35f : .42f;
        for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        { view.TravelTo(elapsed / duration, entering); yield return null; }
        view.TravelTo(1, entering);
    }
    private IEnumerator SwitchSkin(int next, int direction)
    {
        busy = true; Refresh();
        Vector3 travel = sceneCamera.transform.right; travel.y = 0; travel.Normalize();
        if (showcase != null)
        {
            Vector3 from = showcase.transform.position;
            for (float t = 0; t < .24f; t += Time.unscaledDeltaTime)
            {
                showcase.transform.position = from - travel * direction * 45 * Mathf.Pow(t / .24f, 2);
                yield return null;
            }
            Destroy(showcase);
        }
        skin = next;
        if (skin < 3 && TankGarageProgress.Owns(skin)) TankGarageProgress.Select(skin);
        bool first = showcase == null;
        CreateShowcase(); Refresh();
        Vector3 cameraFrom = sceneCamera.transform.position;
        Quaternion rotationFrom = sceneCamera.transform.rotation;
        FrameShowcase(out var cameraTo, out var rotationTo);
        if (first) { cameraFrom = cameraTo; rotationFrom = rotationTo; }
        Vector3 home = showcase.transform.position;
        Quaternion facing = showcase.transform.rotation;
        for (float t = 0; t < .36f; t += Time.unscaledDeltaTime)
        {
            float u = 1 - Mathf.Pow(1 - t / .36f, 3);
            sceneCamera.transform.position = Vector3.Lerp(cameraFrom, cameraTo, u);
            sceneCamera.transform.rotation = Quaternion.Slerp(rotationFrom, rotationTo, u);
            showcase.transform.position = home + travel * direction * 45 * (1 - u);
            showcase.transform.rotation = facing * Quaternion.Euler(0, direction * 12 * (1 - u), 0);
            if (turret != null) turret.localRotation = Quaternion.AngleAxis(direction * 28 * (1 - u), Vector3.up) * turretHome;
            yield return null;
        }
        showcase.transform.SetPositionAndRotation(home, facing);
        sceneCamera.transform.SetPositionAndRotation(cameraTo, rotationTo);
        if (turret != null)
        {
            turret.localRotation = turretHome;
            StartCoroutine(CosmeticShot(turret));
        }
        busy = false; Refresh();
    }
    private void CreateShowcase()
    {
        var prefab = TankiGameplayBootstrap.LoadGarageTank(skin);
        showcase = CopyVisual(prefab.transform, null).gameObject;
        showcase.name = "Garage Showcase Tank";
        showcase.transform.localScale = player.transform.localScale * (skin == 3 ? 1.5f : 1);
        showcase.transform.SetPositionAndRotation(parking, playerRotation * Quaternion.Euler(0, 180, 0));
        turret = TankiGameplayBootstrap.FindGarageTurret(showcase.transform);
        if (turret != null) turretHome = turret.localRotation;
    }
    private void FrameShowcase(out Vector3 position, out Quaternion rotation)
    {
        var renderers = showcase.GetComponentsInChildren<Renderer>();
        Bounds bounds = renderers[0].bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        rotation = Quaternion.LookRotation(new Vector3(-17, -10, 23));
        Vector3 right = rotation * Vector3.right, up = rotation * Vector3.up, forward = rotation * Vector3.forward;
        float projectedWidth = Vector3.Dot(bounds.size, new Vector3(Mathf.Abs(right.x), Mathf.Abs(right.y), Mathf.Abs(right.z)));
        float projectedHeight = Vector3.Dot(bounds.size, new Vector3(Mathf.Abs(up.x), Mathf.Abs(up.y), Mathf.Abs(up.z)));
        float tangent = Mathf.Tan(20 * Mathf.Deg2Rad);
        framedAspect = sceneCamera.aspect;
        float distance = Mathf.Max(projectedWidth / (2 * tangent * framedAspect * .51f), projectedHeight / (2 * tangent * .55f));
        position = bounds.center - forward * distance + right * (.125f * 2 * distance * tangent * framedAspect);
    }
    // Copy only the visual hierarchy: previews never run damage, physics or rewards.
    private static Transform CopyVisual(Transform source, Transform parent)
    {
        var copy = new GameObject(source.name).transform;
        copy.SetParent(parent, false);
        copy.localPosition = source.localPosition; copy.localRotation = source.localRotation; copy.localScale = source.localScale;
        var mesh = source.GetComponent<MeshFilter>(); var renderer = source.GetComponent<MeshRenderer>();
        if (mesh != null && renderer != null)
        {
            copy.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh.sharedMesh;
            var output = copy.gameObject.AddComponent<MeshRenderer>();
            output.sharedMaterials = renderer.sharedMaterials;
            output.shadowCastingMode = renderer.shadowCastingMode;
        }
        foreach (Transform child in source) CopyVisual(child, copy);
        return copy;
    }
    private IEnumerator CosmeticShot(Transform shotTurret)
    {
        if (flashMaterial == null)
        {
            flashMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            flashMaterial.color = new Color(1, .66f, .18f);
            smokeMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            smokeMaterial.color = new Color(.73f, .67f, .53f);
        }
        Vector3 forward = shotTurret.forward;
        var muzzlePoint = TankiGameplayBootstrap.CreateMuzzlePoint(shotTurret);
        Vector3 muzzle = muzzlePoint.position;
        Destroy(muzzlePoint.gameObject);
        var flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(flash.GetComponent<Collider>());
        flash.name = "Garage Cosmetic Shot";
        flash.GetComponent<Renderer>().sharedMaterial = flashMaterial;
        flash.transform.position = muzzle;
        var smoke = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(smoke.GetComponent<Collider>());
        smoke.GetComponent<Renderer>().sharedMaterial = smokeMaterial;
        Vector3 home = shotTurret.localPosition;
        var clip = TankiGameplayBootstrap.LoadGarageShot();
        if (clip != null) shotAudio.PlayOneShot(clip);
        for (float t = 0; t < .32f; t += Time.unscaledDeltaTime)
        {
            if (shotTurret == null) break;
            float u = t / .32f;
            flash.SetActive(t < .085f);
            flash.transform.localScale = Vector3.one * Mathf.Lerp(1.2f, .1f, u);
            smoke.transform.position = muzzle + forward * u * 1.8f + Vector3.up * u * .7f;
            smoke.transform.localScale = Vector3.one * Mathf.Sin(u * Mathf.PI) * .85f;
            shotTurret.localPosition = home - shotTurret.parent.InverseTransformDirection(forward) * .22f * Mathf.Sin(u * Mathf.PI);
            yield return null;
        }
        if (shotTurret != null) shotTurret.localPosition = home;
        Destroy(flash); Destroy(smoke);
    }
    public void BeginBattle(bool infinite)
    {
        if (busy || playing || (skin < 3 && !TankGarageProgress.Owns(skin))) return;
        StartCoroutine(Launch(infinite));
    }
    private IEnumerator Launch(bool infinite)
    {
        busy = true; Refresh(); view.Group.interactable = false; view.SettingsPanel.SetActive(false);
        yield return AnimateMenu(false);
        Vector3 from = sceneCamera.transform.position;
        Quaternion fromRotation = sceneCamera.transform.rotation;
        float fromFov = sceneCamera.fieldOfView;
        var hud = TankiGameplayBootstrap.PrepareBattleFromGarage(skin, infinite);
        Vector3 destination = sceneCamera.transform.position;
        Quaternion destinationRotation = sceneCamera.transform.rotation;
        Quaternion parkedRotation = showcase.transform.rotation;
        Destroy(showcase);
        sceneCamera.transform.SetPositionAndRotation(from, fromRotation);
        var pieces = new List<GarageMenuView.Travel>();
        foreach (Transform child in hud.transform)
        {
            if (child.name == "Health Bar Background" || child.name == "Nitro Bar Background" || child.name == "Special Charge Background")
            {
                var rect = child as RectTransform;
                pieces.Add(new GarageMenuView.Travel { rect = rect, home = rect.anchoredPosition, side = rect.anchorMin.x >= .75f ? 1 : -1 });
            }
        }
        float width = ((RectTransform)hud.transform).rect.width + 350;
        foreach (var p in pieces) p.rect.anchoredPosition = p.home + Vector2.right * p.side * width;
        hud.SetActive(true);
        for (float t = 0; t < 1.3f; t += Time.unscaledDeltaTime)
        {
            float u = GarageUiMotion.Smooth(Mathf.Clamp01(t / 1.1f));
            sceneCamera.transform.position = Vector3.Lerp(from, destination, u) + Vector3.up * Mathf.Sin(u * Mathf.PI) * 4;
            sceneCamera.transform.rotation = Quaternion.Slerp(fromRotation, destinationRotation, u);
            sceneCamera.fieldOfView = Mathf.Lerp(fromFov, 58, u);
            player.transform.rotation = Quaternion.Slerp(parkedRotation, playerRotation, u);
            for (int i = 0; i < pieces.Count; i++)
            {
                var p = pieces[i];
                float h = GarageUiMotion.OutBack(Mathf.Clamp01((t - .55f - i * .045f) / .55f));
                p.rect.anchoredPosition = Vector2.LerpUnclamped(p.home + Vector2.right * p.side * width, p.home, h);
            }
            yield return null;
        }
        foreach (var p in pieces) p.rect.anchoredPosition = p.home;
        player.transform.rotation = playerRotation;
        sceneCamera.transform.SetPositionAndRotation(destination, destinationRotation);
        sceneCamera.fieldOfView = 58;
        view.gameObject.SetActive(false);
        playing = true; busy = false;
        TankiGameplayBootstrap.FinishBattleFromGarage();
    }
    private void OnDestroy()
    {
        TankGarageProgress.Changed -= Refresh;
        if (showcase != null) Destroy(showcase);
        if (flashMaterial != null) Destroy(flashMaterial);
        if (smokeMaterial != null) Destroy(smokeMaterial);
    }
}
