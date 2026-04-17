using UnityEngine;
using System;

/// <summary>
/// 通用战斗判定发送器：挂载于判定盒(Hitbox)之上，负责将伤害打包发送给目标，并向上广播命中事件。
/// </summary>
public class CombatContactSender : MonoBehaviour
{
    [Header("战斗配置")]
    public CharacterStats ownerStats;    // 伤害来源者的属性 (用于读取基础攻击力)
    public Collider2D hitboxCollider;    // 物理判定框

    [Header("本招式属性")]
    public AttackImpact baseImpact;      // 面板配置的固定属性 (力度、削韧等)

    [Header("伤害倍率配置")]
    [Tooltip("此招式的固定伤害倍率。例如设为0.5，则造成 50% 的基础攻击力伤害")]
    public float skillDamageMultiplier = 1.0f;

    private float dynamicMultiplier = 1.0f; // 代码运行时动态传入的额外倍率

    /// <summary>
    /// 当成功命中目标并造成伤害时触发的事件。可供飞行物判定销毁、或玩家吸血使用。
    /// </summary>
    public event Action<IDamageable> OnHitSuccess;

    private void Awake()
    {
        if (hitboxCollider != null) hitboxCollider.enabled = false;
    }

    /// <summary>
    /// 激活判定盒，允许造成伤害
    /// </summary>
    /// <param name="dynMultiplier">程序动态赋予的额外伤害倍率 (默认 1.0)</param>
    public void StartDamageCalculation(float dynMultiplier = 1.0f)
    {
        dynamicMultiplier = dynMultiplier;
        if (hitboxCollider != null) hitboxCollider.enabled = true;
    }

    /// <summary>
    /// 关闭判定盒
    /// </summary>
    public void StopDamageCalculation()
    {
        if (hitboxCollider != null) hitboxCollider.enabled = false;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        IDamageable target = collision.GetComponent<IDamageable>();
        if (target != null)
        {
            // 防御性检查：不打自己，不触发同阵营伤害
            if (collision.gameObject == ownerStats.gameObject) return;
            if (gameObject.CompareTag("PlayerHitbox") && collision.CompareTag("Player")) return;
            if (gameObject.CompareTag("EnemyHitbox") && collision.CompareTag("Enemy")) return;

            // 获取发起者的基础攻击力
            float baseAtk = (ownerStats != null) ? ownerStats.damage.GetValue() : 0;

            // 构建最终的伤害数据包 = 基础攻击力 * 面板技能倍率 * 动态倍率
            AttackImpact finalImpact = baseImpact;
            finalImpact.damage = baseAtk * skillDamageMultiplier * dynamicMultiplier;

            // 执行伤害传递
            target.TakeDamage(finalImpact);

            // 成功造成伤害后，向所有订阅了此事件的脚本广播
            OnHitSuccess?.Invoke(target);
        }
    }
}