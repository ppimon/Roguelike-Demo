using UnityEngine;
using Spine.Unity;
using Spine;
using System.Collections;

public class PlayerController : MonoBehaviour
{
    [Header("参数配置")]
    public float moveSpeed = 8f;
    public float jumpForce = 12f;
    public LayerMask groundLayer;
    public Transform groundCheck;
    public float checkRadius = 0.2f;

    [Header("Spine 动画名称")]
    public string idleAnim = "Idle";
    public string runAnim = "Run";
    public string jumpAnim = "Jump";
    public string onAirAnim = "OnAir";
    public string attackAnim = "Attack";

    [Header("组件引用")]
    public Rigidbody2D rb;
    public SkeletonAnimation skeletonAnimation;
    public PlayerStats myStats;
    public Collider2D attackHitbox;
    public EnemyAI_NormalMelee EnemyAI;
    public CombatContactSender playerHitbox;

    [Header("攻击矩形范围（相对于玩家）")]
    public float attackRectWidth = 1.5f;
    public float attackRectHeight = 1.5f;
    public Vector2 attackRectOffset = Vector2.zero;

    [Header("自动战斗")]
    public bool autoCombatEnabled = false;
    public float autoCombatSearchRadius = 10f;
    public float retreatMinTime = 2f;
    public float retreatMaxTime = 6f;

    // 状态标志
    private bool isGrounded;
    public bool isAttacking;
    private bool isJumping;
    private float horizontalInput;
    private bool canCancelAttack = false;
    public float attackStartTime;
    public float attackWindupDuration = 0.25f; // 前摇时间

    // 自动战斗专用
    private bool isAutoRetreating = false;
    private bool pendingRetreat = false;
    private Transform currentAutoTarget;
    private float retreatTimer = 0f;

    void Start()
    {
        skeletonAnimation.AnimationState.Complete += OnAnimationComplete;
        skeletonAnimation.AnimationState.Event += HandleSpineEvent;
    }

    void Update()
    {
        // 快捷键 F 切换自动战斗
        if (Input.GetKeyDown(KeyCode.F))
        {
            autoCombatEnabled = !autoCombatEnabled;
            if (!autoCombatEnabled)
            {
                isAutoRetreating = false;
                pendingRetreat = false;
                currentAutoTarget = null;
                retreatTimer = 0f;
            }
        }

        if (autoCombatEnabled)
            AutoCombatUpdate();
        else
            ManualInputUpdate();

        // 只有在非攻击状态下才由移动逻辑接管动画（修复攻击动画被覆盖问题）
        HandleMovementAnimation();
        HandleFlip();
    }

    void FixedUpdate()
    {
        float currentSpeed = isAttacking ? 0f : moveSpeed;
        rb.velocity = new Vector2(horizontalInput * currentSpeed, rb.velocity.y);
    }

    // ==================== 手动模式 ====================
    void ManualInputUpdate()
    {
        horizontalInput = Input.GetAxisRaw("Horizontal");

        isGrounded = Physics2D.OverlapCircle(groundCheck.position, checkRadius, groundLayer);
        if (isGrounded && rb.velocity.y <= 0.1f)
            isJumping = false;

        // 攻击可取消区间内，移动或跳跃打断攻击
        if (isAttacking && canCancelAttack)
        {
            if (Input.GetButtonDown("Jump") || Mathf.Abs(horizontalInput) > 0.1f)
                InterruptAttack();
        }

        if (!isAttacking || canCancelAttack)
        {
            if (Input.GetButtonDown("Jump") && isGrounded)
                PerformJump();
            else if (Input.GetMouseButtonDown(0) && !isAttacking)
                PerformAttack();
        }
    }

    // ==================== 自动战斗核心 ====================
    void AutoCombatUpdate()
    {
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, checkRadius, groundLayer);
        if (isGrounded && rb.velocity.y <= 0.1f)
            isJumping = false;

        currentAutoTarget = FindNearestEnemy();

        if (currentAutoTarget == null)
        {
            horizontalInput = 0f;
            isAutoRetreating = false;
            pendingRetreat = false;
            retreatTimer = 0f;
            return;
        }

        // 攻击结束后请求撤退
        if (pendingRetreat && !isAttacking && !isAutoRetreating)
            StartAutoRetreat();

