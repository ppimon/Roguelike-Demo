using UnityEngine;

[System.Serializable]
public struct AttackImpact
{
    [Range(0, 5)]
    public int interruptPower;   // 攻击的“力度”等级 (0-5)
    public float poiseDamage;    // 攻击的“削韧”数值 (对应韧性槽)
    public float damage;         // 基础伤害
}