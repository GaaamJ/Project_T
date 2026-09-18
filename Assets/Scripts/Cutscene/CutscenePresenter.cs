using UnityEngine;
using Yarn.Unity;

public class CutscenePresenter : MonoBehaviour
{
    [SerializeField] DialogueRunner dialogueRunner;

    void OnEnable()
    {
        dialogueRunner.AddCommandHandler<string>("Cutscene", PlayCutscene);
    }

    void OnDisable()
    {
        dialogueRunner.RemoveCommandHandler("Cutscene");
    }

    void PlayCutscene(string id)
    {
        Debug.Log($"[Cutscene] {id}");
    }
}
