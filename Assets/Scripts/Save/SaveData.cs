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
    }
}
