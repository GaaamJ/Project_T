using Pathfinding;
using UnityEditor;
using UnityEngine;

namespace ProjectT.EditorTools
{
    // GridGraph 초기 세팅 유틸. use2D 관련 필드만 코드로 강제하고
    // 크기/센터/장애물 레이어는 인스펙터에서 사용자가 직접 설정하도록 남긴다.
    // (사용자 요구: 자동 Scan/자동 크기지정 금지)
    public static class AstarSetup
    {
        [MenuItem("Tools/ProjectT/AStar/Add GridGraph With 2D Settings")]
        public static void AddGridGraph()
        {
            var astar = Object.FindFirstObjectByType<AstarPath>();
            if (astar == null)
            {
                Debug.LogError("[AstarSetup] AstarPath component not found in scene.");
                return;
            }

            if (astar.data == null || astar.data.graphs == null)
            {
                Debug.LogError("[AstarSetup] AstarPath.data.graphs is null.");
                return;
            }

            GridGraph existing = null;
            foreach (var g in astar.data.graphs)
            {
                if (g is GridGraph gg) { existing = gg; break; }
            }

            GridGraph graph;
            if (existing != null)
            {
                graph = existing;
                Debug.Log("[AstarSetup] GridGraph already exists — updating 2D collision flags only.");
            }
            else
            {
                graph = astar.data.AddGraph(typeof(GridGraph)) as GridGraph;
                Debug.Log("[AstarSetup] GridGraph added.");
            }

            // 2D 프로젝트이므로 use2D 강제. 나머지는 사용자 설정.
            graph.collision.use2D = true;
            graph.collision.type = ColliderType.Sphere;
            graph.collision.diameter = 0.8f;

            EditorUtility.SetDirty(astar);
            Debug.Log("[AstarSetup] Done. Configure GridGraph dimensions, center, and obstacle layer in Inspector, then click Scan.");
        }
    }
}
