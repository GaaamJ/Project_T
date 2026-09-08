using UnityEngine;
using ProjectT.Thread;

// 스테이지 매니저 연동 전 테스트용. StartStage() 자동 호출.
public class DebugStageStarter : MonoBehaviour
{
    [SerializeField] YarnSpawnManager spawnManager;

    void Start()
    {
        spawnManager.StartStage();
    }

    [ContextMenu("Start Stage")]
    void StartStageManual() => spawnManager.StartStage();
}
