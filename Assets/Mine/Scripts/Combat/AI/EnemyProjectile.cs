using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyProjectile : MonoBehaviour
{
    [Header("飞行参数")]
    public float flySpeed = 12f;
    public float lifeTime = 3f;      // 飞行多久后自动销毁防内存泄漏
    public GameObject hitEffect;     // 击中特效 (可选)

    [Header("碰撞检测")]
    public LayerMask obstacleLayer;  // 撞墙销毁

    private Rigidbody2D rb;
    private int flyDirection;
    private EnemyStats ownerStats;   // 发射者的属性，用于读取攻击力

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    /// <summary>
    /// 初始化发射
    /// </summary>
    public void Initialize(int direction, EnemyStats shooterStats)
    {
        flyDirection = direction;
        ownerStats = shooterStats;

        // 根据飞行方向翻转图片
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * direction;
        transform.localScale = scale;

        // 设置初速度
        rb.velocity = new Vector2(flyDirection * flySpeed, 0f);

        // 启动寿命计时销毁
        Destroy(gameObject, lifeTime);
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        // 1. 打中玩家
        if (collision.CompareTag("Player"))
        {
            CharacterStats playerStats = collision.GetComponent<CharacterStats>();
            if (playerStats != null && ownerStats != null)
            {
                // 计算并造成伤害 (调用你的伤害系统)
                float finalDamage = ownerStats.damage.GetValue();
                AttackImpact impact = new AttackImpact();
                impact.damage = Mathf.RoundToInt(finalDamage);
                playerStats.TakeDamage(impact);
            }
            DestroyProjectile();
        }
        // 2. 撞到墙壁或地面
        else if ((obstacleLayer.value & (1 << collision.gameObject.layer)) > 0)
        {
            DestroyProjectile();
        }
    }

    void DestroyProjectile()
    {
        if (hitEffect != null)
        {
            Instantiate(hitEffect, transform.position, Quaternion.identity);
        }
        Destroy(gameObject);
    }
}