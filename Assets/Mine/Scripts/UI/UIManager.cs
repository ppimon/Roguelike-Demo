using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("玩家UI")]
    public PlayerStats playerStats; // 拖入玩家
    public Image healthFill;        // 拖入红色的 HealthFill 图像
    public Image energyFill;        // 拖入蓝色的 EnergyFill 图像

    [Header("暂停菜单")]
    public GameObject pauseMenuPanel; // 拖入 PauseMenu 面板
    private bool isPaused = false;

    void Start()
    {
        // 1. 订阅玩家的属性变化事件
        if (playerStats != null)
        {
            playerStats.OnHealthChanged += UpdateHealthUI;
            playerStats.OnEnergyChanged += UpdateEnergyUI;
        }

        // 确保一开始游戏没暂停，菜单是关的
        pauseMenuPanel.SetActive(false);
        Time.timeScale = 1f;
    }

    void Update()
    {
        // 按下 Esc 键切换暂停状态
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused) ResumeGame();
            else PauseGame();
        }
    }

    // 更新血条UI的回调函数
    void UpdateHealthUI(float current, float max)
    {
        healthFill.fillAmount = current / max;
    }

    // 更新能量条UI的回调函数
    void UpdateEnergyUI(float current, float max)
    {
        energyFill.fillAmount = current / max;
    }

    // --- 按钮功能 ---
    public void PauseGame()
    {
        isPaused = true;
        pauseMenuPanel.SetActive(true);
        Time.timeScale = 0f; // 冻结游戏时间
    }

    public void ResumeGame()
    {
        isPaused = false;
        pauseMenuPanel.SetActive(false);
        Time.timeScale = 1f; // 恢复游戏时间
    }

    public void QuitGame()
    {
        Debug.Log("退出游戏");
        Application.Quit(); // 打包后有效
    }

    void OnDestroy()
    {
        // 养成好习惯，销毁时取消订阅，防止内存泄漏
        if (playerStats != null)
        {
            playerStats.OnHealthChanged -= UpdateHealthUI;
            playerStats.OnEnergyChanged -= UpdateEnergyUI;
        }
    }
}