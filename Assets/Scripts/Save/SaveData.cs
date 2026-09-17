using System.Collections.Generic;

namespace ProjectT.Save
{
    // JSON 직렬화 대상. JsonUtility 를 사용하기 때문에 반드시 [System.Serializable] 이
    // 필요하며, public 필드로 선언해야 한다 (프로퍼티/private 필드는 직렬화되지 않음).
    // 게임 진행 상태와 플레이어 데이터만 포함한다. 설정값(볼륨 등)은 PlayerPrefs 로 관리.
    [System.Serializable]
    public class SaveData
    {
        // 프롤로그(Overture) 완주 여부. TitleManager 가 이 값으로 다음 씬을 분기한다.
        public bool prologueDone;

        // 플레이어 이름. 아직 미입력 상태를 나타내기 위해 기본값 null 을 유지한다
        // (문자열 초기화를 하지 않으면 자동으로 null).
        public string playerName;

        // 완료된 이벤트 ID 목록. EventState 의 HashSet 은 JsonUtility 로 직렬화가 불가능해서
        // List<string> 으로 저장하고, EventState.Load/Save 에서 상호 변환한다.
        // 기본값을 두지 않아 첫 실행 시 null 로 시작하며, Load 쪽에서 null 체크 후 초기화한다.
        public List<string> completedEventIds;
    }
}
