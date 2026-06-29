using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyWaveSpawner : MonoBehaviour
{
    private const float DelayBetweenWaves = 10f;
    private const float TerrainEdgeMargin = 38f;

    [SerializeField] private TankHealth playerHealth;
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private GameObject missilePrefab;
    [SerializeField] private EnemyWaveAnnouncement waveAnnouncement;
    [SerializeField] private float delayBetweenWaves = DelayBetweenWaves;
    [SerializeField] private float terrainEdgeMargin = TerrainEdgeMargin;
    [SerializeField] private Transform enemyRoot;

    private readonly List<TankHealth> activeEnemies = new List<TankHealth>();
    private Coroutine waveRoutine;

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

    public void Configure(TankHealth player, GameObject enemyTankPrefab, GameObject projectilePrefab, EnemyWaveAnnouncement announcement)
    {
        playerHealth = player;
        enemyPrefab = enemyTankPrefab;
        missilePrefab = projectilePrefab;
        waveAnnouncement = announcement;
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

        if (Application.isPlaying && waveRoutine == null)
        {
            waveRoutine = StartCoroutine(RunWaves());
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

            if (waveAnnouncement != null)
            {
                yield return waveAnnouncement.Play(waveIndex + 1);
            }

            SpawnWave(waveIndex);
            yield return WaitUntilWaveCleared();
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
            health.Died += HandleEnemyDied;
        }
    }

    private Vector3 GetSpawnPosition(Vector3 offset)
    {
        Vector3 position = playerHealth != null ? playerHealth.transform.position + offset : offset;
        position = ClampToTerrain(position);
        position.y = TankiGameplayBootstrap.GetGroundY(position);
        return position;
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

    private void HandleEnemyDied(TankHealth health)
    {
        if (health != null)
        {
            health.Died -= HandleEnemyDied;
        }

        activeEnemies.Remove(health);
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
    }

    private void OnValidate()
    {
        delayBetweenWaves = Mathf.Max(0f, delayBetweenWaves);
        terrainEdgeMargin = Mathf.Max(0f, terrainEdgeMargin);
    }
}
