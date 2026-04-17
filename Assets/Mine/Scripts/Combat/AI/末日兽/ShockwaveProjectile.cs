using UnityEngine;

/// <summary>
/// 通用飞行物/冲击波控制器：支持向特定方向平移，并利用事件监听实现“命中即销毁”功能。
/// </summary>
public class ShockwaveProjectile : MonoBehaviour
{
    [Header("飞行物配置")]
    public float moveSpeed = 4f;         // 飞行速度
    public float lifeTime = 3f;          // 存活时间，超时自动销毁
    [Tooltip("如果勾选，则在命中第一个可破坏目标后立即自我销毁")]
    public bool destroyOnHit = true;

    [Header("组件引用")]
    public CombatContactSender contactSender; // 自身携带的伤害判定器

    private float direction = 1f;        // 移动方向 (1为右，-1为左)

    /// <summary>
    /// 初始化飞行物状态 (由发射者调用)
    /// </summary>
    public void Initialize(float dir, BossStats bossStats)
    {
        direction = dir;

        // 翻转贴图以匹配移动方向
        transform.localScale = new Vector3(dir, 1, 1);

        // 初始化伤害发送器，防止飞行物误伤 Boss 自己
        if (contactSender != null)
        {
            contactSender.ownerStats = bossStats;
            contactSender.StartDamageCalculation(0.8f); // 预设余震造成的伤害为原基础的 0.8 倍

            // 订阅命中事件，以便处理销毁逻辑
            contactSender.OnHitSuccess += HandleHit;
        }

        // 设置定时自毁
        Destroy(gameObject, lifeTime);
    }

    /// <summary>
    /// 处理成功命中后的逻辑
    /// </summary>
    private void HandleHit(IDamageable target)
    {
        if (destroyOnHit)
        {
            // TODO: 未来可在此处实例化命中爆炸特效
            Destroy(gameObject);
        }
        // 若 destroyOnHit 为 false，则飞行物会穿透目标继续前进
    }

    private void OnDestroy()
    {
        // 销毁时安全取消订阅，防止内存泄漏
        if (contactSender != null)
        {
            contactSender.OnHitSuccess -= HandleHit;
        }
    }

    void Update()
    {
        // 每帧沿 X 轴方向移动
        transform.Translate(Vector2.right * direction * moveSpeed * Time.deltaTime);
    }
}