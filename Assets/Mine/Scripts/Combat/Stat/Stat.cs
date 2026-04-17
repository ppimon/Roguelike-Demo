using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Stat
{
    [SerializeField]
    public float baseValue; // 基础数值 (在Inspector里填)

    // 修改器列表 (比如：+10攻击力的戒指，+50%攻击力的Buff)
    // 这里为了简化，暂时只处理 加法 修改器
    private List<float> modifiers = new List<float>();

    // 获取最终值
    public float GetValue()
    {
        float finalValue = baseValue;
        foreach (float modifier in modifiers)
        {
            finalValue += modifier;
        }
        return finalValue;
    }

    // 添加修改器 (例如穿装备)
    public void AddModifier(float modifier)
    {
        if (modifier != 0)
            modifiers.Add(modifier);
    }

    // 移除修改器 (例如脱装备)
    public void RemoveModifier(float modifier)
    {
        if (modifier != 0)
            modifiers.Remove(modifier);
    }
}