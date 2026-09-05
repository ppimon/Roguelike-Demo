using System.Collections;
using UnityEngine;
using Spine.Unity;
using Spine;

[AddComponentMenu("自定义 AI / 普通敌人：远程")]
public class EnemyAI_NormalRanged : MonoBehaviour
{
    public enum State { Idle, Wandering, Chasing, Retreating, Attacking, Leaping, Staggered, Broken, Dead }

    [Header("--- 核心状态 ---")]
    public State currentState = State.Idle;

    [Header("组件引用")]
    public Rigidbody2D rb;
    public EnemyStats myStats;
    public Transform player;
    public SkeletonAnimation skeletonAnimation;

    [Header("动画配置")]
    public string idleAnim = "Idle";
    public string moveAnim = "Move_Loop";
    public string rangedAttackAnim = "Attack";
    public string meleeAttackAnim = "Combat";
    public string hitAnim = "Hit";
    public string brokenAnim = "Broken_Loop";
    public string deathAnim = "Die";

    [Header("距离与行为阈值")]
    public float visionRange = 8f;        // 发现玩家的视野
    public float attackRange = 7f;        // 最远射击距离
    public float safeDistance = 4.5f;     // 小于此距离开始尝试后退拉扯
    public float meleeRange = 1.5f;       // 被近身到此距离触发近战或越过

    [Header("移动与越障参数")]
    public float wanderSpeed = 1.5f;
    public float chaseSpeed = 3f;
    public float retreatSpeed = 2.5f;     // 后退速度稍慢
    public Vector2 leapForce = new Vector2(5f, 6f); // 越过玩家时的 (X, Y) 冲力
    public float leapCooldown = 4f;       // 越过技能的冷却时间

    [Header("环境与判定")]
    public LayerMask obstacleLayer;
    public LayerMask groundLayer;
    public Transform ledgeCheck;          // 检测悬崖（挂在身前）
    public Transform wallCheck;           // 检测墙壁（挂在身前）
    public CombatContactSender meleeHitbox;// 近战判定盒

    [Header("远程发射配置")]
    public GameObject arrowPrefab;        // 箭矢预制体
    public Transform firePoint;           // 箭矢发射点
    public float attackCooldown = 2.5f;   // 攻击冷却

    // 内部状态
    private int facingDirection = 1;
    private float lastAttackTime = -10f;
    private float lastLeapTime = -10f;
    private bool hasAggro = false;
    private float currentAggroTimer = 0f;
    public float loseAggroDuration = 3f;

    // 游荡计时器
    private float stateTimer = 0f;
    public float minIdleTime = 1f;
    public float maxIdleTime = 3f;
    public float minWanderTime = 2f;
    public float maxWanderTime = 4f;

