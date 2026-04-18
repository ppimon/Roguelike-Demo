using System.Collections;
using UnityEngine;
using Spine.Unity;
using Spine;

[AddComponentMenu("自定义 AI / 普通敌人：近战")]
public class EnemyAI_NormalMelee : MonoBehaviour
{
    public enum State { Idle, Wandering, Chasing, Attacking, Dodging, Staggered, Broken }

    [Header("--- 普通敌人：近战 ---")]
    public State currentState = State.Idle;

    [Header("组件引用")]
    public Rigidbody2D rb;
    public EnemyStats myStats;
    public Transform player;
    public SkeletonAnimation skeletonAnimation;

    [Header("动画名称配置")]
    public string idleAnim = "Idle";
    public string runAnim = "Run_Loop";
    public string dodgeAnim = "Dodge";
    public string attack1Anim = "Attack_1";
    public string attack2Anim = "Attack_2";
    // 【新增】受击与击破动画名
    public string hitAnim = "Hit";
    public string brokenAnim = "Broken_Loop";

    [Header("视野与环境检测")]
    public float visionRange = 6f;
    public float loseAggroDuration = 3f;
    public LayerMask obstacleLayer;
    public LayerMask groundLayer;
    public Transform ledgeCheck;
    public float ledgeCheckDist = 1f;

    private float currentAggroTimer = 0f;
    private bool hasAggro = false;

    [Header("游荡状态参数")]
    public float wanderSpeed = 1.5f;
    public float minIdleTime = 1f;
    public float maxIdleTime = 3f;
    public float minWanderTime = 2f;
    public float maxWanderTime = 4f;
    private float stateTimer = 0f;

    [Header("移动与闪避参数")]
    public float moveSpeed = 3f;
    public float dodgeSpeed = 10f;
    public float dodgeDuration = 0.2f;
    public float dodgeCooldown = 5f;

    [Header("判定设置")]
    public CombatContactSender shortAttackHitbox;
    public CombatContactSender midAttackHitbox;
    public Vector2 attackDecisionOffset = new Vector2(0.5f, 0.5f);
    public float shortRange = 1.2f;
    public float midRange = 2.5f;
    public float sharedAttackCD = 2f;

    private float lastAttackTime = -10f;
    private float lastDodgeTime = -10f;
    private int facingDirection = 1;

