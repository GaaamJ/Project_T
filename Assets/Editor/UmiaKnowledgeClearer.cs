#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

// Umia의 학습 데이터(맵 지식)를 초기화하는 에디터 전용 유틸.
// 개발 중 반복 테스트에서 이전 세션의 지식이 남아 있으면
// 탐색 로직이 원하는 상태로 시작하지 않기 때문에, 원클릭 초기화 메뉴가 필요하다.
public static class UmiaKnowledgeClearer
{
    [MenuItem("Tools/ProjectT/Clear Umia Knowledge")]
    static void ClearKnowledge()
    {
        string path = Path.Combine(Application.persistentDataPath, "umia_knowledge.json");
        if (File.Exists(path))
        {
            File.Delete(path);
            Debug.Log($"[UmiaKnowledge] Deleted: {path}");
        }
        else
        {
            Debug.Log($"[UmiaKnowledge] File not found (already clean): {path}");
        }
    }
}
#endif
