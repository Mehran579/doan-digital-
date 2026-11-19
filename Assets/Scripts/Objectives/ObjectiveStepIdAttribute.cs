using UnityEngine;

/// <summary>Draws a string field as a dropdown of an ObjectiveData asset's step IDs.</summary>
public sealed class ObjectiveStepIdAttribute : PropertyAttribute
{
    public readonly string objectiveFieldName;
    public readonly string fallbackObjectiveFieldName;

    public ObjectiveStepIdAttribute(string objectiveFieldName, string fallbackObjectiveFieldName = null)
    {
        this.objectiveFieldName = objectiveFieldName;
        this.fallbackObjectiveFieldName = fallbackObjectiveFieldName;
    }
}
