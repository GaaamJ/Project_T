using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class NumberKeyOptionSelector : MonoBehaviour
{
    [SerializeField] private Transform optionContainer;

    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        // 1~4: 하이라이트만 이동
        for (int i = 0; i < optionContainer.childCount; i++)
        {
            var numberKey = keyboard[Key.Digit1 + i];
            if (numberKey.wasPressedThisFrame)
            {
                var optionGO = optionContainer.GetChild(i).gameObject;
                if (!optionGO.activeInHierarchy) continue;

                EventSystem.current.SetSelectedGameObject(optionGO);
            }
        }

        // Space: 하이라이트된 걸 제출
        if (keyboard.spaceKey.wasPressedThisFrame)
        {
            var selected = EventSystem.current.currentSelectedGameObject;
            if (selected != null)
            {
                ExecuteEvents.Execute(
                    selected,
                    new BaseEventData(EventSystem.current),
                    ExecuteEvents.submitHandler
                );
            }
        }
    }
}