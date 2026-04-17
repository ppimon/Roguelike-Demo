using System.Collections;
using UnityEngine;
using Spine.Unity;
using Spine;

/// <summary>
/// Boss 爪子 AI 控制器：负责横扫连击、砸地余震、概率决策与动画队列管理。
/// </summary>
public class BossAI_Claw : MonoBehaviour
{
    public enum ClawSide { Left, Right }

    [Header("核心引用")]
    public ClawSide side;                   // 区分左手还是右手
    public BossAI_Brain brain;              // 指向 Boss 逻辑中枢
    public BossBodyPart myBodyPart;         // 指向爪子自身的代理器 (用于控制无敌)
    public SkeletonAnimation spineAnim;     // Spine 动画组件

    [Header("技能 1：横扫 (参数)")]
    public float skill1Cooldown = 6f;       // 技能1冷却时间
    public float skill1TickRate = 1.0f;     // 每隔几秒进行一次概率判定
    [Range(0, 1)] public float skill1Probability = 0.4f; // 触发概率
    public CombatContactSender swipeHitbox; // 横扫的伤害判定盒

    [Header("技能 2：猛砸与余震 (参数)")]
    public float skill2Cooldown = 8f;       // 技能2冷却时间
    public float skill2TickRate = 1.0f;     // 每隔几秒进行一次概率判定
    [Range(0, 1)] public float skill2Probability = 0.6f; // 触发概率
    public CombatContactSender smashHitbox; // 砸地的伤害判定盒
    public GameObject shockwavePrefab;      // 余震特效预制体
    public Transform shockwaveSpawnPoint;   // 余震生成点

    [Header("独立索敌范围 (黄框)")]
    public LayerMask playerLayer;           // 玩家图层
    public Transform rangeCenter;           // 索敌中心点
    public Vector2 rangeSize = new Vector2(6f, 4f); // 索敌框尺寸
    public Vector2 rangeOffset = Vector2.zero;      // 索敌框偏移量

    // 内部状态计时器
    private bool playerInSwipeRange = false;
    private float skill1TickTimer = 0f;
    private float lastSkill1Time = -10f;
    private float skill2TickTimer = 0f;
    private float lastSkill2Time = -10f;

    private bool isAttacking = false;
    private string trackCurrentAnim = "";   // 记录当前正在释放的技能标识

    void Start()
    {
        // 绑定 Spine 事件
        spineAnim.AnimationState.Event += HandleSpineEvent;
        spineAnim.AnimationState.Complete += HandleSpineComplete;

        // 订阅 Boss 大脑的击破事件
        BossStats stats = brain.GetComponent<BossStats>();
        if (stats != null) stats.OnBroken += ForceInterrupt;
    }

    void OnDestroy()
    {
        if (brain != null)
        {
            BossStats stats = brain.GetComponent<BossStats>();
            if (stats != null) stats.OnBroken -= ForceInterrupt;
        }
    }

    void Update()
    {
        BossStats stats = brain.GetComponent<BossStats>();
        if (stats != null && stats.isBroken) return; // 破防状态下停止一切行动
        if (isAttacking) return;

        // 1. 技能 1 索敌判定：玩家是否在横扫范围内
        if (rangeCenter != null)
        {
            Vector2 checkPos = (Vector2)rangeCenter.position + rangeOffset;
            Collider2D hit = Physics2D.OverlapBox(checkPos, rangeSize, 0f, playerLayer);
            playerInSwipeRange = (hit != null);
        }

        // 2. 并行处理两个技能的冷却与触发逻辑
        HandleSkill1Logic();
        HandleSkill2Logic();
    }

    /// <summary>
    /// 处理技能 1 (横扫) 的判定逻辑
    /// </summary>
    void HandleSkill1Logic()
    {
        if (Time.time < lastSkill1Time + skill1Cooldown) return;

        if (playerInSwipeRange)
        {
            skill1TickTimer += Time.deltaTime;
            if (skill1TickTimer >= skill1TickRate)
            {
                skill1TickTimer -= skill1TickRate; // 周期重置
                if (Random.value <= skill1Probability)
                {
                    ExecuteSkill1();
                }
            }
        }
        else
        {
            skill1TickTimer = 0f; // 玩家不在范围内，重置计时
        }
    }

    /// <summary>
    /// 处理技能 2 (砸地) 的判定逻辑
    /// </summary>
    void HandleSkill2Logic()
    {
        if (Time.time < lastSkill2Time + skill2Cooldown) return;

        // 仅当玩家在地面上时才考虑触发砸地
        if (brain.IsPlayerGrounded())
        {
            skill2TickTimer += Time.deltaTime;
            if (skill2TickTimer >= skill2TickRate)
            {
                skill2TickTimer -= skill2TickRate;
                if (Random.value <= skill2Probability)
                {
                    // 获取互斥锁：如果另一只爪子没在砸地，我才砸
                    if (!brain.isSmashingGround)
                    {
                        brain.isSmashingGround = true; // 上锁
                        ExecuteSkill2();
                    }
                }
            }
        }
        else
        {
            skill2TickTimer = 0f;
        }
    }

