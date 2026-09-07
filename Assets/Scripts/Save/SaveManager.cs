using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class SaveManager : MonoBehaviour
{
    private static SaveManager _instance;
    public static SaveManager Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("SaveManager");
                _instance = go.AddComponent<SaveManager>();
            }
            return _instance;
        }
    }

    private static readonly string SavePath =
        Path.Combine(Application.persistentDataPath, "save.json");

    private HashSet<string> _visited = new HashSet<string>();

    [Serializable]
    private class SaveData
    {
        public List<string> visitedSpaces = new List<string>();
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        Load();
    }

    public bool IsVisited(string spaceId) => _visited.Contains(spaceId);

    public void MarkVisited(string spaceId)
    {
        if (_visited.Add(spaceId))
            Save();
    }

    private void Save()
    {
        var data = new SaveData { visitedSpaces = new List<string>(_visited) };
        File.WriteAllText(SavePath, JsonUtility.ToJson(data, true));
    }

    private void Load()
    {
        if (!File.Exists(SavePath)) return;
        try
        {
            var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
            _visited = new HashSet<string>(data.visitedSpaces);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SaveManager] 로드 실패, 초기화: {e.Message}");
            _visited = new HashSet<string>();
        }
    }
}
