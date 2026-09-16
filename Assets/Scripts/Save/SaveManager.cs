using System;
using System.IO;
using UnityEngine;

namespace ProjectT.Save
{
    // JSON 파일 기반의 정적 세이브 매니저.
    // - 저장 위치: Application.persistentDataPath/save.json
    //   (플랫폼별 표준 저장소를 자동 제공하므로 별도 경로 관리 불필요.)
    // - PlayerPrefs 는 설정값 전용으로 남기고, 게임 진행 상태는 이 시스템만 사용한다.
    // - static 으로 노출하는 이유: 세이브는 씬 라이프사이클과 무관한 전역 상태이며,
    //   유일해야 하는 자원(디스크 파일)을 다루기 때문. 인스턴스를 여러 곳으로 넘길
    //   필요가 없어 참조 전달 대비 API 부담이 확연히 적다.
    public static class SaveManager
    {
        // 세션 동안 유지되는 현재 세이브 데이터. Load() 를 호출하지 않아도
        // 항상 유효한 기본값을 제공하기 위해 static 초기화로 새 인스턴스를 만든다.
        // 참조하는 쪽(TitleManager 등)에서 null 체크 없이 바로 필드에 접근할 수 있도록.
        public static SaveData Data { get; private set; } = new SaveData();

        // 파일 이름을 상수로 두는 이유: 경로 조합을 한 곳에서 관리하여
        // 저장/삭제(SaveManagerTools) 사이의 불일치를 원천 차단하기 위함.
        const string SaveFileName = "save.json";

        // Application.persistentDataPath 는 메인 스레드에서만 안전하게 접근 가능하지만,
        // Save/Load 는 모두 메인 스레드에서 호출되므로 프로퍼티로 즉시 계산해도 충분하다.
        static string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

        // 파일이 없으면 기본값(new SaveData()) 로 초기화하고 조용히 종료한다.
        // 파일이 있지만 파싱 실패(손상 등) 시에도 로그 경고 후 기본값으로 폴백해서
        // 게임 진입 자체가 막히는 상황을 피한다.
        public static void Load()
        {
            if (!File.Exists(SavePath))
            {
                // 첫 실행 등 파일이 아직 없는 정상 상황. 새 인스턴스로 초기화만 하고 반환.
                Data = new SaveData();
                return;
            }

            try
            {
                string json = File.ReadAllText(SavePath);
                SaveData loaded = JsonUtility.FromJson<SaveData>(json);

                // JsonUtility.FromJson 은 빈 문자열 등 일부 입력에서 null 을 반환할 수 있다.
                // null 그대로 두면 이후 Data.prologueDone 접근 시 NullReferenceException 이
                // 나므로 반드시 기본값으로 대체한다.
                Data = loaded ?? new SaveData();
            }
            catch (Exception e)
            {
                // 손상된 세이브를 만나도 게임을 진입 가능한 상태로 유지한다.
                // 파일을 자동 삭제하지 않는 이유: 사용자가 원본을 확인/복구할 여지를 남긴다.
                Debug.LogWarning($"[SaveManager] save.json 로드 실패, 기본값으로 폴백합니다: {e.Message}");
                Data = new SaveData();
            }
        }

        // 현재 Data 를 JSON 으로 직렬화해 SavePath 에 덮어쓴다.
        // prettyPrint=true 는 디버깅 편의를 위한 선택이며 파일 크기는 세이브 규모상 무시 가능.
        // 쓰기 실패(디스크 꽉 참, 권한 없음 등) 시 크래시 대신 경고만 남긴다.
        public static void Save()
        {
            try
            {
                string json = JsonUtility.ToJson(Data, true);
                File.WriteAllText(SavePath, json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveManager] save.json 저장 실패: {e.Message}");
            }
        }
    }
}