    /// <summary>
    /// 执行技能 1：进入无敌并播放 4 段 Spine 动画队列
    /// </summary>
    void ExecuteSkill1()
    {
        isAttacking = true;
        myBodyPart.isInvincible = true;
        trackCurrentAnim = "Skill_1";

        spineAnim.AnimationState.SetAnimation(0, "Skill_1_Begin_Start", false);
        spineAnim.AnimationState.AddAnimation(0, "Skill_1_Begin_End", false, 0f);
        spineAnim.AnimationState.AddAnimation(0, "Skill_1_Attack", false, 0f);
        spineAnim.AnimationState.AddAnimation(0, "Skill_1_End", false, 0f);
    }

    /// <summary>
    /// 执行技能 2：进入无敌并播放砸地动画
    /// </summary>
    void ExecuteSkill2()
    {
        isAttacking = true;
        myBodyPart.isInvincible = true;
        trackCurrentAnim = "Skill_2_Attack";

        spineAnim.AnimationState.SetAnimation(0, "Skill_2_Attack", false);
    }

    /// <summary>
    /// 强制打断当前行动 (用于破防)
    /// </summary>
    void ForceInterrupt()
    {
        isAttacking = false;
        myBodyPart.isInvincible = false; // 破防解除无敌
        swipeHitbox.StopDamageCalculation();
        smashHitbox.StopDamageCalculation();
        StopAllCoroutines();

        // 如果正准备砸地却被破防，必须释放互斥锁，防止全局卡死
        if (trackCurrentAnim == "Skill_2_Attack") brain.isSmashingGround = false;
    }

    // --- Spine 事件处理 ---

    void HandleSpineEvent(TrackEntry trackEntry, Spine.Event e)
    {
        if (e.Data.Name == "OnAttack")
        {
            // 处理横扫的伤害帧
            if (trackEntry.Animation.Name == "Skill_1_Attack")
            {
                swipeHitbox.StartDamageCalculation(1.0f);
                StartCoroutine(StopHitbox(swipeHitbox));
            }
            // 处理砸地的伤害帧 (同时产生判定和冲击波)
            else if (trackEntry.Animation.Name == "Skill_2_Attack")
            {
                smashHitbox.StartDamageCalculation(1.5f); // 砸地伤害倍率较高
                StartCoroutine(StopHitbox(smashHitbox));

                if (shockwavePrefab != null)
                {
                    BossStats stats = brain.GetComponent<BossStats>();
                    // 生成向右传播的余震
                    GameObject waveRight = Instantiate(shockwavePrefab, shockwaveSpawnPoint.position, Quaternion.identity);
                    waveRight.GetComponent<ShockwaveProjectile>().Initialize(1f, stats);

                    // 生成向左传播的余震
                    GameObject waveLeft = Instantiate(shockwavePrefab, shockwaveSpawnPoint.position, Quaternion.identity);
                    waveLeft.GetComponent<ShockwaveProjectile>().Initialize(-1f, stats);
                }
            }
        }
    }

    void HandleSpineComplete(TrackEntry trackEntry)
    {
        // 技能 1 完全结束
        if (trackEntry.Animation.Name == "Skill_1_End")
        {
            EndAttack();
            lastSkill1Time = Time.time;
        }
        // 技能 2 完全结束
        else if (trackEntry.Animation.Name == "Skill_2_Attack")
        {
            EndAttack();
            lastSkill2Time = Time.time;
            brain.isSmashingGround = false; // 动作完成，释放互斥锁
        }
    }

    /// <summary>
    /// 收尾方法：解除无敌，恢复待机
    /// </summary>
    void EndAttack()
    {
        isAttacking = false;
        myBodyPart.isInvincible = false;
        spineAnim.AnimationState.SetAnimation(0, "Idle", true);
    }

    IEnumerator StopHitbox(CombatContactSender hitbox)
    {
        yield return new WaitForSeconds(0.15f);
        if (hitbox != null) hitbox.StopDamageCalculation();
    }

    // --- 编辑器可视化 ---

    void OnDrawGizmosSelected()
    {
        if (rangeCenter != null)
        {
            Gizmos.color = Color.yellow; // 爪子索敌框使用黄色
            Vector2 checkPos = (Vector2)rangeCenter.position + rangeOffset;
            Gizmos.DrawWireCube(checkPos, rangeSize); // 【修复】使用带偏移的坐标
        }
    }
}