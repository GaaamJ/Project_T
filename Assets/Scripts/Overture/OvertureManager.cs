using System;
using ProjectT.Save;
using UnityEngine;
using UnityEngine.SceneManagement;
using Yarn.Unity;

namespace ProjectT.Overture
{
    // OvertureScene 의 진입점.
    // - Yarn 대화 흐름을 시작하고, 완료 시 세이브 갱신 + Encounter 씬으로 전환한다.
    // - $pc_username 은 여기서만 세팅하고 이후 NameInputView 등 다른 스크립트가
    //   $in_game_name 같은 자기 담당 변수를 관리한다. 책임을 한 곳에 몰지 않기 위함.
    public class OvertureManager : MonoBehaviour
    {
        // Build Settings 등록 이름과 반드시 일치해야 한다. 오타를 한 곳에서 관리하기 위해 상수화.
        const string EncounterSceneName = "Encounter";

        // Yarn 프로젝트에서 실행할 첫 노드. 노드 리네이밍 시 여기 한 곳만 바꾸면 되게 상수화.
        const string StartNodeName = "Overture_Start";

        [Tooltip("씬에 배치된 DialogueSystem 프리팹의 DialogueRunner 참조.")]
        [SerializeField] DialogueRunner dialogueRunner;

        void Awake()
        {
            // onDialogueComplete 는 UnityEvent 라서 인스펙터로도 연결 가능하지만,
            // 코드로 등록해서 씬 파일과 스크립트 사이의 참조 누락 가능성을 줄인다.
            // OnDestroy 에서 반드시 짝을 맞춰 해제한다.
            if (dialogueRunner != null)
                dialogueRunner.onDialogueComplete.AddListener(OnDialogueComplete);
            else
                Debug.LogWarning("[OvertureManager] dialogueRunner 참조가 비어 있습니다.");
        }

        void OnDestroy()
        {
            // 씬 전환 시 DialogueRunner 도 함께 파괴되지만, 다른 매니저가 붙어 있을 수 있어
            // 명시적으로 해제해 이벤트 누수 위험을 원천 차단한다.
            if (dialogueRunner != null)
                dialogueRunner.onDialogueComplete.RemoveListener(OnDialogueComplete);
        }

        void Start()
        {
            if (dialogueRunner == null) return;

            // $pc_username 은 "게임 밖 사용자 이름" 이라 세이브 대상이 아님.
            // 매 씬 진입마다 최신 값을 다시 주입한다.
            // Environment.UserName 이 예외로 실패할 가능성은 극히 낮지만
            // 그래도 대사 흐름이 막히지 않도록 빈 문자열로 폴백.
            string osUserName;
            try
            {
                osUserName = Environment.UserName ?? string.Empty;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[OvertureManager] Environment.UserName 취득 실패, 빈 문자열로 폴백: {e.Message}");
                osUserName = string.Empty;
            }

            // 노드가 시작하기 전에 변수를 세팅해야 첫 대사에서 {$pc_username} 이 올바르게 치환된다.
            dialogueRunner.VariableStorage.SetValue("$pc_username", osUserName);

            // 자동 검증 지원: $pc_username 주입 결과와 시작 노드를 콘솔에서 확인할 수 있도록 로그.
            Debug.Log($"[OvertureManager] $pc_username='{osUserName}' 주입 → '{StartNodeName}' 노드 시작");

            dialogueRunner.StartDialogue(StartNodeName);
        }

        void OnDialogueComplete()
        {
            // 대화 완료 = 프롤로그 완주. TitleManager 가 이 플래그로 다음 진입 씬을 분기하므로
            // 저장까지 확실히 마친 뒤 씬 전환한다.
            SaveManager.Data.prologueDone = true;
            SaveManager.Save();

            Debug.Log($"[OvertureManager] 프롤로그 완료 → '{EncounterSceneName}' 로드 (prologueDone=true, playerName='{SaveManager.Data.playerName}')");
            SceneManager.LoadScene(EncounterSceneName);
        }
    }
}
