using System;
using SheepyCreations.UIControl.Runtime;
using UnityEngine;

// HUD panel behaviour: forwards the "add post-it" button as a plain event, so PostItCreationFlow
// doesn't need to know anything about bridges or UI Toolkit elements.
public class HUD : PanelBehaviour
{
    [SerializeField] private ButtonBridge addPostItButton;

    public event Action AddPostItRequested;

    private void OnEnable()
    {
        if (addPostItButton != null) addPostItButton.Clicked += RaiseAddPostItRequested;
    }

    private void OnDisable()
    {
        if (addPostItButton != null) addPostItButton.Clicked -= RaiseAddPostItRequested;
    }

    private void RaiseAddPostItRequested()
    {
        AddPostItRequested?.Invoke();
    }
}