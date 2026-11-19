using UnityEngine;

[CreateAssetMenu(menuName = "Game/Objectives/Objective")]
public class ObjectiveData : ScriptableObject
{
    public string objectiveID;
    public string title;
    [TextArea] public string description;

    public ObjectiveStep[] steps;
}

[System.Serializable]
public class ObjectiveStep
{
    public string stepID;
    public string description;
    public int requiredAmount = 1;
}