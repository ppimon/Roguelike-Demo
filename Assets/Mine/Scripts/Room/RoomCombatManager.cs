using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RoomCombatManager : MonoBehaviour
{
    [Header("房间生成配置")]
    public Room room;                 // 自身的Room组件引用
    public BoxCollider2D roomBounds;  // 房间的边界（用于限制射线生成的范围和检测玩家进入）
    public LayerMask wallLayer;       // 地面图层 (用于射线检测)
    public LayerMask obstacleLayer;   // 障碍物图层 (用于防挤压检测)

    [Header("战斗难度与限制")]
    public int roomDifficultyBudget = 0; // 这个值稍后由 LevelGenerator 分配
    public int maxEnemiesPerWave = 4;    // 本房间最大同屏生成数
    public int maxWaves = 3;             // 最大波次数

    [Header("可生成的敌人池")]
    public List<EnemyData> allowedEnemies; // 在 Inspector 中配置该房间允许刷新的怪物种类

    // 内部战斗状态
    private bool combatStarted = false;
    private bool combatCleared = false;
    private int currentWave = 0;
    private int remainingDifficulty;
    private List<GameObject> aliveEnemies = new List<GameObject>();
    private Coroutine combatStartCoroutine;

    void Start()
    {
        if (roomBounds == null) roomBounds = GetComponent<BoxCollider2D>();
        // 将触发器设为true，用于检测玩家进入
        roomBounds.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!combatStarted && !combatCleared && collision.CompareTag("Player"))
        {
            if (combatStartCoroutine == null)
            {
                combatStartCoroutine = StartCoroutine(DelayedStartCombat());
            }
        }
    }

    /// <summary>
    /// 触发战斗
    /// </summary>
    private IEnumerator DelayedStartCombat()
    {
        combatStarted = true; // 立刻标记为已开始，防止玩家进进出出重复触发

        // 缓冲 0.5 秒，让玩家彻底走入房间。
        // （你可以根据玩家移速把这个值调为 0.3f 或 0.8f）
        yield return new WaitForSeconds(0.5f);

        remainingDifficulty = roomDifficultyBudget;
        currentWave = 1;

        LockDoors(true);
        SpawnWave();
    }

    /// <summary>
    /// 封锁或解锁所有已连接的出入口
    /// </summary>
    void LockDoors(bool isLocked)
    {
        foreach (var exit in room.exits)
        {
            if (exit.doorObject != null)
            {
                var doorScript = exit.doorObject.GetComponent<SlidingDoor>();
                if (doorScript != null)
                {
                    doorScript.SetLock(isLocked);
                }
            }
        }
    }

    /// <summary>
    /// 核心算法：生成波次
    /// </summary>
    void SpawnWave()
    {
        if (remainingDifficulty <= 0 || allowedEnemies.Count == 0)
        {
            EndCombat();
            return;
        }

        List<EnemyData> waveEnemies = new List<EnemyData>();
        int waveCost = 0;

        // 1. 挑选这波要生成的怪物 (尽量凑齐难度，但不超过单波数量上限)
        while (waveEnemies.Count < maxEnemiesPerWave && remainingDifficulty > 0)
        {
            // 筛选出花费不大于剩余难度的怪物
            var affordableEnemies = allowedEnemies.Where(e => e.difficultyCost <= remainingDifficulty).ToList();
            if (affordableEnemies.Count == 0) break; // 连最便宜的都买不起了

            // 随机选一个
            EnemyData chosenEnemy = affordableEnemies[Random.Range(0, affordableEnemies.Count)];
            waveEnemies.Add(chosenEnemy);
            remainingDifficulty -= chosenEnemy.difficultyCost;
            waveCost += chosenEnemy.difficultyCost;
        }

        // 2. 寻找生成点并实例化
        List<Vector2> usedSpawnPoints = new List<Vector2>();
        float baseDifficultySum = 0;

        foreach (var enemyData in waveEnemies)
        {
            Vector2 spawnPos;
            if (TryFindSpawnPoint(enemyData.spawnSize, usedSpawnPoints, out spawnPos))
            {
                usedSpawnPoints.Add(spawnPos);
                GameObject enemyObj = Instantiate(enemyData.prefab, spawnPos, Quaternion.identity, transform);

                // --- 【索敌】 ---
                // 1. 找到场景中的玩家 (确保你的玩家 Tag 设置为 "Player")
                GameObject player = GameObject.FindWithTag("Player");

                // 2. 获取敌人身上的 AI 脚本并赋值
                var ai = enemyObj.GetComponent<EnemyAI_NormalMelee>();
                if (ai != null && player != null)
                {
                    ai.player = player.transform;
                    ai.currentState = EnemyAI_NormalMelee.State.Wandering; // 强制进入游荡状态
                }

                // 监听怪物死亡
                EnemyStats stats = enemyObj.GetComponent<EnemyStats>();
                if (stats != null)
                {
                    stats.OnDeath += () => OnEnemyDied(enemyObj);
                }

                aliveEnemies.Add(enemyObj);
                baseDifficultySum += enemyData.difficultyCost;
            }
            else
            {
                // 如果找不到位置，把难度退还
                remainingDifficulty += enemyData.difficultyCost;
                waveCost -= enemyData.difficultyCost;
            }
        }

        // 3. 特殊机制：如果已经是最后一波（或因为没位置退还了难度导致溢出），剩余难度转化为 Buff
        if ((currentWave >= maxWaves && remainingDifficulty > 0) || (usedSpawnPoints.Count < maxEnemiesPerWave && remainingDifficulty > 0 && waveEnemies.Count > usedSpawnPoints.Count))
        {
            if (baseDifficultySum > 0) // 防止除以0
            {
                float buffRatio = remainingDifficulty / baseDifficultySum;
                ApplyBuffToWave(buffRatio);
                Debug.Log($"<color=red>难度溢出 {remainingDifficulty} 点，场上怪物获得 {buffRatio:P0} 的属性提升！</color>");
                remainingDifficulty = 0; // 消耗完毕
            }
        }
    }

    /// <summary>
    /// 自上而下射线寻找生成点
    /// </summary>
    bool TryFindSpawnPoint(Vector2 enemySize, List<Vector2> usedPoints, out Vector2 foundPos)
    {
        foundPos = Vector2.zero;
        int maxAttempts = 15; // 最多尝试15次，防止死循环
        float minDistanceBetweenEnemies = 1.5f;

        Bounds bounds = roomBounds.bounds;
        float safeTopY = bounds.max.y - 1.5f;

        float minX = bounds.min.x + 1.5f;
        float maxX = bounds.max.x - 1.5f;

        for (int i = 0; i < maxAttempts; i++)
        {
            float randomX = Random.Range(minX, maxX);
            Vector2 origin = new Vector2(randomX, safeTopY);

            bool originalQueriesHitTriggers = Physics2D.queriesHitTriggers;

            Physics2D.queriesHitTriggers = false;
            Physics2D.queriesHitTriggers = originalQueriesHitTriggers; // 还原设置

            // 1. 向下打射线，找地面
            RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, bounds.size.y, wallLayer);
            if (hit.collider != null)
            {
                // 2. 算出站立点位置 (脚底的中心点往上抬半个身高)
                Vector2 testPos = hit.point + new Vector2(0, enemySize.y * 0.5f);

                // 3. 检测是否和其他生成的点太近
                bool tooClose = usedPoints.Any(p => Vector2.Distance(p, testPos) < minDistanceBetweenEnemies);
                if (tooClose) continue;

                // 4. 防挤压检测 (画一个盒子看有没有卡进墙里或木箱里)
                Collider2D overlap = Physics2D.OverlapBox(testPos, enemySize * 0.9f, 0f, obstacleLayer | wallLayer);
                if (overlap == null)
                {
                    foundPos = testPos;
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// 为场上的怪物按比例分配剩余难度 Buff
    /// </summary>
    void ApplyBuffToWave(float buffRatio)
    {
        foreach (var enemy in aliveEnemies)
        {
            EnemyStats stats = enemy.GetComponent<EnemyStats>();
            if (stats != null)
            {
                // 提升最大血量并回满
                float addedHealth = stats.maxHealth * buffRatio;
                stats.maxHealth += addedHealth;
                stats.SetHealthToMax(); ; 

                // 提升攻击力、防御力和法抗 (因为之前是用 AddModifier)
                // 这里假设 Stat.cs 里的 baseValue 无法直接公开获取，你需要去 Stat.cs 加一个 public float GetBaseValue() { return baseValue; }
                stats.damage.AddModifier(stats.damage.baseValue * buffRatio);
                stats.defense.AddModifier(stats.defense.baseValue * buffRatio);
                stats.magicResistance.AddModifier(stats.magicResistance.baseValue * buffRatio);

                // 体型稍微变大一点点以示威慑（可选）
                enemy.transform.localScale *= (1f + Mathf.Min(buffRatio * 0.2f, 0.5f));
            }
        }
    }

    /// <summary>
    /// 怪物死亡回调
    /// </summary>
    void OnEnemyDied(GameObject enemy)
    {
        aliveEnemies.Remove(enemy);
        if (aliveEnemies.Count == 0)
        {
            // 当前波次死光了
            if (remainingDifficulty > 0 && currentWave < maxWaves)
            {
                currentWave++;
                Invoke("SpawnWave", 1.5f); // 延迟 1.5 秒刷下一波
            }
            else
            {
                EndCombat();
            }
        }
    }

    /// <summary>
    /// 战斗结束
    /// </summary>
    void EndCombat()
    {
        combatCleared = true;
        LockDoors(false);
        Debug.Log($"<color=green>房间战斗结束！门已解锁。</color>");
        // TODO: 这里可以调用生成宝箱的代码
    }
}