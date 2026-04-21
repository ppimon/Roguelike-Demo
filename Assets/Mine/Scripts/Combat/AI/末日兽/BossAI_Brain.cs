using UnityEngine;

/// <summary>
/// Boss 共享逻辑中枢：管理跨肢体的互斥锁状态，以及提供全局环境检测（如玩家是否落地）。
/// </summary>
public class BossAI_Brain : MonoBehaviour
{
    [Header("玩家引用与检测")]
    public Transform player;            // 玩家 Transform
    public LayerMask groundLayer;       // 地面图层
    public Transform playerGroundCheck; // 挂在玩家脚底的空物体，用于检测落地
    public float groundCheckRadius = 0.2f;

    [Header("Boss 共享状态 (互斥锁)")]
    [Tooltip("防止左右爪子同时使用砸地技能")]
    public bool isSmashingGround = false;

    private void Start()
    {
        // 1. 自动查找玩家对象 (假设玩家 Tag 为 "Player")
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                player = playerObj.transform;
            }
        }

        // 2. 自动查找玩家脚底的检测点
        // 方案 A：如果检测点是玩家的子物体且命名固定（例如 "GroundCheck"）
        if (player != null && playerGroundCheck == null)
        {
            playerGroundCheck = player.Find("GroundCheck");
        }
    }

    /// <summary>
    /// 全局检测玩家当前是否站在地面上
    /// </summary>
    public bool IsPlayerGrounded()
    {
        if (playerGroundCheck == null) return false;
        // 在玩家脚底画一个圆，如果碰到了 Ground 图层，说明落地了
        Collider2D hit = Physics2D.OverlapCircle(playerGroundCheck.position, groundCheckRadius, groundLayer);
        return hit != null;
    }
}