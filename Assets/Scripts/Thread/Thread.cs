using UnityEngine;

namespace ProjectT.Thread
{
    public class Thread : MonoBehaviour
    {
        [SerializeField] Sprite redSprite;
        [SerializeField] Sprite blueSprite;
        [SerializeField] Sprite goldSprite;

        ThreadType type;
        ThreadSpawnPoint spawnPoint;
        ThreadSpawnManager spawnManager;

        // 프리팹 하나로 3색을 다 표현하기 위해 스폰 시점에 타입을 주입받아
        // 스프라이트를 스왑한다. 프리팹을 3개로 분리하면 관리 지점이 늘어나므로 통합.
        public void Initialize(ThreadType threadType, ThreadSpawnPoint point, ThreadSpawnManager manager)
        {
            type = threadType;
            spawnPoint = point;
            spawnManager = manager;
            GetComponent<SpriteRenderer>().sprite = type switch
            {
                ThreadType.Red  => redSprite,
                ThreadType.Blue => blueSprite,
                _               => goldSprite,
            };
        }

        // 옵션 A: IBuffHolder 인터페이스를 만들지 않고 ThreadBuffHolder를 직접 받는다.
        // 실을 얻을 수 있는 대상이 지금은 플레이어(ThreadBuffHolder)뿐이라 굳이 추상화하지 않음.
        // 나중에 다른 타입이 실을 획득해야 하는 상황이 생기면 그때 인터페이스로 승격.
        public void TakeHit(ThreadBuffHolder attacker)
        {
            attacker?.Acquire(type);
            spawnPoint?.Free();
            spawnManager?.NotifyDestroyed(type);
            Destroy(gameObject);
        }

        // 실제 공격 시스템이 붙기 전까지 스폰/리스폰 로직을 눈으로 확인하기 위한 테스트용.
        // 버프 없이 파괴만 트리거해서 리스폰이 정상 도는지 검증한다.
        [ContextMenu("Test Destroy (No Buff)")]
        void TestDestroy()
        {
            spawnPoint?.Free();
            spawnManager?.NotifyDestroyed(type);
            Destroy(gameObject);
        }
    }
}
