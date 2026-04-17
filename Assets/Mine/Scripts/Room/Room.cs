using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class Room : MonoBehaviour
{
    [Header("出入口配置")]
    public RoomExit[] exits; // 在Inspector中配置每个口的朝向和墙体

    public enum Direction
    {
        Up,
        Down,
        Left,
        Right
    }

    // 定义一个简单的类来管理单个出入口的所有信息
    [System.Serializable]
    public class RoomExit
    {
        public Transform point;       // 坐标锚点
        public Direction direction;   // 出口朝向
        public GameObject wallObject; // 对应的封堵墙体
        public GameObject doorObject;
        [HideInInspector] public bool isOccupied = false; // 是否已连接
    }

    /// <summary>
    /// 获取所有未连接的出入口
    /// </summary>
    public List<RoomExit> GetFreeExits()
    {
        return exits.Where(e => !e.isOccupied).ToList();
    }

    /// <summary>
    /// 获取指定方向的空闲出入口（用于匹配）
    /// </summary>
    public RoomExit GetFreeExitWithDirection(Direction dir)
    {
        // 找到第一个符合方向且未被占用的接口
        return exits.FirstOrDefault(e => !e.isOccupied && e.direction == dir);
    }

    /// <summary>
    /// 封堵未连接的接口
    /// </summary>
    public void CloseUnconnectedExits()
    {
        foreach (var exit in exits)
        {
            if (!exit.isOccupied && exit.wallObject != null)
            {
                exit.wallObject.SetActive(true);
            }
        }
    }
}