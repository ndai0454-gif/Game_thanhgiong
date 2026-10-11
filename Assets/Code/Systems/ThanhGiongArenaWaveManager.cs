using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Owns spawns and living enemies for exactly one arena round.</summary>
public sealed class ThanhGiongArenaWaveManager : MonoBehaviour
{
    [Serializable]
    public sealed class Wave
    {
        public string title = "Quân Ân";
        [Min(1)] public int count = 6;
        [Min(.1f)] public float spawnInterval = 12f;
        [Min(1)] public float health = 180f;
        [Min(.1f)] public float moveSpeed = 2.8f;
    }

    public ThanhGiongEnemy enemyPrefab;
    public Transform[] spawnPoints;
    public Transform enemyRoot;
    public Wave[] waves = { new Wave(), new Wave { count = 8, spawnInterval = 14f }, new Wave { count = 8, spawnInterval = 16f, health = 230f, moveSpeed = 3.1f } };
    public event Action<int> WaveCleared;
    public int WaveIndex { get; private set; } = -1;
    public int Spawned { get; private set; }
    public int Defeated { get; private set; }
    public int AliveCount => alive.Count;
    public bool AllSpawned => WaveIndex >= 0 && Spawned >= waves[WaveIndex].count;
    public bool Running { get; private set; }
    public int Generation { get; private set; }
    public IReadOnlyCollection<ThanhGiongEnemy> LivingEnemies => alive;

    readonly HashSet<ThanhGiongEnemy> alive = new HashSet<ThanhGiongEnemy>();
    readonly List<GameObject> roundObjects = new List<GameObject>();
    ThanhGiongCampaignController owner;
    float untilSpawn;
    bool cleared;

    public void Configure(ThanhGiongCampaignController campaign) => owner = campaign;

    public bool StartWave(int index)
    {
        if (Running || alive.Count > 0 || index < 0 || index >= waves.Length ||
            enemyPrefab == null || spawnPoints == null || spawnPoints.Length == 0 || owner == null) return false;
        WaveIndex = index; Spawned = 0; Defeated = 0; cleared = false;
        untilSpawn = 0f; Running = true;
        return true;
    }

    float recoveryCheckTimer;

    void Update()
    {
        if (!Running || Time.timeScale <= 0f || owner == null || !owner.IsBattleActive) return;
        untilSpawn -= Time.deltaTime;
        if (!AllSpawned && untilSpawn <= 0f)
        {
            SpawnOne();
            untilSpawn = Mathf.Max(.1f, waves[WaveIndex].spawnInterval);
        }
        recoveryCheckTimer -= Time.deltaTime;
        if (recoveryCheckTimer <= 0f)
        {
            recoveryCheckTimer = 0.5f;
            RecoverStuckOrFallenEnemies();
        }
        CheckCleared();
    }

    void RecoverStuckOrFallenEnemies()
    {
        if (alive.Count == 0 || spawnPoints == null || spawnPoints.Length == 0) return;
        foreach (var enemy in alive)
        {
            if (enemy == null || !enemy.gameObject.activeInHierarchy || enemy.HealthRatio <= 0) continue;
            Vector3 pos = enemy.transform.position;
            bool outOfBounds = pos.y < -0.8f || Mathf.Abs(pos.x) > 34f || Mathf.Abs(pos.z) > 34f;
            if (outOfBounds)
            {
                Transform safePoint = GetSafeSpawnPoint();
                Vector3 targetPos = safePoint != null ? safePoint.position : new Vector3(0, .2f, 15f);
                enemy.transform.position = targetPos;
                if (enemy.Body != null && !enemy.Body.isKinematic)
                {
                    enemy.Body.linearVelocity = Vector3.zero;
                    enemy.Body.angularVelocity = Vector3.zero;
                    enemy.Body.position = targetPos;
                }
                Debug.LogWarning($"[ArenaWave] Recovered stuck/fallen enemy {enemy.name} back to arena at {targetPos}.", this);
            }
        }
    }

    Transform GetSafeSpawnPoint()
    {
        if (spawnPoints == null || spawnPoints.Length == 0) return null;
        Transform bestPoint = spawnPoints[Spawned % spawnPoints.Length];
        if (owner == null) return bestPoint;

        Vector3 playerPos = owner.transform.position;
        if (bestPoint != null && Vector3.Distance(bestPoint.position, playerPos) >= 7f)
            return bestPoint;

        // Find the spawn point furthest from the player to avoid spawning on top of the player
        float maxDist = -1f;
        Transform furthest = bestPoint;
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            var p = spawnPoints[i];
            if (p == null) continue;
            float d = Vector3.Distance(p.position, playerPos);
            if (d > maxDist)
            {
                maxDist = d;
                furthest = p;
            }
        }
        return furthest;
    }

    void SpawnOne()
    {
        Transform point = GetSafeSpawnPoint();
        if (point == null) { Debug.LogError("Arena: missing spawn point.", this); Running = false; return; }
        var enemy = Instantiate(enemyPrefab, point.position, point.rotation, enemyRoot);
        enemy.gameObject.SetActive(false);
        enemy.maxHealth = waves[WaveIndex].health;
        enemy.moveSpeed = waves[WaveIndex].moveSpeed;
        enemy.isBoss = false;
        enemy.enemyName = $"Quân Ân · đợt {WaveIndex + 1}";
        enemy.gameObject.name = $"Wave{WaveIndex + 1}_Enemy{Spawned + 1}_Round{Generation}";
        roundObjects.Add(enemy.gameObject);
        alive.Add(enemy);
        Spawned++;
        enemy.Initialize(owner, owner.transform);
    }

    public bool ReportDeath(ThanhGiongEnemy enemy)
    {
        // Membership also rejects old rounds and repeated callbacks.
        if (!Running || enemy == null || !alive.Remove(enemy)) return false;
        Defeated++;
        CheckCleared();
        return true;
    }

    void CheckCleared()
    {
        if (cleared || !Running || !AllSpawned || alive.Count != 0) return;
        cleared = true; Running = false;
        WaveCleared?.Invoke(WaveIndex);
    }

    public void StopSpawning() => Running = false;

    public void ResetRound()
    {
        Running = false; Generation++; alive.Clear();
        foreach (GameObject enemy in roundObjects)
        {
            if (enemy == null) continue;
            enemy.SetActive(false);
            Destroy(enemy);
        }
        roundObjects.Clear();
        WaveIndex = -1; Spawned = 0; Defeated = 0; cleared = false; untilSpawn = 0f;
    }
}