        if (isAutoRetreating)
        {
            if (currentAutoTarget == null)
            {
                StopAutoRetreat();
                horizontalInput = 0f;
                return;
            }

            retreatTimer -= Time.deltaTime;
            if (retreatTimer <= 0f)
            {
                StopAutoRetreat();
                horizontalInput = 0f;
                return;
            }

            // 远离敌人
            int retreatDir = (currentAutoTarget.position.x > transform.position.x) ? -1 : 1;
            horizontalInput = retreatDir;
        }
        else
        {
            if (!isAttacking)
            {
                if (IsTargetInAttackRange(currentAutoTarget))
                {
                    horizontalInput = 0f;
                    PerformAttack();
                }
                else
                {
                    int moveDir = (currentAutoTarget.position.x > transform.position.x) ? 1 : -1;
                    horizontalInput = moveDir;
                }
            }
            else
            {
                horizontalInput = 0f;
            }
        }
    }

    Transform FindNearestEnemy()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, autoCombatSearchRadius);
        Transform nearest = null;
        float minDistSqr = Mathf.Infinity;
        Vector3 myPos = transform.position;

        foreach (var hit in hits)
        {
            if (hit.CompareTag("Enemy"))
            {
                float distSqr = (hit.transform.position - myPos).sqrMagnitude;
                if (distSqr < minDistSqr)
                {
                    minDistSqr = distSqr;
                    nearest = hit.transform;
                }
            }
        }
        return nearest;
    }

    bool IsTargetInAttackRange(Transform target)
    {
        float finalOffsetX = attackRectOffset.x * skeletonAnimation.Skeleton.ScaleX;
        Vector2 rectCenter = (Vector2)transform.position + new Vector2(finalOffsetX, attackRectOffset.y);
        Vector2 halfSize = new Vector2(attackRectWidth * 0.5f, attackRectHeight * 0.5f);
        Vector2 targetPos = target.position;

        return (targetPos.x >= rectCenter.x - halfSize.x && targetPos.x <= rectCenter.x + halfSize.x &&
                targetPos.y >= rectCenter.y - halfSize.y && targetPos.y <= rectCenter.y + halfSize.y);
    }

    void StartAutoRetreat()
    {
        if (currentAutoTarget == null) return;
        isAutoRetreating = true;
        pendingRetreat = false;
        retreatTimer = Random.Range(retreatMinTime, retreatMaxTime);
    }

    void StopAutoRetreat()
    {
        isAutoRetreating = false;
        pendingRetreat = false;
        retreatTimer = 0f;
    }

    // ==================== 角色行为 ====================
    void PerformJump()
    {
        rb.velocity = new Vector2(rb.velocity.x, jumpForce);
        isJumping = true;
        isAttacking = false;
        skeletonAnimation.AnimationState.SetAnimation(0, jumpAnim, false);
    }

    void PerformAttack()
    {
        isAttacking = true;
        attackStartTime = Time.time;
        canCancelAttack = false;
        float speedMultiplier = (myStats != null) ? myStats.attackSpeed.GetValue() : 1f;
        var track = skeletonAnimation.AnimationState.SetAnimation(0, attackAnim, false);
        track.TimeScale = speedMultiplier;
    }

    void InterruptAttack()
    {
        isAttacking = false;
        canCancelAttack = false;
    }

    // ==================== 动画与翻转 ====================
    void HandleMovementAnimation()
    {
        // ★ 关键修复：攻击时禁止切换移动/空闲动画，避免覆盖攻击动画 ★
        if (isAttacking) return;

        var currentTrack = skeletonAnimation.AnimationState.GetCurrent(0);
        string currentAnimName = (currentTrack != null) ? currentTrack.Animation.Name : "";

        if (isJumping)
        {
            if (currentAnimName == jumpAnim && currentTrack.IsComplete)
                isJumping = false;
            else if (currentAnimName == jumpAnim)
                return;
        }

        if (!isGrounded)
        {
            if (currentAnimName != jumpAnim && currentAnimName != onAirAnim)
                skeletonAnimation.AnimationState.SetAnimation(0, onAirAnim, true);
        }
        else
        {
            string targetAnim = (Mathf.Abs(horizontalInput) > 0.1f) ? runAnim : idleAnim;
            if (currentAnimName != targetAnim)
                skeletonAnimation.AnimationState.SetAnimation(0, targetAnim, true);
        }
    }

    void HandleFlip()
    {
        if (horizontalInput > 0)
            skeletonAnimation.Skeleton.ScaleX = 1;
        else if (horizontalInput < 0)
            skeletonAnimation.Skeleton.ScaleX = -1;

        FlipHitboxLocalX(playerHitbox);
    }

    void FlipHitboxLocalX(CombatContactSender hitbox)
    {
        if (hitbox != null)
        {
            Vector3 localPos = hitbox.transform.localPosition;
            localPos.x = Mathf.Abs(localPos.x) * skeletonAnimation.Skeleton.ScaleX;
            hitbox.transform.localPosition = localPos;
        }
    }

    void HandleSpineEvent(TrackEntry trackEntry, Spine.Event e)
    {
        if (e.Data.Name == "OnAttack")
            StartCoroutine(HitboxRoutine());
        else if (e.Data.Name == "CanCancel")
            canCancelAttack = true;
    }

    IEnumerator HitboxRoutine()
    {
        attackHitbox.enabled = true;
        yield return new WaitForSeconds(0.1f);
        attackHitbox.enabled = false;
    }

    void OnAnimationComplete(Spine.TrackEntry trackEntry)
    {
        if (trackEntry.Animation.Name == attackAnim)
        {
            isAttacking = false;
            canCancelAttack = false;
            trackEntry.TimeScale = 1f;

            if (autoCombatEnabled)
                pendingRetreat = true;
        }
    }

    public float GetAttackWindupProgress()
    {
        if (!isAttacking) return 0f;

        return Mathf.Clamp01((Time.time - attackStartTime) / attackWindupDuration);
    }

    void OnDrawGizmos()
    {
        if (groundCheck != null)
        {
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, checkRadius);
        }

        if (autoCombatEnabled)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, autoCombatSearchRadius);

            // 攻击矩形
            Gizmos.color = Color.red;
            Vector2 rectCenter;
            if (Application.isPlaying && skeletonAnimation != null)
                rectCenter = (Vector2)transform.position + new Vector2(attackRectOffset.x * skeletonAnimation.Skeleton.ScaleX, attackRectOffset.y);
            else
                rectCenter = (Vector2)transform.position + attackRectOffset;
            Gizmos.DrawWireCube(rectCenter, new Vector2(attackRectWidth, attackRectHeight));
        }
    }
}