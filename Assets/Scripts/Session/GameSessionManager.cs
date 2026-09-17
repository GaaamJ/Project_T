using UnityEngine;
using ProjectT.Save;

namespace ProjectT.Session
{
    // 게임 진행 중에만 유효한 런타임 세션 데이터의 소유자.
    //
    // 왜 별도 매니저인가:
    // - SaveManager 는 디스크 I/O 만 담당한다 (SaveData 직렬화/역직렬화).
    // - EventState 는 세이브를 원본 소스로 삼는 런타임 미러 데이터이며,
    //   씬 이동 사이에도 유지되어야 한다.
    // - 이 둘의 관심사를 섞으면 SaveManager 가 도메인 로직(EventState 갱신)까지
    //   떠안게 되어 추후 다른 런타임 세션 데이터(플래그, 카운터 등) 추가 시
    //   SaveManager 가 계속 부풀어난다.
    //
    // 왜 싱글톤 MonoBehaviour + DontDestroyOnLoad 인가:
    // - EventRunner 는 씬에 배치되는 컴포넌트이므로, 씬 로드 시점마다 자신의 참조
    //   대상(EventState)이 사라졌다 다시 생기면 안 된다.
    // - 그렇다고 EventRunner 에 EventState 를 Inspector 로 매번 연결시키는 것은
    //   실수의 여지를 늘린다 (연결 누락 시 NRE, 씬마다 새 인스턴스 → 세션 상태 소실).
    // - 단일 세션 상태를 씬 이동과 무관하게 유지하기 위한 도구로써 싱글톤이 가장 단순한 해법.
    //
    // Awake 초기화 순서:
    // - SaveManager.Data 는 static 초기값(new SaveData()) 을 보장하므로 SaveManager.Load()
    //   호출 전에도 안전하게 참조 가능. 다만 실제 세이브 파일 반영을 위해서는 Load 가
    //   선행되어야 하므로, Awake 에서 SaveManager.Load() 를 명시적으로 호출한다.
    //   (Step 4 승인: SaveManager 직접 참조 A안)
    public class GameSessionManager : MonoBehaviour
    {
        public static GameSessionManager Instance { get; private set; }

        public EventState EventState { get; private set; } = new EventState();

        void Awake()
        {
            // 이미 다른 씬에서 생성되어 DontDestroyOnLoad 로 살아있는 인스턴스가 있으면
            // 신규 인스턴스는 폐기하여 세션 상태가 두 개로 갈라지는 상황을 방지한다.
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // 디스크에서 세이브를 읽어 SaveManager.Data 를 최신 상태로 만든 뒤,
            // EventState 가 그것을 흡수한다. 순서를 뒤집으면 초기값(빈 SaveData) 기준으로
            // EventState 가 비어버리므로 반드시 Load 를 먼저 호출한다.
            SaveManager.Load();
            EventState.Load(SaveManager.Data);
        }

        void OnDestroy()
        {
            // 자신이 활성 인스턴스일 때만 전역 참조를 해제한다.
            // 중복 생성 → Destroy 경로에서 활성 인스턴스의 참조까지 지워버리는 사고 방지.
            if (Instance == this)
                Instance = null;
        }
    }
}
