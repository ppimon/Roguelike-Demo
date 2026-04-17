public interface IDamageable
{
    // 【修改】统一使用 AttackImpact 结构体传递完整的攻击数据
    void TakeDamage(AttackImpact impact);
}