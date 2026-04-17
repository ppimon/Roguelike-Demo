using System.Collections;
using UnityEngine;
using Spine.Unity;
using Spine;

/// <summary>
/// Boss 头部 AI 控制器：负责头部的独立索敌、啃咬攻击机制，并监听 Boss 主体的击破状态。
/// </summary>
public class BossAI_Head : MonoBehaviour
{
    [Header("核心引用")]
    public BossStats bossBrain;           // 【修复】加入 Boss 大脑的引用，解决报错
    public SkeletonAnimation spineAnim;   // Spine 动画组件
    public CombatContactSender biteHitbox;// 啃咬伤害判定发送器

    [Header("技能参数：啃咬")]
    public float biteCooldown = 5f;       // 技能冷却时间
    public float timeToTriggerBite = 1.5f;// 玩家停留多久后触发啃咬
    public string biteAnimName = "Bite";  // 啃咬动画名称

    [Header("独立索敌范围 (绿框)")]
    public LayerMask playerLayer;         // 玩家图层
    public Transform rangeCenter;         // 索敌中心点
    public Vector2 rangeSize = new Vector2(4f, 4f); // 索敌框尺寸
    public Vector2 rangeOffset = Vector2.zero;      // 索敌框偏移量

    // 状态记录
    private bool playerInRange = false;   // 玩家是否在啃咬范围内
    private float timeInRange = 0f;       // 玩家在范围内的累计时间
    private float lastBiteTime = -10f;    // 上次啃咬发生的时间
    private bool isAttacking = false;     // 是否正在执行攻击动作

    void Start()
    {
        // 绑定 Spine 事件
        spineAnim.AnimationState.Event += HandleSpineEvent;
        spineAnim.AnimationState.Complete += HandleSpineComplete;

        // 初始状态进入待机
        spineAnim.AnimationState.SetAnimation(0, "Idle", true);

        // 订阅大脑的破防事件：破防时强制打断当前行动
        if (bossBrain != null) bossBrain.OnBroken += ForceInterrupt;
    }

    void OnDestroy()
    {
        // 销毁时安全取消订阅，防止内存泄漏
        if (bossBrain != null) bossBrain.OnBroken -= ForceInterrupt;
    }

    void Update()
    {
        // 如果 Boss 被击破，或者头部正在攻击，停止思考
        if (bossBrain != null && bossBrain.isBroken) return;
        if (isAttacking) return;

        // 1. 索敌判定：使用带偏移量的 OverlapBox 检测玩家
        if (rangeCenter != null)
        {
            Vector2 checkPos = (Vector2)rangeCenter.position + rangeOffset;
            Collider2D hit = Physics2D.OverlapBox(checkPos, rangeSize, 0f, playerLayer);
            playerInRange = (hit != null);
        }

        // 2. 冷却判断
        if (Time.time < lastBiteTime + biteCooldown) return;

        // 3. 触发机制：玩家在范围内持续停留
        if (playerInRange)
        {
            timeInRange += Time.deltaTime;
            if (timeInRange >= timeToTriggerBite)
            {
                TriggerBite();
            }
        }
        else
        {
            timeInRange = 0f; // 玩家离开，时间重置
        }
    }

    /// <summary>
    /// 触发啃咬技能
    /// </summary>
    void TriggerBite()
    {
        isAttacking = true;
        timeInRange = 0f;
        spineAnim.AnimationState.SetAnimation(0, biteAnimName, false);
    }

    /// <summary>
    /// 强制打断当前行为 (通常在 Boss 韧性被击破时调用)
    /// </summary>
    void ForceInterrupt()
    {
        isAttacking = false;
        timeInRange = 0f;
        biteHitbox.StopDamageCalculation(); // 强行关闭伤害判定
        StopAllCoroutines();                // 停止可能正在运行的延迟关闭协程
    }

    // --- Spine 回调区域 ---

    void HandleSpineEvent(TrackEntry trackEntry, Spine.Event e)
    {
        // 动画播放到伤害帧时，开启判定盒
        if (e.Data.Name == "OnAttack" && trackEntry.Animation.Name == biteAnimName)
        {
            biteHitbox.StartDamageCalculation(1.0f);
            StartCoroutine(StopHitbox(biteHitbox));
        }
    }

    void HandleSpineComplete(TrackEntry trackEntry)
    {
        // 攻击动画结束，恢复待机，并开始计算冷却
        if (trackEntry.Animation.Name == biteAnimName)
        {
            spineAnim.AnimationState.SetAnimation(0, "Idle", true);
            isAttacking = false;
            lastBiteTime = Time.time;
        }
    }

    /// <summary>
    /// 延迟关闭伤害判定盒
    /// </summary>
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
            Gizmos.color = Color.yellow; // 头部索敌框使用绿色
            Vector2 checkPos = (Vector2)rangeCenter.position + rangeOffset;
            Gizmos.DrawWireCube(checkPos, rangeSize); // 【修复】使用带偏移的坐标绘制
        }
    }
}