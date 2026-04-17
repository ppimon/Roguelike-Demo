using UnityEngine;

public class BossStats : CharacterStats
{
    [Header("Boss 专属机制")]
    [Tooltip("韧性未击破时，受到的伤害倍率 (例如 0.2 表示只有20%伤害，即80%减伤)")]
    public string bossName = "末日兽";
    public float unbrokenDamageMultiplier = 0.2f;

    protected override void Awake()
    {
        base.Awake();
        hasToughnessBar = true; // Boss 强制拥有韧性槽
    }

    protected override void Start()
    {
        base.Start();

        // 【新增】游戏开始（或 Boss 被激活时），通知 UI 面板显示自己
        if (BossUIManager.Instance != null)
        {
            BossUIManager.Instance.ShowBossUI(this);
        }
    }

    // 重写受伤逻辑，加入 Boss 特有的减伤机制
    public override void TakeDamage(AttackImpact impact)
    {
        // 1. 复制一份 impact，准备修改最终伤害
        AttackImpact finalImpact = impact;

        // 2. Boss 核心减伤/易伤逻辑
        if (!isBroken)
        {
            // 韧性存在时：高额减伤
            finalImpact.damage *= unbrokenDamageMultiplier;
            // 提示：你可以用 Debug.Log 确认减伤是否生效
            // Debug.Log($"Boss处于高额减伤状态，实际受到伤害: {finalImpact.damage}");
        }
        else
        {
            // 击破时：调用基类的易伤逻辑 (你之前在 CharacterStats 里写了 brokenDamageMultiplier)
            // 这里为了保持逻辑一致，我们直接交给 base.TakeDamage 处理易伤
        }

        // 3. 将计算好减伤的攻击数据，传给基类执行真正的扣血和削韧
        base.TakeDamage(finalImpact);
    }

    public override void Die()
    {
        base.Die();
        Debug.Log("Boss 被击杀！");
        // TODO: 通知所有部位播放死亡动画，爆装备等
    }
}