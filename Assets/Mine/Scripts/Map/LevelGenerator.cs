using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Room;

public class LevelGenerator : MonoBehaviour
{
    [Header("资源池")]
    public Room startRoomPrefab;
    public Room[] corridorPrefabs;
    public Room[] roomPrefabs;

    [Header("参数")]
    public int maxRooms = 15;
    public int minCorridorLength = 2;
    public int maxCorridorLength = 5;

    // --- 核心改动：不再依赖LayerMask，改用手动列表记录 ---
    // 使用 Rect (矩形) 来存储已占用区域，比 Physics2D 更快更准
    private List<Rect> occupiedRects = new List<Rect>();
    private List<Room> allSpawnedObjects = new List<Room>();
    private int functionalRoomCount = 0;

    void Start()
    {
        GenerateLevel();
    }

    void GenerateLevel()
    {
        // 1. 生成起点
        Room startRoom = Instantiate(startRoomPrefab, Vector3.zero, Quaternion.identity);

        // 注册起点的占用区域
        RegisterRoom(startRoom);

        allSpawnedObjects.Add(startRoom);
        functionalRoomCount++;

        // 2. 循环生成
        int iterations = 0;
        while (functionalRoomCount < maxRooms && iterations < 1000)
        {
            iterations++;
            AttemptSpawnBranch();
        }

        // 3. 封口
        foreach (var r in allSpawnedObjects) r.CloseUnconnectedExits();
    }

    void AttemptSpawnBranch()
    {
        List<Room> currentBranch = new List<Room>();
        // 临时记录本分支占用的区域，如果失败了方便回滚（只需不加入主列表即可）
        List<Rect> branchRects = new List<Rect>();

        List<Room> availableRooms = allSpawnedObjects.Where(r => r.GetFreeExits().Count > 0).ToList();
        Shuffle(availableRooms);

        Room startNode = null;
        RoomExit startExit = null;

        foreach (var room in availableRooms)
        {
            var exits = room.GetFreeExits();
            if (exits.Count > 0)
            {
                startNode = room;
                startExit = exits[Random.Range(0, exits.Count)];
                break;
            }
        }

        if (startNode == null) return;

        Transform connectPoint = startExit.point;
        Direction needDir = GetOppositeDirection(startExit.direction);

        // 生成通道链
        int length = Random.Range(minCorridorLength, maxCorridorLength + 1);

        for (int i = 0; i < length; i++)
        {
            // 尝试创建并检测，传入当前临时的 branchRects 以避免自交
            Room newCorridor = TryCreateRoom(corridorPrefabs, needDir, connectPoint, branchRects);

            if (newCorridor == null)
            {
                DestroyBranch(currentBranch);
                return;
            }

            // 成功，加入列表
            currentBranch.Add(newCorridor);
            branchRects.Add(CalculateRoomRect(newCorridor)); // 记录区域

            RoomExit entry = newCorridor.GetFreeExitWithDirection(needDir);
            entry.isOccupied = true;

            var possibleExits = newCorridor.GetFreeExits();
            if (possibleExits.Count == 0)
            {
                DestroyBranch(currentBranch);
                return;
            }

            RoomExit nextExit = possibleExits[Random.Range(0, possibleExits.Count)];
            nextExit.isOccupied = true;
            connectPoint = nextExit.point;
            needDir = GetOppositeDirection(nextExit.direction);
        }

        // 生成终点
        Room endRoom = TryCreateRoom(roomPrefabs, needDir, connectPoint, branchRects);

        if (endRoom == null)
        {
            DestroyBranch(currentBranch);
            return;
        }

        branchRects.Add(CalculateRoomRect(endRoom));
        endRoom.GetFreeExitWithDirection(needDir).isOccupied = true;

        // --- 全部成功，正式提交数据 ---
        startExit.isOccupied = true;
        allSpawnedObjects.AddRange(currentBranch);
        allSpawnedObjects.Add(endRoom);

        // 将本分支的区域合并到总占用表中
        occupiedRects.AddRange(branchRects);

        functionalRoomCount++;
    }

    /// <summary>
    /// 尝试创建房间，使用的是纯数学计算检测，不再依赖Physics2D
    /// </summary>
    Room TryCreateRoom(Room[] prefabPool, Direction requiredDir, Transform targetPoint, List<Rect> currentBranchRects)
    {
        List<Room> pool = new List<Room>(prefabPool);
        Shuffle(pool);

        foreach (Room prefab in pool)
        {
            int exitIndex = -1;
            for (int i = 0; i < prefab.exits.Length; i++)
            {
                if (!prefab.exits[i].isOccupied && prefab.exits[i].direction == requiredDir)
                {
                    exitIndex = i;
                    break;
                }
            }

            if (exitIndex == -1) continue;

            // --- 核心修改：手动计算目标位置的 Rect ---

            // 1. 获取接口局部坐标
            Vector3 exitLocalPos = prefab.exits[exitIndex].point.localPosition;

            // 2. 预测生成后的世界坐标原点
            Vector3 potentialWorldPos = targetPoint.position - exitLocalPos;

            // 3. 计算预测的矩形范围 (使用Prefab的Collider数据)
            BoxCollider2D col = prefab.GetComponent<BoxCollider2D>();
            Rect potentialRect = CalculateRectFromData(potentialWorldPos, col);

            // 4. 与“总表”和“当前分支临时表”比对
            if (IsOverlapping(potentialRect, occupiedRects) || IsOverlapping(potentialRect, currentBranchRects))
            {
                continue; // 重叠了，换下一个
            }

            // 5. 通过检测，实例化
            Room instance = Instantiate(prefab);
            instance.transform.position = potentialWorldPos;
            return instance;
        }

        return null;
    }

    // --- 纯数学辅助方法 ---

    // 注册房间到总表
    void RegisterRoom(Room room)
    {
        occupiedRects.Add(CalculateRoomRect(room));
    }

    // 根据实例计算矩形
    Rect CalculateRoomRect(Room room)
    {
        BoxCollider2D col = room.GetComponent<BoxCollider2D>();
        return CalculateRectFromData(room.transform.position, col);
    }

    // 核心数学公式：根据位置和Collider数据算出世界坐标下的 Rect
    Rect CalculateRectFromData(Vector3 worldPos, BoxCollider2D col)
    {
        // 假设 scale 都是 1，如果不是，需要乘 lossyScale
        // 稍微缩小一点点(0.9f)作为容错，允许边缘刚好接触
        Vector2 size = col.size * 0.95f;
        Vector2 centerOffset = col.offset;

        // Rect 的构造函数参数是 (x, y, width, height)，其中 x,y 是左下角
        Vector2 worldCenter = (Vector2)worldPos + centerOffset;
        Vector2 bottomLeft = worldCenter - (size * 0.5f);

        return new Rect(bottomLeft, size);
    }

    // 检测矩形是否与列表中的任意矩形重叠
    bool IsOverlapping(Rect target, List<Rect> existingRects)
    {
        foreach (var r in existingRects)
        {
            if (target.Overlaps(r)) return true;
        }
        return false;
    }

    Direction GetOppositeDirection(Direction dir)
    {
        if (dir == Direction.Up) return Direction.Down;
        if (dir == Direction.Down) return Direction.Up;
        if (dir == Direction.Left) return Direction.Right;
        return Direction.Left;
    }

    void DestroyBranch(List<Room> branch)
    {
        foreach (var r in branch) Destroy(r.gameObject);
    }

    void Shuffle<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            T temp = list[i];
            int randomIndex = Random.Range(i, list.Count);
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }
}