using UnityEngine;

namespace ProjectT.Thread
{
    // 스폰 매니저가 "이 위치가 이미 사용 중인가"를 알아야 하므로
    // 위치 자체가 자기 상태(점유 여부)를 들고 있게 한다.
    // 매니저가 별도로 dict로 관리하지 않아도 되고, 씬에서 위치만 늘리면 확장된다.
    public class ThreadSpawnPoint : MonoBehaviour
    {
        public bool IsOccupied { get; private set; }

        public void Occupy() => IsOccupied = true;
        public void Free() => IsOccupied = false;
    }
}
