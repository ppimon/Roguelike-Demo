using UnityEngine;
using UnityEngine.UI;

public class EnemyHealthBar : MonoBehaviour
{
    public CharacterStats enemyStats;
    public Image healthFill;
    private Canvas myCanvas; // 【新增】获取 Canvas 组件

    void Start()
    {
        myCanvas = GetComponent<Canvas>(); // 获取血条的 Canvas
        if (enemyStats != null)
        {
            enemyStats.OnHealthChanged += UpdateHealthBar;
        }

        // 可选：你希望一开始满血也显示血条，就把下面这行注释掉
        // if (myCanvas != null) myCanvas.enabled = false; 
    }

    void UpdateHealthBar(float current, float max)
    {
        healthFill.fillAmount = current / max;

        // 【修复】只开关 Canvas 渲染组件，不关闭 GameObject，保证脚本继续运行
        if (myCanvas != null)
        {
            myCanvas.enabled = (current < max); // 不满血时显示，满血时隐藏
        }
    }

    void OnDestroy()
    {
        if (enemyStats != null) enemyStats.OnHealthChanged -= UpdateHealthBar;
    }

    void LateUpdate()
    {
        transform.rotation = Quaternion.identity;
    }
}