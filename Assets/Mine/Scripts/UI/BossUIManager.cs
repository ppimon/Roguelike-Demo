using UnityEngine;
using UnityEngine.UI;
using TMPro; // 强烈建议使用 TextMeshPro 渲染文本

public class BossUIManager : MonoBehaviour
{
    // 单例模式，方便全局调用
    public static BossUIManager Instance { get; private set; }

    [Header("UI 组件引用")]
    public GameObject bossUIPanel;        // 整个BossUI的父节点（用于控制显示/隐藏）
    public TextMeshProUGUI bossNameText;  // Boss名称
    public Slider healthSlider;           // 血条
    public Slider toughnessSlider;        // 韧性条
    public Image toughnessFillImage;      // 韧性条的填充图片（用于改变颜色）

    [Header("UI 颜色配置")]
    public Color normalToughnessColor = Color.yellow;
    public Color brokenToughnessColor = Color.gray;

    private BossStats currentBoss; // 记录当前正在追踪的 Boss

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // 初始状态隐藏 UI
        bossUIPanel.SetActive(false);
    }

    // 由 Boss 身上发起的调用
    public void ShowBossUI(BossStats boss)
    {
        // 1. 如果已经有旧Boss，先取消订阅旧事件，防止内存泄漏
        if (currentBoss != null)
        {
            UnsubscribeEvents(currentBoss);
        }

        currentBoss = boss;

        // 2. 初始化 UI 数据
        bossNameText.text = boss.bossName;
        UpdateHealth(boss.currentHealth, boss.maxHealth);
        UpdateToughness(boss.currentToughness, boss.maxToughness);
        toughnessFillImage.color = normalToughnessColor;

        // 3. 订阅当前 Boss 的所有数值变化事件
        currentBoss.OnHealthChanged += UpdateHealth;
        currentBoss.OnToughnessChanged += UpdateToughness;
        currentBoss.OnBroken += HandleBroken;
        currentBoss.OnRecover += HandleRecover;
        currentBoss.OnDeath += HideBossUI;

        // 4. 显示面板
        bossUIPanel.SetActive(true);
    }

    public void HideBossUI()
    {
        bossUIPanel.SetActive(false);
        if (currentBoss != null)
        {
            UnsubscribeEvents(currentBoss);
            currentBoss = null;
        }
    }

    private void UnsubscribeEvents(BossStats boss)
    {
        boss.OnHealthChanged -= UpdateHealth;
        boss.OnToughnessChanged -= UpdateToughness;
        boss.OnBroken -= HandleBroken;
        boss.OnRecover -= HandleRecover;
        boss.OnDeath -= HideBossUI;
    }

    // --- UI 刷新回调逻辑 ---

    private void UpdateHealth(float current, float max)
    {
        healthSlider.maxValue = max;
        healthSlider.value = current;
    }

    private void UpdateToughness(float current, float max)
    {
        toughnessSlider.maxValue = max;
        toughnessSlider.value = current;
    }

    private void HandleBroken()
    {
        // 韧性条变灰，表示正在破防恢复中
        toughnessFillImage.color = brokenToughnessColor;
        UpdateToughness(0, currentBoss.maxToughness);
    }

    private void HandleRecover()
    {
        // 韧性条恢复为黄色
        toughnessFillImage.color = normalToughnessColor;
        UpdateToughness(currentBoss.maxToughness, currentBoss.maxToughness);
    }
}