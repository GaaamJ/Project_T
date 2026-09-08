using UnityEngine;

namespace ProjectT.Thread
{
    // SpriteRenderer는 Initialize에서 타입별 스프라이트를 스왑하기 위해 반드시 필요.
    // 프리팹에서 누락되지 않도록 RequireComponent로 강제한다.
    [RequireComponent(typeof(SpriteRenderer))]
    public class Yarn : MonoBehaviour
    {
        [SerializeField] Sprite redSprite;
        [SerializeField] Sprite blueSprite;
        [SerializeField] Sprite goldSprite;

        ThreadType type;
        YarnSpawnPoint spawnPoint;
        YarnSpawnManager spawnManager;

        // 프리팹 하나로 3색을 다 표현하기 위해 스폰 시점에 타입을 주입받아
        // 스프라이트를 스왑한다. 프리팹을 3개로 분리하면 관리 지점이 늘어나므로 통합.
        public void Initialize(ThreadType threadType, YarnSpawnPoint point, YarnSpawnManager manager)
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
        // Player·Umia 모두 ThreadBuffHolder를 직접 사용하므로 추상화 불필요.
        // 실을 얻는 대상이 ThreadBuffHolder가 아닌 타입으로 확장될 때 인터페이스로 승격.
        public void TakeHit(ThreadBuffHolder attacker)
        {
            attacker?.Acquire(type);
            spawnPoint?.Free();
            spawnManager?.NotifyDestroyed(type, spawnPoint);
            Destroy(gameObject);
        }
    }
}
