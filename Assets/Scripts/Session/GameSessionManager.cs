using UnityEngine;
using ProjectT.Save;

namespace ProjectT.Session
{
    public class GameSessionManager : MonoBehaviour
    {
        public static GameSessionManager Instance { get; private set; }

        public EventState EventState { get; private set; } = new EventState();

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            SaveManager.Load();
            EventState.Load(SaveManager.Data);
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
