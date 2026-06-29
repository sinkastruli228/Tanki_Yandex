using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class EnemyScreenMarkerDisplay : MonoBehaviour
{
    [SerializeField] private Image markerTemplate;
    [SerializeField] private Canvas canvas;
    [SerializeField] private float edgePadding = 42f;
    [SerializeField] private float markerSize = 44f;
    [SerializeField] private float enemyHeightOffset = 1.2f;

    private readonly List<Image> markerPool = new List<Image>();
    private readonly List<TankHealth> enemies = new List<TankHealth>();

    public void Configure(Image template, Canvas parentCanvas)
    {
        markerTemplate = template;
        canvas = parentCanvas;
        edgePadding = Mathf.Max(0f, edgePadding);
        markerSize = Mathf.Max(1f, markerSize);

        if (markerTemplate != null)
        {
            markerTemplate.raycastTarget = false;
            markerTemplate.gameObject.SetActive(false);
            markerTemplate.rectTransform.sizeDelta = Vector2.one * markerSize;
        }
    }

    private void Update()
    {
        Camera camera = Camera.main;
        if (camera == null || markerTemplate == null)
        {
            HideAllMarkers();
            return;
        }

        CollectEnemies();
        int visibleMarkerCount = 0;
        foreach (TankHealth enemy in enemies)
        {
            if (enemy == null || !enemy.IsAlive)
            {
                continue;
            }

            Vector3 worldPoint = enemy.transform.position + Vector3.up * enemyHeightOffset;
            Vector3 viewportPoint = camera.WorldToViewportPoint(worldPoint);
            bool isBehindCamera = viewportPoint.z < 0f;
            bool isOnScreen = !isBehindCamera
                && viewportPoint.x >= 0f
                && viewportPoint.x <= 1f
                && viewportPoint.y >= 0f
                && viewportPoint.y <= 1f;

            if (isOnScreen)
            {
                continue;
            }

            Image marker = GetMarker(visibleMarkerCount);
            UpdateMarker(marker, viewportPoint, isBehindCamera);
            visibleMarkerCount++;
        }

        for (int i = visibleMarkerCount; i < markerPool.Count; i++)
        {
            markerPool[i].gameObject.SetActive(false);
        }
    }

    private void CollectEnemies()
    {
        enemies.Clear();
        TankHealth[] healths = FindObjectsByType<TankHealth>(FindObjectsSortMode.None);
        foreach (TankHealth health in healths)
        {
            if (health != null && health.Team == TankTeam.Enemy && health.IsAlive)
            {
                enemies.Add(health);
            }
        }
    }

    private Image GetMarker(int index)
    {
        while (markerPool.Count <= index)
        {
            Image marker = Instantiate(markerTemplate, markerTemplate.transform.parent);
            marker.name = $"Enemy Marker {markerPool.Count + 1}";
            marker.raycastTarget = false;
            marker.rectTransform.sizeDelta = Vector2.one * markerSize;
            markerPool.Add(marker);
        }

        Image result = markerPool[index];
        result.gameObject.SetActive(true);
        result.transform.SetAsLastSibling();
        return result;
    }

    private void UpdateMarker(Image marker, Vector3 viewportPoint, bool isBehindCamera)
    {
        RectTransform markerRect = marker.rectTransform;
        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Vector2 screenPoint = new Vector2(viewportPoint.x * Screen.width, viewportPoint.y * Screen.height);
        if (isBehindCamera)
        {
            screenPoint = screenCenter - (screenPoint - screenCenter);
        }

        Vector2 direction = screenPoint - screenCenter;
        if (direction.sqrMagnitude <= 0.001f)
        {
            direction = Vector2.up;
        }

        direction.Normalize();
        Vector2 edgePosition = GetEdgePosition(screenCenter, direction);
        markerRect.position = edgePosition;
        markerRect.localScale = Vector3.one;
        markerRect.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        marker.enabled = marker.sprite != null;
    }

    private Vector2 GetEdgePosition(Vector2 screenCenter, Vector2 direction)
    {
        float halfWidth = Mathf.Max(1f, Screen.width * 0.5f - edgePadding);
        float halfHeight = Mathf.Max(1f, Screen.height * 0.5f - edgePadding);
        float scaleX = Mathf.Abs(direction.x) > 0.001f ? halfWidth / Mathf.Abs(direction.x) : float.PositiveInfinity;
        float scaleY = Mathf.Abs(direction.y) > 0.001f ? halfHeight / Mathf.Abs(direction.y) : float.PositiveInfinity;
        float scale = Mathf.Min(scaleX, scaleY);
        return screenCenter + direction * scale;
    }

    private void HideAllMarkers()
    {
        foreach (Image marker in markerPool)
        {
            if (marker != null)
            {
                marker.gameObject.SetActive(false);
            }
        }

        if (markerTemplate != null)
        {
            markerTemplate.gameObject.SetActive(false);
        }
    }

    private void OnValidate()
    {
        edgePadding = Mathf.Max(0f, edgePadding);
        markerSize = Mathf.Max(1f, markerSize);
        enemyHeightOffset = Mathf.Max(0f, enemyHeightOffset);
    }
}
