using UnityEngine;

public enum EnemyRank { Normal, Elite, Boss }
public enum EnemyFaction { Antimatter, Belobog, Xianzhou }

[CreateAssetMenu(fileName = "NewEnemyData", menuName = "Game/Enemy Data")]
public class EnemyData : ScriptableObject
{
    public string enemyName;
    public GameObject prefab;

    [Header("生成分类")]
    public EnemyRank rank;
    public EnemyFaction faction;

    [Header("难度与空间占用")]
    public int difficultyCost = 1;

    [Tooltip("无需手动填写！拖入 Prefab 后会自动读取其身上的 BoxCollider2D 大小")]
    public Vector2 spawnSize;

    // 【新增魔法方法】当你在面板修改数据或拖入 Prefab 时自动执行
    private void OnValidate()
    {
        if (prefab != null)
        {
            // 尝试获取 Prefab 身上的 BoxCollider2D
            BoxCollider2D col = prefab.GetComponent<BoxCollider2D>();
            if (col != null)
            {
                // 自动计算实际的世界坐标大小 (size * scale)
                spawnSize = new Vector2(
                    col.size.x * Mathf.Abs(prefab.transform.localScale.x),
                    col.size.y * Mathf.Abs(prefab.transform.localScale.y)
                );
            }
        }
    }
}