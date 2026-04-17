using System;
using UnityEngine;

public class PlayerStats : CharacterStats
{
    [Header("玩家特有属性")]
    public float maxEnergy = 100;
    public float currentEnergy { get; private set; }
    public float energyRegenRate = 5f; // 每秒回蓝量
    public event Action<float, float> OnEnergyChanged;

    public Stat attackSpeed; // 攻击速度 (1 为标准速度)

    protected override void Awake()
    {
        base.Awake(); // 执行父类的初始化 (设置满血)
        currentEnergy = maxEnergy;
    }

    void Update()
    {
        // 自动恢复能量
        if (currentEnergy < maxEnergy)
        {
            currentEnergy += Time.deltaTime * energyRegenRate;
            if (currentEnergy > maxEnergy) currentEnergy = maxEnergy;
        }

        OnEnergyChanged?.Invoke(currentEnergy, maxEnergy);
    }

    // 消耗能量的方法 (返回true表示消耗成功，false表示蓝不够)
    public bool UseEnergy(float amount)
    {
        if (currentEnergy >= amount)
        {
            currentEnergy -= amount;
            OnEnergyChanged?.Invoke(currentEnergy, maxEnergy); 
            return true;
        }
        else
        {
            Debug.Log("能量不足!");
            return false;
        }
    }
}