using System.IO;
using UnityEditor;
using UnityEngine;

public static class SaveManagerTools
{
    private static string SavePath => Path.Combine(Application.persistentDataPath, "save.json");

    [MenuItem("Tools/Save/Delete Save File")]
    private static void DeleteSaveFile()
    {
        if (!File.Exists(SavePath))
        {
            Debug.Log("[SaveManagerTools] save.json 없음.");
            return;
        }
        File.Delete(SavePath);
        Debug.Log($"[SaveManagerTools] 삭제 완료: {SavePath}");
    }

    [MenuItem("Tools/Save/Open Save Folder")]
    private static void OpenSaveFolder()
    {
        EditorUtility.RevealInFinder(Application.persistentDataPath);
    }
}
