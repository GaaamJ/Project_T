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
            if (dialogueRunner == null)
                Debug.LogWarning("[OvertureManager] dialogueRunner 참조가 비어 있습니다.");
        }

        void Start()
        {
            if (dialogueRunner == null) return;

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

            dialogueRunner.VariableStorage.SetValue("$pc_username", osUserName);
            Debug.Log($"[OvertureManager] $pc_username='{osUserName}' 주입 → '{StartNodeName}' 노드 시작");

            // YarnTask.CompletedTask 가 AwaitableCompletionSource 를 Reset() 으로
            // 즉시 재활용하므로, StartDialogue 내부의 await WhenAll(CompletedTask...)
            // 이 영구 hang 된다. StartDialogue 는 WhenAll 이전 단계(SetProgram, SetNode,
            // OnDialogueStartedAsync 등)를 동기로 완료하므로, 반환 후 Continue() 를 직접
            // 호출해 대사를 시작한다. 같은 이유로 onDialogueComplete 이벤트도 발화되지 않아
            // DialogueCompleteHandler 를 직접 후킹한다.
            var origCompleteHandler = dialogueRunner.Dialogue.DialogueCompleteHandler;
            dialogueRunner.Dialogue.DialogueCompleteHandler = () =>
            {
                origCompleteHandler?.Invoke();
                OnDialogueComplete();
            };

            dialogueRunner.StartDialogue(StartNodeName);
            dialogueRunner.Dialogue.Continue();
        }

        void OnDialogueComplete()
        {
            SaveManager.Data.prologueDone = true;
            SaveManager.Save();

            Debug.Log($"[OvertureManager] 프롤로그 완료 → '{EncounterSceneName}' 로드 (prologueDone=true, playerName='{SaveManager.Data.playerName}')");
            SceneManager.LoadScene(EncounterSceneName);
        }
    }
}
