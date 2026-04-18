using UnityEngine;

public class EnemyStats : CharacterStats
{
    protected override void Awake()
    {
        base.Awake();
        // 可以在这里初始化怪物的特定血量、防御等
    }

    public override void Die()
    {
        base.Die();
        // 敌人死亡逻辑：播放动画、掉落物品、销毁物体
        // 停止所有 AI 行为
        if (TryGetComponent<EnemyAI_NormalMelee>(out var ai))
        {
            ai.TriggerDeath();   // ← 新增一个方法，让 AI 播放死亡动画
        }
    }
}