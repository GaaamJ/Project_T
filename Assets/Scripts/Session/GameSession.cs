using System.Collections.Generic;
using UnityEngine;
using ProjectT.Save;

namespace ProjectT.Session
{
    // 씬을 다시 불러와도 유지되고, 게임을 다시 켜면 비워지는 값만 둔다.
    public static class GameSession
    {
        // 조사한 ObjectData.Id
        public static HashSet<string> Investigated { get; private set; }

        // 정적 필드 초기화 대신 여기서 만드는 이유: 도메인 리로드를 끈 플레이 모드에서도 실행마다 비우기 위함.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Begin()
        {
            Investigated = new HashSet<string>();
            SaveManager.Load();
        }
    }
}
