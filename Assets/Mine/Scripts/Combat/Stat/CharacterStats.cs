using UnityEngine;
using System;

public class CharacterStats : MonoBehaviour, IDamageable
{
    [Header("基础生存属性")]
    public float maxHealth = 100;
    public float currentHealth { get; protected set; }
    public event Action<float, float> OnHealthChanged;

    [Header("战斗属性")]
    public Stat damage;           // 攻击力
    public Stat defense;          // 物理防御
    public Stat magicResistance;  // 魔法抗性

    [Header("隐藏属性：抗打断与韧性")]
    [Range(0, 5)]
    [Tooltip("抗打断等级 (0: 纸糊, 1: 普通, 5: 霸体)")]
    public int interruptResistance = 1;

    public bool hasToughnessBar = false; // 是否拥有韧性槽（击破系统）
    public float maxToughness = 100f;
    public float currentToughness { get; protected set; }
    public float toughnessRecoverRate = 10f; // 击破后每秒恢复多少
    public float brokenDamageMultiplier = 1.5f; // 击破状态易伤倍率

    public bool isBroken { get; protected set; } // 是否处于击破状态

    // 状态事件
    public event Action OnStagger; // 被打断小僵直
    public event Action OnBroken;  // 被击破大僵直
    public event Action OnRecover; // 从击破状态恢复
    public event Action<float, float> OnToughnessChanged; // 韧性变化事件
    public event Action OnDeath;                          // 死亡事件

    protected virtual void Awake()
    {
        currentHealth = maxHealth;
        currentToughness = maxToughness;
    }

    protected virtual void Start()
    {
        // 游戏开始时初始化一次UI
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    protected virtual void Update()
    {
        // 韧性槽恢复逻辑
        if (isBroken)
        {
            currentToughness += toughnessRecoverRate * Time.deltaTime;
            OnToughnessChanged?.Invoke(currentToughness, maxToughness);
            if (currentToughness >= maxToughness)
            {
                RecoverFromBroken();
            }
        }
    }

    // 【修改】接收完整的 AttackImpact 数据
    public virtual void TakeDamage(AttackImpact impact)
    {
        // 1. 伤害计算与减伤逻辑
        float finalDamage = impact.damage;

        // 如果处于击破状态，受到额外伤害
        if (isBroken) finalDamage *= brokenDamageMultiplier;

        finalDamage -= defense.GetValue();
        finalDamage = Mathf.Clamp(finalDamage, 0, int.MaxValue);

        currentHealth -= finalDamage;
        Debug.Log($"{transform.name} 受到了 {finalDamage} 点伤害. 剩余血量: {currentHealth}");

        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0)
        {
            Die();
            return;
        }

        // 2. 抗打断判定 (力度等级对比)
        // 注意：击破状态下，自身抗打断完全失效 (视为0)
        int effectiveResist = isBroken ? 0 : interruptResistance;
        if (impact.interruptPower >= effectiveResist)
        {
            OnStagger?.Invoke(); // 触发僵直打断
        }

        // 3. 削韧判定 (仅针对有韧性槽且未被击破的单位)
        if (hasToughnessBar && !isBroken)
        {
            currentToughness -= impact.poiseDamage;
            currentToughness = Mathf.Max(0, currentToughness); // 防止变成负数
            OnToughnessChanged?.Invoke(currentToughness, maxToughness);
            if (currentToughness <= 0)
            {
                TriggerBroken();
            }
        }
    }

    private void TriggerBroken()
    {
        isBroken = true;
        currentToughness = 0;
        Debug.Log($"{transform.name} 的韧性被击破！");
        OnBroken?.Invoke();
    }

    private void RecoverFromBroken()
    {
        isBroken = false;
        currentToughness = maxToughness;
        Debug.Log($"{transform.name} 从击破状态恢复。");
        OnRecover?.Invoke();
    }

    public void SetHealthToMax()
    {
        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public virtual void Die()
    {
        Debug.Log(transform.name + " 死亡了.");
        OnDeath?.Invoke();
        // Destroy(gameObject); // 暂时注释掉，避免测试时人直接没了
    }
}