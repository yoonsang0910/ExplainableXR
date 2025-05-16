using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using System;

public class TemplateBasedLogDefinition : ScriptableObject
{
    public List<Action> Actions = new();
}

[Serializable]
public class Action
{
    public string Name;
    public string ActionTriggerSource = "XRHMD";
    public List<Intent> Intents = new();
    public List<Binding> Bindings = new();
}


[Serializable]
public class Intent
{
    public string Name;
    public string IntentType;

}

[Serializable]
public class Binding
{
    public InputActionType ActionType;
    public string ActionTriggerSourceBinding;
}