    void Start()
    {
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) player = playerObj.transform;
        }

        if (skeletonAnimation != null)
        {
            skeletonAnimation.AnimationState.Event += HandleSpineEvent;
            skeletonAnimation.AnimationState.Complete += HandleSpineComplete;
        }

        if (myStats != null)
        {
            myStats.OnStagger += HandleStagger;
            myStats.OnBroken += HandleBroken;
            myStats.OnRecover += HandleRecover;
        }

        stateTimer = Random.Range(minIdleTime, maxIdleTime);
    }

    void OnDestroy()
    {
        if (myStats != null)
        {
            myStats.OnStagger -= HandleStagger;
            myStats.OnBroken -= HandleBroken;
            myStats.OnRecover -= HandleRecover;
        }
    }

    // --- 状态事件响应 ---
    void HandleStagger()
    {
        if (currentState == State.Dead || currentState == State.Broken) return;
        StopCurrentActions();
        currentState = State.Staggered;
        PlayAnimation(hitAnim, false, true);
    }

    void HandleBroken()
    {
        if (currentState == State.Dead) return;
        StopCurrentActions();
        currentState = State.Broken;
        PlayAnimation(brokenAnim, true, true);
    }

    void HandleRecover()
    {
        if (currentState == State.Broken)
        {
            currentState = State.Chasing;
            hasAggro = true;
        }
    }

    void StopCurrentActions()
    {
        StopAllCoroutines();
        rb.velocity = Vector2.zero;
        if (meleeHitbox != null) meleeHitbox.StopDamageCalculation();
    }

    public void TriggerDeath()
    {
        if (currentState == State.Dead) return;
        currentState = State.Dead;
        StopCurrentActions();
        rb.isKinematic = true;
        Collider2D myCollider = GetComponent<Collider2D>();
        if (myCollider != null) myCollider.enabled = false;
        PlayAnimation(deathAnim, false, true);
    }

    // --- 核心更新逻辑 ---
    void Update()
    {
        if (player == null) return;

        // 【安全锁】僵直、破防、死亡、攻击或跳跃期间，锁死AI逻辑
        if (currentState == State.Staggered || currentState == State.Broken || currentState == State.Dead ||
            currentState == State.Attacking || currentState == State.Leaping)
            return;

        // 仇恨检测
        if (!hasAggro)
        {
            if (CanSeePlayer())
            {
                hasAggro = true;
                currentState = State.Chasing;
            }
            else
            {
                HandleWanderAndIdle();
                return;
            }
        }
        else
        {
            if (!CanSeePlayer())
            {
                currentAggroTimer += Time.deltaTime;
                if (currentAggroTimer >= loseAggroDuration)
                {
                    hasAggro = false;
                    currentState = State.Idle;
                    return;
                }
            }
            else currentAggroTimer = 0f;
        }

        // 视线永远锁定玩家
        SetFacingDirection(player.position.x > transform.position.x ? 1 : -1);
        float distToPlayer = Vector2.Distance(transform.position, player.position);

        // 行为决策树
        if (distToPlayer <= safeDistance)
        {
            if (CanRetreat())
            {
                // 空间足够，拉扯距离
                Retreat();
            }
            else
            {
                // 没有退路了！
                if (distToPlayer <= meleeRange && Time.time >= lastLeapTime + leapCooldown)
                {
                    // 玩家离得很近，且技能就绪：尝试从玩家头顶越过逃生
                    StartCoroutine(LeapOverPlayerRoutine());
                }
                else
                {
                    // 无法越障或 CD 中，放弃逃跑全力攻击 (距离近用近战，否则射箭)
                    PerformAttack(distToPlayer);
                }
            }
        }
        else if (distToPlayer <= attackRange)
        {
            // 处于安全射击距离，站桩输出
            PerformAttack(distToPlayer);
        }
        else
        {
            // 距离太远，向前追击
            Chase();
        }
    }

    // --- 行为模式 ---

    bool CanRetreat()
    {
        // 探测背后的环境 (注意检测方向是 -facingDirection)
        int backDir = -facingDirection;
        RaycastHit2D wallHit = Physics2D.Raycast(wallCheck.position, Vector2.right * backDir, 1f, obstacleLayer);
        // 悬崖检测射线向后偏移一点
        Vector2 backLedgePos = (Vector2)ledgeCheck.position + new Vector2(backDir * 1.5f, 0);
        RaycastHit2D ledgeHit = Physics2D.Raycast(backLedgePos, Vector2.down, 1.5f, groundLayer);

        // 背后没墙，且脚下有地
        return wallHit.collider == null && ledgeHit.collider != null;
    }

    void Retreat()
    {
        currentState = State.Retreating;
        rb.velocity = new Vector2(-facingDirection * retreatSpeed, rb.velocity.y);
        PlayAnimation(moveAnim, true); // 播着移动动画，但速度是向后的（实现警惕后退）
    }

    void Chase()
    {
        currentState = State.Chasing;
        if (!IsLedgeAhead())
        {
            rb.velocity = new Vector2(facingDirection * chaseSpeed, rb.velocity.y);
            PlayAnimation(moveAnim, true);
        }
        else
        {
            rb.velocity = new Vector2(0, rb.velocity.y);
            PlayAnimation(idleAnim, true);
        }
    }

    IEnumerator LeapOverPlayerRoutine()
    {
        currentState = State.Leaping;
        lastLeapTime = Time.time;
        PlayAnimation(moveAnim, true); // 或者如果你有跳跃动画可以用跳跃动画

        // 给予一个抛物线冲力 (向玩家方向跳，试图跨越)
        rb.velocity = new Vector2(facingDirection * leapForce.x, leapForce.y);

        // 等待落地 (简单用时间控制，或者检测 rb.velocity.y)
        yield return new WaitForSeconds(0.6f);

        rb.velocity = new Vector2(0, rb.velocity.y);
        currentState = State.Chasing;
    }

    void PerformAttack(float dist)
    {
        if (Time.time < lastAttackTime + attackCooldown)
        {
            // 攻击冷却中，原地待机等CD
            rb.velocity = new Vector2(0, rb.velocity.y);
            PlayAnimation(idleAnim, true);
            return;
        }

        currentState = State.Attacking;
        rb.velocity = new Vector2(0, rb.velocity.y);
        lastAttackTime = Time.time;

        if (dist <= meleeRange)
            PlayAnimation(meleeAttackAnim, false);
        else
            PlayAnimation(rangedAttackAnim, false);
    }

    // --- 游荡与通用方法 ---
    void HandleWanderAndIdle()
    {
        stateTimer -= Time.deltaTime;
        if (currentState == State.Idle)
        {
            rb.velocity = new Vector2(0, rb.velocity.y);
            PlayAnimation(idleAnim, true);
            if (stateTimer <= 0)
            {
                currentState = State.Wandering;
                stateTimer = Random.Range(minWanderTime, maxWanderTime);
                SetFacingDirection(Random.value > 0.5f ? 1 : -1);
            }
        }
        else if (currentState == State.Wandering)
        {
            if (IsLedgeAhead() || IsWallAhead()) SetFacingDirection(-facingDirection);
            rb.velocity = new Vector2(facingDirection * wanderSpeed, rb.velocity.y);
            PlayAnimation(moveAnim, true);
            if (stateTimer <= 0)
            {
                currentState = State.Idle;
                stateTimer = Random.Range(minIdleTime, maxIdleTime);
            }
        }
        else
        {
            currentState = State.Idle;
            stateTimer = Random.Range(minIdleTime, maxIdleTime);
        }
    }

    bool CanSeePlayer()
    {
        float dist = Vector2.Distance(transform.position, player.position);
        if (dist > visionRange) return false;
        Vector2 direction = (player.position - transform.position).normalized;
        RaycastHit2D hit = Physics2D.Raycast(transform.position, direction, dist, obstacleLayer);
        return hit.collider == null;
    }

    bool IsLedgeAhead()
    {
        RaycastHit2D hit = Physics2D.Raycast(ledgeCheck.position, Vector2.down, 1.5f, groundLayer);
        return hit.collider == null;
    }

    bool IsWallAhead()
    {
        RaycastHit2D hit = Physics2D.Raycast(wallCheck.position, Vector2.right * facingDirection, 1f, obstacleLayer);
        return hit.collider != null;
    }

    void SetFacingDirection(int dir)
    {
        if (facingDirection == dir) return;
        facingDirection = dir;
        if (skeletonAnimation != null) skeletonAnimation.Skeleton.ScaleX = facingDirection;
        if (meleeHitbox != null)
        {
            Vector3 localPos = meleeHitbox.transform.localPosition;
            localPos.x = Mathf.Abs(localPos.x) * facingDirection;
            meleeHitbox.transform.localPosition = localPos;
        }
        if (firePoint != null)
        {
            Vector3 fpPos = firePoint.localPosition;
            fpPos.x = Mathf.Abs(fpPos.x) * facingDirection;
            firePoint.localPosition = fpPos;
        }
    }

    void PlayAnimation(string animName, bool loop, bool force = false)
    {
        if (skeletonAnimation == null) return;
        var currentTrack = skeletonAnimation.AnimationState.GetCurrent(0);
        if (!force && currentTrack != null && !currentTrack.Loop && !currentTrack.IsComplete) return;

        if (currentTrack == null || currentTrack.Animation.Name != animName)
            skeletonAnimation.AnimationState.SetAnimation(0, animName, loop);
    }

    // --- Spine 事件与回调 ---
    void HandleSpineEvent(TrackEntry trackEntry, Spine.Event e)
    {
        // Spine 触发攻击判定的事件名（需与美术在Spine里配的一致）
        if (e.Data.Name == "OnAttack")
        {
            if (trackEntry.Animation.Name == meleeAttackAnim && meleeHitbox != null)
            {
                meleeHitbox.StartDamageCalculation(1.0f);
                StartCoroutine(StopDamageRoutine(meleeHitbox));
            }
            else if (trackEntry.Animation.Name == rangedAttackAnim && arrowPrefab != null && firePoint != null)
            {
                // 生成箭矢
                GameObject arrowObj = Instantiate(arrowPrefab, firePoint.position, Quaternion.identity);
                EnemyProjectile arrowScript = arrowObj.GetComponent<EnemyProjectile>();
                if (arrowScript != null)
                {
                    // 传入当前朝向和来源 Stats 以计算伤害
                    arrowScript.Initialize(facingDirection, myStats);
                }
            }
        }
    }

    IEnumerator StopDamageRoutine(CombatContactSender hitbox)
    {
        yield return new WaitForSeconds(0.15f);
        if (hitbox != null) hitbox.StopDamageCalculation();
    }

    void HandleSpineComplete(TrackEntry trackEntry)
    {
        if (trackEntry.Animation.Name == deathAnim)
        {
            Destroy(gameObject);
            return;
        }

        if (currentState == State.Dead) return;

        if (trackEntry.Animation.Name == meleeAttackAnim || trackEntry.Animation.Name == rangedAttackAnim)
        {
            currentState = State.Chasing;
            if (meleeHitbox != null) meleeHitbox.StopDamageCalculation();
        }
        else if (trackEntry.Animation.Name == hitAnim)
        {
            if (currentState == State.Staggered)
            {
                currentState = State.Chasing;
                lastAttackTime = Time.time; // 防重置秒刀
            }
        }
    }
}