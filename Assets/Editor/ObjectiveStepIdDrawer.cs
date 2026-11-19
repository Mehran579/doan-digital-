using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(ObjectiveStepIdAttribute))]
public sealed class ObjectiveStepIdDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        ObjectiveStepIdAttribute stepAttribute = (ObjectiveStepIdAttribute)attribute;
        ObjectiveData objective = GetSiblingObjectReference(property, stepAttribute.objectiveFieldName);
        if (objective == null && !string.IsNullOrEmpty(stepAttribute.fallbackObjectiveFieldName))
            objective = GetSiblingObjectReference(property, stepAttribute.fallbackObjectiveFieldName);

        if (objective == null || objective.steps == null)
        {
            using (new EditorGUI.DisabledScope(true))
                EditorGUI.Popup(position, label.text, 0, new[] { "Assign an objective first" });
            return;
        }

        List<string> ids = new() { string.Empty };
        List<string> options = new() { "None" };
        int selected = 0;

        for (int i = 0; i < objective.steps.Length; i++)
        {
            ObjectiveStep step = objective.steps[i];
            if (step == null || string.IsNullOrEmpty(step.stepID))
                continue;

            ids.Add(step.stepID);
            string description = string.IsNullOrEmpty(step.description) ? step.stepID : step.description;
            options.Add($"{description} ({step.stepID})");
            if (step.stepID == property.stringValue)
                selected = ids.Count - 1;
        }

        if (!string.IsNullOrEmpty(property.stringValue) && selected == 0)
        {
            ids.Insert(1, property.stringValue);
            options.Insert(1, $"Missing step ({property.stringValue})");
            selected = 1;
        }

        EditorGUI.BeginProperty(position, label, property);
        int next = EditorGUI.Popup(position, label.text, selected, options.ToArray());
        if (next >= 0 && next < ids.Count)
            property.stringValue = ids[next];
        EditorGUI.EndProperty();
    }

    private static ObjectiveData GetSiblingObjectReference(SerializedProperty property, string siblingName)
    {
        string propertyPath = property.propertyPath;
        int lastSeparator = propertyPath.LastIndexOf('.');
        string parentPath = lastSeparator >= 0 ? propertyPath.Substring(0, lastSeparator + 1) : string.Empty;
        SerializedProperty sibling = property.serializedObject.FindProperty(parentPath + siblingName);
        if (sibling == null)
            sibling = property.serializedObject.FindProperty(siblingName);
        return sibling != null ? sibling.objectReferenceValue as ObjectiveData : null;
    }
}
