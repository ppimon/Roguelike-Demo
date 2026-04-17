using System.Collections;
using UnityEngine;

/// <summary>
/// 通用场景破坏物脚本（木箱、陶罐等）：
/// 实现 IDamageable 接口，可承受伤害、闪烁受击、物理震动并掉落物品。
/// </summary>
public class DestructibleProp : MonoBehaviour, IDamageable
{
    [Header("生存属性")]
    public float maxHealth = 30f;
    private float currentHealth;

    [Header("组件引用")]
    public Rigidbody2D rb;
    public SpriteRenderer spriteRenderer;

    [Header("受击表现")]
    public Color hitFlashColor = Color.red;   // 受击闪烁的颜色
    public float flashDuration = 0.1f;        // 闪烁持续时间
    public float hitJitterForce = 2f;         // 受击时的物理微小抖动力度

    [Header("掉落配置 (待实装)")]
    [Range(0f, 1f)]
    public float dropProbability = 0.5f;      // 掉落概率 (0-1)
    // public GameObject[] dropItems;         // 预留：未来实装的掉落物预制体数组

    private Color originalColor;              // 记录原本的颜色

    void Start()
    {
        currentHealth = maxHealth;

        // 自动获取组件
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }
    }

    /// <summary>
    /// 接口实现：受到攻击时调用
    /// </summary>
    public void TakeDamage(AttackImpact impact)
    {
        // 1. 扣除血量
        currentHealth -= impact.damage;

        // 2. 物理与视觉表现
        PlayHitEffect();

        // 3. 死亡判定
        if (currentHealth <= 0)
        {
            BreakAndDestroy();
        }
    }

    /// <summary>
    /// 播放受击特效（闪色 + 物理微颤）
    /// </summary>
    private void PlayHitEffect()
    {
        // 物理震动：给刚体一个微小的随机方向冲力，模拟被打得晃动
        if (rb != null)
        {
            Vector2 randomDir = new Vector2(Random.Range(-1f, 1f), Random.Range(0.2f, 1f)).normalized;
            rb.AddForce(randomDir * hitJitterForce, ForceMode2D.Impulse);
        }

        // 视觉闪白/闪红
        if (spriteRenderer != null)
        {
            StopAllCoroutines();
            StartCoroutine(FlashRoutine());
        }
    }

    private IEnumerator FlashRoutine()
    {
        spriteRenderer.color = hitFlashColor;
        yield return new WaitForSeconds(flashDuration);
        spriteRenderer.color = originalColor;
    }

    /// <summary>
    /// 摧毁自身并掉落物品
    /// </summary>
    private void BreakAndDestroy()
    {
        // 1. 触发掉落逻辑
        if (Random.value <= dropProbability)
        {
            Debug.Log($"<color=orange>【木箱】 {gameObject.name} 碎裂，掉落了物品！(系统待实装)</color>");
            // 预留代码：
            // int randomIndex = Random.Range(0, dropItems.Length);
            // Instantiate(dropItems[randomIndex], transform.position, Quaternion.identity);
        }

        // TODO: 未来可以在这里 Instantiate 播放一个“木屑四溅”的粒子特效和音效

        // 2. 销毁自身
        Destroy(gameObject);
    }
}