    void Start()
    {
        //生成时自动寻找玩家
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                player = playerObj.transform;
            }
            else
            {
                Debug.LogError($"{gameObject.name} 找不到 Tag 为 Player 的物体！请检查场景！");
            }
        }

        if (skeletonAnimation != null)
        {
            skeletonAnimation.AnimationState.Event += HandleSpineEvent;
            skeletonAnimation.AnimationState.Complete += HandleSpineComplete;
        }

        // 【新增】订阅受击打断与击破事件
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
        // 养成好习惯，销毁时取消订阅
        if (myStats != null)
        {
            myStats.OnStagger -= HandleStagger;
            myStats.OnBroken -= HandleBroken;
            myStats.OnRecover -= HandleRecover;
        }
    }

    // --- 状态响应方法 ---

    // 1. 响应小僵直 (力度判定通过)
    void HandleStagger()
    {
        if (currentState == State.Broken) return; // 如果已经被击破在地了，就不响应小僵直

        StopCurrentActions();
        currentState = State.Staggered;
        PlayAnimation(hitAnim, false, true); // 强制播放受击动画
    }

    // 2. 响应击破 (韧性槽归零)
    void HandleBroken()
    {
        StopCurrentActions();
        currentState = State.Broken;
        PlayAnimation(brokenAnim, true, true); // 强制播放击破动画 (循环跪地等)
    }

    // 3. 从击破恢复
    void HandleRecover()
    {
        if (currentState == State.Broken)
        {
            currentState = State.Chasing;
            hasAggro = true; // 挨完打立刻记仇
        }
    }

    // 中断当前的任何行动（攻击判定、位移协程等）
    void StopCurrentActions()
    {
        StopAllCoroutines();
        rb.velocity = Vector2.zero; // 停步
        if (shortAttackHitbox != null) shortAttackHitbox.StopDamageCalculation();
        if (midAttackHitbox != null) midAttackHitbox.StopDamageCalculation();
    }

    void Update()
    {
        if (player == null) return;

        // 【修改】僵直或破防时，停止执行后续 AI 逻辑
        if (currentState == State.Staggered || currentState == State.Broken ||
            currentState == State.Attacking || currentState == State.Dodging)
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
                    currentAggroTimer = 0f;
                    currentState = State.Idle;
                    stateTimer = Random.Range(minIdleTime, maxIdleTime);
                    return;
                }
            }
            else
            {
                currentAggroTimer = 0f;
            }
        }

        // 追击与出招逻辑
        SetFacingDirection(player.position.x > transform.position.x ? 1 : -1);

        float distanceToPlayerBody = Vector2.Distance(transform.position, player.position);

        if (distanceToPlayerBody <= shortRange && Time.time >= lastDodgeTime + dodgeCooldown)
        {
            StartCoroutine(DodgeRoutine());
            return;
        }

        Vector2 attackCenter = GetAttackDecisionCenter();
        float distanceToAttackCenter = Vector2.Distance(attackCenter, player.position);

        if (distanceToAttackCenter <= midRange && Time.time >= lastAttackTime + sharedAttackCD)
        {
            TriggerAttack(distanceToAttackCenter);
            return;
        }

        // 追击移动
        if (distanceToAttackCenter > shortRange)
        {
            if (IsLedgeAhead())
            {
                rb.velocity = new Vector2(0, rb.velocity.y);
                PlayAnimation(idleAnim, true);
            }
            else
            {
                rb.velocity = new Vector2(facingDirection * moveSpeed, rb.velocity.y);
                PlayAnimation(runAnim, true);
            }
        }
        else
        {
            rb.velocity = new Vector2(0, rb.velocity.y);
            PlayAnimation(idleAnim, true);
        }
    }

    void PlayAnimation(string animName, bool loop, bool force = false)
    {
        if (skeletonAnimation == null) return;
        var currentTrack = skeletonAnimation.AnimationState.GetCurrent(0);
        if (!force && currentTrack != null && !currentTrack.Loop && !currentTrack.IsComplete)
        {
            return;
        }

        if (currentTrack == null || currentTrack.Animation.Name != animName)
        {
            skeletonAnimation.AnimationState.SetAnimation(0, animName, loop);
        }
    }

    bool IsLedgeAhead()
    { /* ...原逻辑保持不变... */
        if (ledgeCheck == null) return false;
        RaycastHit2D hit = Physics2D.Raycast(ledgeCheck.position, Vector2.down, ledgeCheckDist, groundLayer);
        return hit.collider == null;
    }

    bool CanSeePlayer()
    { /* ...原逻辑保持不变... */
        float dist = Vector2.Distance(transform.position, player.position);
        if (dist > visionRange) return false;
        int dirToPlayer = player.position.x > transform.position.x ? 1 : -1;
        if (dirToPlayer != facingDirection && dist > 1.5f) return false;
        Vector2 direction = (player.position - transform.position).normalized;
        RaycastHit2D hit = Physics2D.Raycast(transform.position, direction, dist, obstacleLayer);
        return hit.collider == null;
    }

    void HandleWanderAndIdle()
    { /* ...原逻辑保持不变... */
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
            if (IsLedgeAhead()) SetFacingDirection(-facingDirection);
            rb.velocity = new Vector2(facingDirection * wanderSpeed, rb.velocity.y);
            PlayAnimation(runAnim, true);
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

    void SetFacingDirection(int dir)
    { /* ...原逻辑保持不变... */
        if (facingDirection == dir) return;
        facingDirection = dir;
        if (skeletonAnimation != null) skeletonAnimation.Skeleton.ScaleX = facingDirection;
        FlipHitboxLocalX(shortAttackHitbox);
        FlipHitboxLocalX(midAttackHitbox);
        if (ledgeCheck != null)
        {
            Vector3 ledgePos = ledgeCheck.localPosition;
            ledgePos.x = Mathf.Abs(ledgePos.x) * facingDirection;
            ledgeCheck.localPosition = ledgePos;
        }
    }

    void FlipHitboxLocalX(CombatContactSender hitbox)
    { /* ...原逻辑保持不变... */
        if (hitbox != null)
        {
            Vector3 localPos = hitbox.transform.localPosition;
            localPos.x = Mathf.Abs(localPos.x) * facingDirection;
            hitbox.transform.localPosition = localPos;
        }
    }

    Vector2 GetAttackDecisionCenter()
    { /* ...原逻辑保持不变... */
        return (Vector2)transform.position + new Vector2(attackDecisionOffset.x * facingDirection, attackDecisionOffset.y);
    }

    IEnumerator DodgeRoutine()
    { /* ...原逻辑保持不变... */
        currentState = State.Dodging;
        lastDodgeTime = Time.time;
        PlayAnimation(dodgeAnim, false, true);
        rb.velocity = new Vector2(-facingDirection * dodgeSpeed, rb.velocity.y);
        yield return new WaitForSeconds(dodgeDuration);
        rb.velocity = new Vector2(0, rb.velocity.y);
        currentState = State.Chasing;
    }

    void TriggerAttack(float currentDecisionDistance)
    { /* ...原逻辑保持不变... */
        currentState = State.Attacking;
        rb.velocity = new Vector2(0, rb.velocity.y);
        bool useShortAttack = (currentDecisionDistance <= shortRange) && (Random.value > 0.5f);
        string animToPlay = useShortAttack ? attack1Anim : attack2Anim;
        PlayAnimation(animToPlay, false);
    }

    void HandleSpineEvent(TrackEntry trackEntry, Spine.Event e)
    { /* ...原逻辑保持不变... */
        if (e.Data.Name == "OnAttack")
        {
            if (trackEntry.Animation.Name == attack1Anim && shortAttackHitbox != null)
            {
                shortAttackHitbox.StartDamageCalculation(1.0f);
                StartCoroutine(StopDamageRoutine(shortAttackHitbox));
            }
            else if (trackEntry.Animation.Name == attack2Anim && midAttackHitbox != null)
            {
                midAttackHitbox.StartDamageCalculation(1.0f);
                StartCoroutine(StopDamageRoutine(midAttackHitbox));
            }
        }
    }

    IEnumerator StopDamageRoutine(CombatContactSender activeHitbox)
    { /* ...原逻辑保持不变... */
        yield return new WaitForSeconds(0.1f);
        if (activeHitbox != null) activeHitbox.StopDamageCalculation();
    }

    void HandleSpineComplete(TrackEntry trackEntry)
    {
        if (trackEntry.Animation.Name == attack1Anim || trackEntry.Animation.Name == attack2Anim)
        {
            currentState = State.Chasing;
            lastAttackTime = Time.time;

            if (shortAttackHitbox != null) shortAttackHitbox.StopDamageCalculation();
            if (midAttackHitbox != null) midAttackHitbox.StopDamageCalculation();
        }
        else if (trackEntry.Animation.Name == dodgeAnim)
        {
            currentState = State.Chasing;
        }
        // 【新增】如果小僵直动画播放完毕，恢复追击
        else if (trackEntry.Animation.Name == hitAnim)
        {
            if (currentState == State.Staggered)
            {
                currentState = State.Chasing;
                // 为了防止怪物刚出硬直瞬间秒开攻击，给它重置一下攻击时间
                lastAttackTime = Time.time;
            }
        }
    }
}