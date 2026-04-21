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

    [Header("攻击碰撞箱")]
    public CombatContactSender playerHitbox;

    // --- 状态标志位 ---
    private bool isGrounded;
    private bool isAttacking;
    private bool isJumping; // 新增：专门记录是否处于起跳动作中
    private float horizontalInput;
    private bool canCancelAttack = false; // 是否进入了可打断区间

    void Start()
    {
        skeletonAnimation.AnimationState.Complete += OnAnimationComplete;
        skeletonAnimation.AnimationState.Event += HandleSpineEvent;
    }

    void Update()
    {
        horizontalInput = Input.GetAxisRaw("Horizontal");

        // 1. 地面检测
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, checkRadius, groundLayer);

        // 如果落地了，且垂直速度向下或为0，说明跳跃动作彻底结束
        if (isGrounded && rb.velocity.y <= 0.1f)
        {
            isJumping = false;
        }

        // 如果正在攻击且处于“可取消”阶段，且玩家有了移动或跳跃输入
        if (isAttacking && canCancelAttack)
        {
            if (Input.GetButtonDown("Jump") || Mathf.Abs(horizontalInput) > 0.1f)
            {
                InterruptAttack(); // 强行打断
            }
        }

        // 只有在没攻击时，或者攻击已经进入可取消阶段时，才处理新输入
        if (!isAttacking || canCancelAttack)
        {
            if (Input.GetButtonDown("Jump") && isGrounded)
            {
                PerformJump();
            }
            else if (Input.GetMouseButtonDown(0) && !isAttacking)
            {
                PerformAttack();
            }
        }

        // 4. 动画状态管理
        // 只有在没攻击时，才由移动逻辑接管动画
        if (!isAttacking)
        {
            HandleMovementAnimation();
        }

        HandleFlip();
    }

    void FixedUpdate()
    {
        float currentSpeed = isAttacking ? moveSpeed * 0f : moveSpeed;
        rb.velocity = new Vector2(horizontalInput * currentSpeed, rb.velocity.y);
    }

    void PerformJump()
    {
        // 物理跳跃
        rb.velocity = new Vector2(rb.velocity.x, jumpForce);

        // --- 逻辑修正核心 ---
        isJumping = true;      // 上锁：标记正在起跳
        isAttacking = false;   // 强制打断攻击状态：如果在攻击时跳跃，立刻停止攻击逻辑

        // 播放动画
        skeletonAnimation.AnimationState.SetAnimation(0, jumpAnim, false);
    }

    void PerformAttack()
    {
        isAttacking = true;
        canCancelAttack = false; // 攻击开始时，锁定打断

        float speedMultiplier = (myStats != null) ? myStats.attackSpeed.GetValue() : 1f;
        var track = skeletonAnimation.AnimationState.SetAnimation(0, attackAnim, false);
        track.TimeScale = speedMultiplier;
    }

    // 打断攻击的方法
    void InterruptAttack()
    {
        isAttacking = false;
        canCancelAttack = false;
        // 注意：这里不需要手动 ClearTrack，后续的 PerformJump 或 HandleMovementAnimation 
        // 会通过 SetAnimation 自动覆盖当前动画
    }

    void HandleMovementAnimation()
    {
        var currentTrack = skeletonAnimation.AnimationState.GetCurrent(0);
        string currentAnimName = (currentTrack != null) ? currentTrack.Animation.Name : "";

        // --- 优先级 1: 起跳保护 ---
        // 如果正在播放 Jump 动作，且动作没播完，强制保持 Jump
        // 解决了“起跳瞬间被切回Run”的问题
        if (isJumping)
        {
            // 如果动画播完了，接触锁定（允许切入 OnAir）
            if (currentAnimName == jumpAnim && currentTrack.IsComplete)
            {
                isJumping = false;
            }
            else if (currentAnimName == jumpAnim)
            {
                return; // 直接返回，不执行下面的地面/空中判断
            }
        }

        // --- 优先级 2: 空中/地面逻辑 ---
        if (!isGrounded)
        {
            // 在空中，且不是在播 Jump，那就播 OnAir
            if (currentAnimName != jumpAnim && currentAnimName != onAirAnim)
            {
                skeletonAnimation.AnimationState.SetAnimation(0, onAirAnim, true);
            }
        }
        else
        {
            // 在地面
            string targetAnim = (Mathf.Abs(horizontalInput) > 0.1f) ? runAnim : idleAnim;
            if (currentAnimName != targetAnim)
            {
                skeletonAnimation.AnimationState.SetAnimation(0, targetAnim, true);
            }
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

    // 处理 Spine 事件的方法
    void HandleSpineEvent(TrackEntry trackEntry, Spine.Event e)
    {
        // 这里的 "OnAttack" 必须与你在 Spine 编辑器里命名的事件名【一模一样】
        if (e.Data.Name == "OnAttack")
        {
            // 触发事件时，启动一个协程来开启判定盒
            StartCoroutine(HitboxRoutine());
        }
        else if (e.Data.Name == "CanCancel")
        {
            canCancelAttack = true;
        }
    }

    // 控制判定盒开启与关闭的协程
    IEnumerator HitboxRoutine()
    {
        // 1. 开启碰撞盒，瞬间产生伤害判定
        attackHitbox.enabled = true;

        // 2. 保持开启极短的时间 (通常 0.1 到 0.15 秒就足够了，根据你动画挥砍的速度定)
        yield return new WaitForSeconds(0.1f);

        // 3. 关闭碰撞盒，防止把剑收回来时还能碰到敌人
        attackHitbox.enabled = false;
    }

    void OnAnimationComplete(Spine.TrackEntry trackEntry)
    {
        // 只有自然播放结束才会触发
        if (trackEntry.Animation.Name == attackAnim)
        {
            isAttacking = false;
            canCancelAttack = false; // 彻底播完，重置状态
            // 恢复动画速度，防止攻速影响了走路
            trackEntry.TimeScale = 1f;
        }
    }

    void OnDrawGizmos()
    {
        if (groundCheck != null)
        {
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, checkRadius);
        }
    }
}