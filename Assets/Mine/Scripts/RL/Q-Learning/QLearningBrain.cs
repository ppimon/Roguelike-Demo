using System.Collections.Generic;
using UnityEngine;
using System.IO;

[System.Serializable]
public class QTableEntry
{
    public string state;
    public float[] values;
}

public class QLearningBrain
{
    Dictionary<string, float[]> QTable = new Dictionary<string, float[]>();

    int actionCount = 5;
    float lr = 0.1f;
    float gamma = 0.9f;
    float epsilon = 0.3f;

    string StateToKey(int d, int h)
    {
        return d + "_" + h;
    }

    public int ChooseAction(int d, int h)
    {
        string key = StateToKey(d, h);

        if (!QTable.ContainsKey(key))
            QTable[key] = new float[actionCount];

        if (Random.value < epsilon)
            return Random.Range(0, actionCount);

        float[] q = QTable[key];

        int best = 0;
        for (int i = 1; i < q.Length; i++)
            if (q[i] > q[best]) best = i;

        return best;
    }

    public void UpdateQ(int d, int h, int action, float reward, int nd, int nh)
    {
        Debug.Log("Q更新: " + d + "_" + h);

        string key = StateToKey(d, h);
        string nextKey = StateToKey(nd, nh);

        if (!QTable.ContainsKey(key))
            QTable[key] = new float[actionCount];

        if (!QTable.ContainsKey(nextKey))
            QTable[nextKey] = new float[actionCount];

        float maxNext = Mathf.Max(QTable[nextKey]);

        QTable[key][action] += lr * (reward + gamma * maxNext - QTable[key][action]);
    }

    public float[] GetQValues(int d, int h)
    {
        string key = d + "_" + h;

        if (!QTable.ContainsKey(key))
            QTable[key] = new float[actionCount];

        return QTable[key];
    }

    public void SaveQTable()
    {
        Debug.Log("QTable数量: " + QTable.Count);

        List<QTableEntry> list = new List<QTableEntry>();

        foreach (var kv in QTable)
        {
            list.Add(new QTableEntry
            {
                state = kv.Key,
                values = kv.Value
            });
        }

        string json = JsonUtility.ToJson(new Wrapper { list = list }, true);

        string path = Application.persistentDataPath + "/qtable.json";
        File.WriteAllText(path, json);

        Debug.Log("QTable 已保存到: " + path);
    }

    [System.Serializable]
    class Wrapper
    {
        public List<QTableEntry> list;
    }

    public void LoadQTable()
    {
        string path = Application.persistentDataPath + "/qtable.json";

        if (!File.Exists(path))
        {
            Debug.Log("未找到QTable存档");
            return;
        }

        string json = File.ReadAllText(path);

        Wrapper wrapper = JsonUtility.FromJson<Wrapper>(json);

        QTable.Clear();

        foreach (var entry in wrapper.list)
        {
            QTable[entry.state] = entry.values;
        }

        Debug.Log("QTable 已加载");
    }
}