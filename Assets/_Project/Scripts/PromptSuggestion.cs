using System.Collections.Generic;
using SheepyCreations.UIControl.Runtime;
using TMPro;
using UnityEngine;

public class PromptSuggestion : MonoBehaviour
{
    [SerializeField] private UIPanel panel;
    [SerializeField] private LabelTextBridge label;
    [SerializeField] private List<Prompt_SO> promptList;

    private void OnEnable() => ShowRandom(); // se llama cada vez que se abre el panel de creación

    public void ShowRandom()
    {
        if (promptList.Count == 0) return;
        label.SetText(promptList[Random.Range(0, promptList.Count)].prompt);
    }
}