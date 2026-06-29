using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyWaveSpawner : MonoBehaviour
{
    private enum InfiniteEnemyKind
    {
        Normal,
        Maus
    }

    private const float DelayBetweenWaves = 10f;
    private const float TerrainEdgeMargin = 38f;
    private const float GeneratedWallMargin = 28f;
    private const int HealthPickupHealAmount = 25;
    private const float EnemySpawnClearanceRadius = 7.5f;
    private const float EnemySpawnNudgeDistance = 8f;
    private const float HealthPickupSpawnDistance = 10f;
    private const float HealthPickupScale = 4.65f;

    [SerializeField] private TankHealth playerHealth;
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private GameObject bossPrefab;
    [SerializeField] private GameObject missilePrefab;
    [SerializeField] private EnemyWaveAnnouncement waveAnnouncement;
    [SerializeField] private float delayBetweenWaves = DelayBetweenWaves;
    [SerializeField] private float terrainEdgeMargin = TerrainEdgeMargin;
    [SerializeField] private float generatedWallMargin = GeneratedWallMargin;
    [SerializeField] private Transform enemyRoot;
    [SerializeField] private Transform pickupRoot;
    [SerializeField] private bool infiniteMode;

    private readonly List<TankHealth> activeEnemies = new List<TankHealth>();
    private readonly Dictionary<TankHealth, int> enemyWaveIndexes = new Dictionary<TankHealth, int>();
    private Coroutine waveRoutine;
    private static Material healthPickupMaterial;

    private static readonly Vector3[][] WaveOffsets =
    {
        new[]
        {
            new Vector3(0f, 0f, 120f)
        },
        new[]
        {
            new Vector3(-42f, 0f, 155f),
            new Vector3(42f, 0f, 155f)
        },
        new[]
        {
            new Vector3(-62f, 0f, 185f),
            new Vector3(0f, 0f, 210f),
            new Vector3(62f, 0f, 185f)
        },
        new[]
        {
            new Vector3(0f, 0f, 150f),
            new Vector3(-150f, 0f, 0f),
            new Vector3(150f, 0f, 0f),
            new Vector3(0f, 0f, -150f)
        },
        new[]
        {
            new Vector3(0f, 0f, 178f),
            new Vector3(-168f, 0f, 55f),
            new Vector3(-104f, 0f, -144f),
            new Vector3(104f, 0f, -144f),
            new Vector3(168f, 0f, 55f)
        }
    };

    private static readonly InfiniteEnemyKind[][] InfiniteWaveCompositions =
    {
        new[] { InfiniteEnemyKind.Normal },
        new[] { InfiniteEnemyKind.Normal, InfiniteEnemyKind.Maus },
        new[] { InfiniteEnemyKind.Maus },
        new[] { InfiniteEnemyKind.Normal, InfiniteEnemyKind.Normal },
        new[] { InfiniteEnemyKind.Normal, InfiniteEnemyKind.Normal, InfiniteEnemyKind.Normal }
    };

    public void Configure(TankHealth player, GameObject enemyTankPrefab, GameObject bossTankPrefab, GameObject projectilePrefab, EnemyWaveAnnouncement announcement, bool infinite = false)
    {
        playerHealth = player;
        enemyPrefab = enemyTankPrefab;
        bossPrefab = bossTankPrefab;
        missilePrefab = projectilePrefab;
        waveAnnouncement = announcement;
        infiniteMode = infinite;
        delayBetweenWaves = Mathf.Max(0f, delayBetweenWaves);
        terrainEdgeMargin = Mathf.Max(0f, terrainEdgeMargin);

        if (enemyRoot == null)
        {
            GameObject root = GameObject.Find("Wave Enemies");
            if (root == null)
            {
                root = new GameObject("Wave Enemies");
            }

            enemyRoot = root.transform;
        }

        if (pickupRoot == null)
        {
            GameObject root = GameObject.Find("Health Pickups");
            if (root == null)
            {
                root = new GameObject("Health Pickups");
            }

            pickupRoot = root.transform;
        }

        if (Application.isPlaying && waveRoutine == null)
        {
            waveRoutine = StartCoroutine(infiniteMode ? RunInfiniteWaves() : RunWaves());
        }
    }

    private void OnDisable()
    {
        if (waveRoutine != null)
        {
            StopCoroutine(waveRoutine);
            waveRoutine = null;
        }
    }

    private IEnumerator RunWaves()
    {
        ClearOldEnemies();

        for (int waveIndex = 0; waveIndex < WaveOffsets.Length; waveIndex++)
        {
            if (!CanSpawn())
            {
                yield break;
            }

            if (waveIndex > 0 && delayBetweenWaves > 0f)
            {
                yield return new WaitForSeconds(delayBetweenWaves);
            }

            while (PlayerHealthBar.GameplayInputBlocked)
            {
                yield return null;
            }

            RemoveExpiredHealthPickups(waveIndex);

            if (waveAnnouncement != null)
            {
                yield return waveAnnouncement.Play(waveIndex + 1);
            }

            SpawnWave(waveIndex);
            yield return WaitUntilWaveCleared();
        }

        if (CanSpawnBoss())
        {
            if (delayBetweenWaves > 0f)
            {
                yield return new WaitForSeconds(delayBetweenWaves);
            }

            while (PlayerHealthBar.GameplayInputBlocked)
            {
                yield return null;
            }

            RemoveExpiredHealthPickups(WaveOffsets.Length);

            if (waveAnnouncement != null)
            {
                yield return waveAnnouncement.Play(WaveOffsets.Length + 1);
            }

            SpawnBoss();
            yield return WaitUntilWaveCleared();
        }

        waveRoutine = null;
    }

    private IEnumerator RunInfiniteWaves()
    {
        ClearOldEnemies();

        int infiniteWaveIndex = 0;
        while (CanSpawnInfinite())
        {
            if (infiniteWaveIndex > 0 && delayBetweenWaves > 0f)
            {
                yield return new WaitForSeconds(delayBetweenWaves);
            }

            while (PlayerHealthBar.GameplayInputBlocked)
            {
                yield return null;
            }

            RemoveExpiredHealthPickups(infiniteWaveIndex);

            if (waveAnnouncement != null)
            {
                yield return waveAnnouncement.Play(infiniteWaveIndex + 1);
            }

            InfiniteEnemyKind[] composition = infiniteWaveIndex == 0
                ? InfiniteWaveCompositions[0]
                : InfiniteWaveCompositions[Random.Range(0, InfiniteWaveCompositions.Length)];
            SpawnInfiniteWave(composition, infiniteWaveIndex);
            yield return WaitUntilWaveCleared();
            infiniteWaveIndex++;
        }

        waveRoutine = null;
    }

    private bool CanSpawn()
    {
        return playerHealth != null
            && playerHealth.IsAlive
            && enemyPrefab != null
            && missilePrefab != null;
    }

    private bool CanSpawnInfinite()
    {
        return CanSpawn() && bossPrefab != null;
    }

    private bool CanSpawnBoss()
    {
        return playerHealth != null
            && playerHealth.IsAlive
            && bossPrefab != null
            && missilePrefab != null;
    }

    private IEnumerator WaitUntilWaveCleared()
    {
        while (CanSpawn())
        {
            PruneDeadEnemies();
            if (activeEnemies.Count == 0)
            {
                yield break;
            }

            yield return null;
        }
    }

    private void SpawnWave(int waveIndex)
    {
        activeEnemies.Clear();
        Vector3[] offsets = WaveOffsets[Mathf.Clamp(waveIndex, 0, WaveOffsets.Length - 1)];
        for (int i = 0; i < offsets.Length; i++)
        {
            Vector3 position = GetSpawnPosition(offsets[i]);
            Vector3 toPlayer = TankPlaneMath.Flatten(playerHealth.transform.position - position);
            Quaternion rotation = toPlayer.sqrMagnitude > 0.001f
                ? TankPlaneMath.RotationLookingAlong(toPlayer, Vector3.forward)
                : Quaternion.identity;

            GameObject enemy = Instantiate(enemyPrefab, position, rotation, enemyRoot);
            enemy.name = $"Wave {waveIndex + 1} Enemy {i + 1}";
            TankiGameplayBootstrap.ConfigureEnemy(enemy, missilePrefab, playerHealth);

            TankHealth health = enemy.GetComponent<TankHealth>();
            if (health == null)
            {
                continue;
            }

            activeEnemies.Add(health);
            enemyWaveIndexes[health] = waveIndex;
            health.Died += HandleEnemyDied;
        }
    }

    private void SpawnBoss()
    {
        activeEnemies.Clear();
        Vector3 position = GetSpawnPosition(new Vector3(0f, 0f, 230f));
        Vector3 toPlayer = TankPlaneMath.Flatten(playerHealth.transform.position - position);
        Quaternion rotation = toPlayer.sqrMagnitude > 0.001f
            ? TankPlaneMath.RotationLookingAlong(toPlayer, Vector3.forward)
            : Quaternion.identity;

        GameObject enemy = Instantiate(bossPrefab, position, rotation, enemyRoot);
        enemy.name = "Boss Tank Maus";
        TankiGameplayBootstrap.ConfigureEnemy(enemy, missilePrefab, playerHealth);

        TankHealth health = enemy.GetComponent<TankHealth>();
        if (health == null)
        {
            return;
        }

        activeEnemies.Add(health);
        enemyWaveIndexes[health] = WaveOffsets.Length;
        health.Died += HandleEnemyDied;
    }

    private void SpawnInfiniteWave(InfiniteEnemyKind[] composition, int waveIndex)
    {
        activeEnemies.Clear();
        for (int i = 0; i < composition.Length; i++)
        {
            InfiniteEnemyKind enemyKind = composition[i];
            GameObject prefab = enemyKind == InfiniteEnemyKind.Maus ? bossPrefab : enemyPrefab;
            if (prefab == null)
            {
                continue;
            }

            Vector3 position = GetSpawnPosition(GetInfiniteWaveOffset(i, composition.Length));
            Vector3 toPlayer = TankPlaneMath.Flatten(playerHealth.transform.position - position);
            Quaternion rotation = toPlayer.sqrMagnitude > 0.001f
                ? TankPlaneMath.RotationLookingAlong(toPlayer, Vector3.forward)
                : Quaternion.identity;

            GameObject enemy = Instantiate(prefab, position, rotation, enemyRoot);
            enemy.name = enemyKind == InfiniteEnemyKind.Maus
                ? $"Infinite Wave {waveIndex + 1} Maus {i + 1}"
                : $"Infinite Wave {waveIndex + 1} Enemy {i + 1}";
            TankiGameplayBootstrap.ConfigureEnemy(enemy, missilePrefab, playerHealth);

            TankHealth health = enemy.GetComponent<TankHealth>();
            if (health == null)
            {
                continue;
            }

            activeEnemies.Add(health);
            enemyWaveIndexes[health] = waveIndex;
            health.Died += HandleEnemyDied;
        }
    }

    private static Vector3 GetInfiniteWaveOffset(int index, int count)
    {
        if (count <= 1)
        {
            return new Vector3(0f, 0f, 150f);
        }

        if (count == 2)
        {
            return index == 0
                ? new Vector3(-52f, 0f, 165f)
                : new Vector3(52f, 0f, 165f);
        }

        float x = (index - (count - 1) * 0.5f) * 62f;
        float z = index == 1 ? 205f : 178f;
        return new Vector3(x, 0f, z);
    }

    private Vector3 GetSpawnPosition(Vector3 offset)
    {
        Vector3 position = playerHealth != null ? playerHealth.transform.position + offset : offset;
        position = ClampToTerrain(position);
        position = ClampToGeneratedWalls(position);
        position = FindClearEnemySpawnPosition(position);
        position.y = TankiGameplayBootstrap.GetGroundY(position);
        return position;
    }

    private Vector3 FindClearEnemySpawnPosition(Vector3 startPosition)
    {
        Vector3 origin = startPosition;
        Vector3 awayFromPlayer = playerHealth != null
            ? TankPlaneMath.Flatten(origin - playerHealth.transform.position).normalized
            : Vector3.forward;
        if (awayFromPlayer.sqrMagnitude <= 0.001f)
        {
            awayFromPlayer = Vector3.forward;
        }

        Vector3 side = Vector3.Cross(Vector3.up, awayFromPlayer).normalized;
        Vector3[] directions =
        {
            awayFromPlayer,
            side,
            -side,
            -awayFromPlayer,
            (awayFromPlayer + side).normalized,
            (awayFromPlayer - side).normalized,
            (-awayFromPlayer + side).normalized,
            (-awayFromPlayer - side).normalized
        };

        Vector3 bestPosition = origin;
        float bestClearance = GetEnemySpawnClearance(origin);
        if (bestClearance >= EnemySpawnClearanceRadius)
        {
            return origin;
        }

        for (int step = 1; step <= 4; step++)
        {
            for (int i = 0; i < directions.Length; i++)
            {
                Vector3 candidate = origin + directions[i] * (EnemySpawnNudgeDistance * step);
                candidate = ClampToTerrain(candidate);
                candidate = ClampToGeneratedWalls(candidate);
                candidate.y = TankiGameplayBootstrap.GetGroundY(candidate);
                float clearance = GetEnemySpawnClearance(candidate);
                if (clearance > bestClearance)
                {
                    bestClearance = clearance;
                    bestPosition = candidate;
                }

                if (clearance >= EnemySpawnClearanceRadius)
                {
                    return candidate;
                }
            }
        }

        return bestPosition;
    }

    private static float GetEnemySpawnClearance(Vector3 position)
    {
        Collider[] overlaps = Physics.OverlapSphere(
            position + Vector3.up * 1.1f,
            EnemySpawnClearanceRadius,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        float nearestDistance = EnemySpawnClearanceRadius;
        foreach (Collider overlap in overlaps)
        {
            if (!IsBlockingSpawnCollider(overlap))
            {
                continue;
            }

            Vector3 closestPoint = overlap.ClosestPoint(position);
            closestPoint.y = position.y;
            float distance = Vector3.Distance(position, closestPoint);
            nearestDistance = Mathf.Min(nearestDistance, distance);
        }

        return nearestDistance;
    }

    private static bool IsBlockingSpawnCollider(Collider collider)
    {
        if (collider == null || collider.isTrigger || collider is TerrainCollider)
        {
            return false;
        }

        if (collider.GetComponentInParent<ProjectileMovement>() != null)
        {
            return false;
        }

        return true;
    }

    private Vector3 ClampToTerrain(Vector3 position)
    {
        Terrain terrain = Terrain.activeTerrain != null ? Terrain.activeTerrain : FindFirstObjectByType<Terrain>();
        if (terrain == null || terrain.terrainData == null)
        {
            return position;
        }

        Vector3 terrainPosition = terrain.transform.position;
        Vector3 terrainSize = terrain.terrainData.size;
        float margin = Mathf.Min(terrainEdgeMargin, terrainSize.x * 0.45f, terrainSize.z * 0.45f);
        position.x = Mathf.Clamp(position.x, terrainPosition.x + margin, terrainPosition.x + terrainSize.x - margin);
        position.z = Mathf.Clamp(position.z, terrainPosition.z + margin, terrainPosition.z + terrainSize.z - margin);
        return position;
    }

    private Vector3 ClampToGeneratedWalls(Vector3 position)
    {
        if (!TryGetGeneratedWallBounds(out Bounds bounds))
        {
            return position;
        }

        float margin = Mathf.Max(0f, generatedWallMargin);
        float minX = Mathf.Min(bounds.min.x + margin, bounds.max.x);
        float maxX = Mathf.Max(bounds.max.x - margin, bounds.min.x);
        float minZ = Mathf.Min(bounds.min.z + margin, bounds.max.z);
        float maxZ = Mathf.Max(bounds.max.z - margin, bounds.min.z);
        position.x = Mathf.Clamp(position.x, minX, maxX);
        position.z = Mathf.Clamp(position.z, minZ, maxZ);
        return position;
    }

    private static bool TryGetGeneratedWallBounds(out Bounds bounds)
    {
        bounds = default;
        GameObject root = FindGeneratedWallsRoot();
        if (root == null)
        {
            return false;
        }

        bool hasBounds = false;
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
                continue;
            }

            bounds.Encapsulate(renderer.bounds);
        }

        if (hasBounds)
        {
            return true;
        }

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        foreach (Collider collider in colliders)
        {
            if (!hasBounds)
            {
                bounds = collider.bounds;
                hasBounds = true;
                continue;
            }

            bounds.Encapsulate(collider.bounds);
        }

        return hasBounds;
    }

    private static GameObject FindGeneratedWallsRoot()
    {
        GameObject root = GameObject.Find("Generated Walls") ?? GameObject.Find("Generated Wall");
        if (root != null)
        {
            return root;
        }

        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsSortMode.None);
        foreach (Transform transform in transforms)
        {
            if (transform != null && transform.name.Contains("Generated Wall", System.StringComparison.OrdinalIgnoreCase))
            {
                return transform.gameObject;
            }
        }

        return null;
    }

    private void HandleEnemyDied(TankHealth health)
    {
        if (health != null)
        {
            health.Died -= HandleEnemyDied;
        }

        int waveIndex = enemyWaveIndexes.TryGetValue(health, out int storedWaveIndex) ? storedWaveIndex : 0;
        enemyWaveIndexes.Remove(health);
        activeEnemies.Remove(health);
        if (health != null)
        {
            SpawnHealthPickup(health.transform.position, waveIndex);
        }
    }

    private void PruneDeadEnemies()
    {
        for (int i = activeEnemies.Count - 1; i >= 0; i--)
        {
            TankHealth health = activeEnemies[i];
            if (health == null || !health.IsAlive)
            {
                if (health != null)
                {
                    health.Died -= HandleEnemyDied;
                    enemyWaveIndexes.Remove(health);
                }

                activeEnemies.RemoveAt(i);
            }
        }
    }

    private void ClearOldEnemies()
    {
        TankHealth[] healths = FindObjectsByType<TankHealth>(FindObjectsSortMode.None);
        foreach (TankHealth health in healths)
        {
            if (health == null || health.Team != TankTeam.Enemy)
            {
                continue;
            }

            Destroy(health.gameObject);
        }

        enemyWaveIndexes.Clear();
        RemoveExpiredHealthPickups(int.MaxValue);
    }

    private void SpawnHealthPickup(Vector3 deathPosition, int deathWaveIndex)
    {
        if (!CanSpawn())
        {
            return;
        }

        Vector3 pickupPosition = FindHealthPickupPosition(deathPosition);
        GameObject pickupObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        pickupObject.name = $"Health Pickup Wave {deathWaveIndex + 1}";
        pickupObject.transform.SetParent(pickupRoot, true);
        pickupObject.transform.position = pickupPosition;
        pickupObject.transform.localScale = Vector3.one * HealthPickupScale;

        Collider collider = pickupObject.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }

        Renderer renderer = pickupObject.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = GetHealthPickupMaterial();
        }

        HealthPickup pickup = pickupObject.AddComponent<HealthPickup>();
        pickup.Configure(HealthPickupHealAmount, deathWaveIndex + 2);
    }

    private Vector3 FindHealthPickupPosition(Vector3 deathPosition)
    {
        Vector3 toPlayer = playerHealth != null
            ? TankPlaneMath.Flatten(playerHealth.transform.position - deathPosition).normalized
            : Vector3.forward;
        if (toPlayer.sqrMagnitude <= 0.001f)
        {
            toPlayer = Vector3.forward;
        }

        Vector3 side = Vector3.Cross(Vector3.up, toPlayer).normalized;
        Vector3[] directions =
        {
            side,
            -side,
            toPlayer,
            -toPlayer,
            (side + toPlayer).normalized,
            (-side + toPlayer).normalized,
            (side - toPlayer).normalized,
            (-side - toPlayer).normalized
        };

        for (int i = 0; i < directions.Length; i++)
        {
            Vector3 candidate = deathPosition + directions[i] * HealthPickupSpawnDistance;
            candidate = ClampToTerrain(candidate);
            candidate = ClampToGeneratedWalls(candidate);
            candidate.y = TankiGameplayBootstrap.GetGroundY(candidate) + 1.15f;
            if (IsHealthPickupSpaceFree(candidate))
            {
                return candidate;
            }
        }

        Vector3 fallback = ClampToGeneratedWalls(ClampToTerrain(deathPosition));
        fallback.y = TankiGameplayBootstrap.GetGroundY(fallback) + 1.15f;
        return fallback;
    }

    private static bool IsHealthPickupSpaceFree(Vector3 position)
    {
        Collider[] overlaps = Physics.OverlapSphere(position, 1.25f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        foreach (Collider overlap in overlaps)
        {
            if (overlap == null || overlap.isTrigger || overlap is TerrainCollider)
            {
                continue;
            }

            if (overlap.GetComponentInParent<ProjectileMovement>() != null)
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private void RemoveExpiredHealthPickups(int currentWaveIndex)
    {
        HealthPickup[] pickups = FindObjectsByType<HealthPickup>(FindObjectsSortMode.None);
        foreach (HealthPickup pickup in pickups)
        {
            if (pickup != null && pickup.ExpireAtWaveIndex <= currentWaveIndex)
            {
                Destroy(pickup.gameObject);
            }
        }
    }

    private static Material GetHealthPickupMaterial()
    {
        if (healthPickupMaterial != null)
        {
            return healthPickupMaterial;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        healthPickupMaterial = new Material(shader);
        healthPickupMaterial.name = "Runtime Health Pickup Material";
        healthPickupMaterial.color = new Color(0.1f, 1f, 0.22f, 1f);
        if (healthPickupMaterial.HasProperty("_EmissionColor"))
        {
            healthPickupMaterial.EnableKeyword("_EMISSION");
            healthPickupMaterial.SetColor("_EmissionColor", new Color(0.05f, 1.8f, 0.18f, 1f));
        }

        return healthPickupMaterial;
    }

    private void OnValidate()
    {
        delayBetweenWaves = Mathf.Max(0f, delayBetweenWaves);
        terrainEdgeMargin = Mathf.Max(0f, terrainEdgeMargin);
        generatedWallMargin = Mathf.Max(0f, generatedWallMargin);
    }
}
