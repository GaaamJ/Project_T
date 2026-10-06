using System;
using UnityEngine;
using UnityEngine.InputSystem;
using ProjectT.Session;

namespace ProjectT.Player
{
    // 조사 입력(Z)을 받아 잠금 중이 아닐 때만 알린다. 대화 진행용 Z는 Yarn이 따로 읽는다.
    public class PlayerInteractInput : MonoBehaviour
    {
        [SerializeField] InputActionReference interactAction;

        public event Action InteractPressed;

        void OnEnable()
        {
            interactAction.action.performed += OnInteract;
            interactAction.action.Enable();
        }

        void OnDisable()
        {
            interactAction.action.performed -= OnInteract;
        }

        void OnInteract(InputAction.CallbackContext context)
        {
            GameSessionManager session = GameSessionManager.Instance;
            if (session != null && session.IsInputLocked)
                return;

            InteractPressed?.Invoke();
        }
    }
}
