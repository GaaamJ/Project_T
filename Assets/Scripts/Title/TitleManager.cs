using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectT.Title
{
    // 타이틀 화면의 Play 버튼을 처리하는 단일 매니저.
    // PlayerPrefs 의 "PrologueDone" 키 유무만으로 다음 씬을 분기한다.
    // EncounterDone 등의 추가 플래그는 사용하지 않는다 — EncounterScene 내부에서
    // 이벤트 스킵 여부를 자체 판단하기 때문에 타이틀에서는 관여하지 않는다.
    public class TitleManager : MonoBehaviour
    {
        // 진행 상태별 로드 대상 씬 이름. Build Settings 등록 이름과 반드시 일치해야 한다.
        // 상수로 두는 이유는 오타 방지 + 이 매니저가 담당하는 라우팅 규칙을 한 곳에 명시하기 위함.
        const string OvertureSceneName = "Overture";
        const string EncounterSceneName = "Encounter";

        // PlayerPrefs 키. 프롤로그(Overture) 완주 여부만 저장한다.
        // 값 자체는 관심 없고 HasKey 로 존재 여부만 판정.
        const string PrologueDoneKey = "PrologueDone";

        [Tooltip("타이틀의 Play 버튼. 클릭 시 다음 씬으로 이동한다.")]
        [SerializeField] Button playButton;

        void OnEnable()
        {
            // OnEnable/OnDisable 쌍으로 리스너를 관리해서, 비활성/재활성 사이클에서도
            // 중복 등록이 발생하지 않도록 한다. 씬 시작 시에도 자동으로 등록된다.
            if (playButton != null)
                playButton.onClick.AddListener(OnPlayClicked);
            else
                Debug.LogWarning("[TitleManager] playButton 참조가 비어 있습니다.");
        }

        void OnDisable()
        {
            if (playButton != null)
                playButton.onClick.RemoveListener(OnPlayClicked);
        }

        void OnPlayClicked()
        {
            // 씬 로드가 비동기적으로 처리되는 동안 사용자가 버튼을 여러 번 눌러
            // LoadScene 이 중복 호출되는 사고를 막기 위해 즉시 상호작용을 잠근다.
            // 여기서 True 로 되돌리지 않는 이유: 이 씬 자체가 다음 씬 로드로 곧 파괴되기 때문.
            if (playButton != null)
                playButton.interactable = false;

            // PrologueDone 이 없다 = 프롤로그를 아직 안 봤다 → Overture 로 진입.
            // PrologueDone 이 있다 = 프롤로그를 이미 봤다 → 바로 Encounter 로 진입.
            string nextScene = PlayerPrefs.HasKey(PrologueDoneKey)
                ? EncounterSceneName
                : OvertureSceneName;

            Debug.Log($"[TitleManager] Play 클릭 → '{nextScene}' 로드");
            SceneManager.LoadScene(nextScene);
        }
    }
}
