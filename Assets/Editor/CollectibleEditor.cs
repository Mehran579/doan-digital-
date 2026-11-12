using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(collectible))]
public class CollectibleEditor : Editor
{
    private SerializedProperty promptSpriteProp;
    private SerializedProperty promptWorldScaleProp;
    private SerializedProperty promptPlayerOffsetProp;
    private SerializedProperty promptSortingOrderProp;

    private SerializedProperty popDurationProp;
    private SerializedProperty popOvershootProp;
    private SerializedProperty floatAmplitudeProp;
    private SerializedProperty floatFrequencyProp;

    private SerializedProperty playerTagProp;
    private SerializedProperty destroyOnCollectProp;
    private SerializedProperty disableOnCollectProp;

    private SerializedProperty objectiveActionProp;
    private SerializedProperty targetObjectiveProp;
    private SerializedProperty targetStepIDProp;
    private SerializedProperty progressAmountProp;

    private void OnEnable()
    {
        promptSpriteProp = serializedObject.FindProperty("promptSprite");
        promptWorldScaleProp = serializedObject.FindProperty("promptWorldScale");
        promptPlayerOffsetProp = serializedObject.FindProperty("promptPlayerOffset");
        promptSortingOrderProp = serializedObject.FindProperty("promptSortingOrder");

        popDurationProp = serializedObject.FindProperty("popDuration");
        popOvershootProp = serializedObject.FindProperty("popOvershoot");
        floatAmplitudeProp = serializedObject.FindProperty("floatAmplitude");
        floatFrequencyProp = serializedObject.FindProperty("floatFrequency");

        playerTagProp = serializedObject.FindProperty("playerTag");
        destroyOnCollectProp = serializedObject.FindProperty("destroyOnCollect");
        disableOnCollectProp = serializedObject.FindProperty("disableOnCollect");

        objectiveActionProp = serializedObject.FindProperty("objectiveAction");
        targetObjectiveProp = serializedObject.FindProperty("targetObjective");
        targetStepIDProp = serializedObject.FindProperty("targetStepID");
        progressAmountProp = serializedObject.FindProperty("progressAmount");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // 1. Prompt Visuals
        EditorGUILayout.LabelField("Prompt Visuals", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(promptSpriteProp);
        EditorGUILayout.PropertyField(promptWorldScaleProp);
        EditorGUILayout.PropertyField(promptPlayerOffsetProp);
        EditorGUILayout.PropertyField(promptSortingOrderProp);

        EditorGUILayout.Space(6);

        // 2. Animation & Floating
        EditorGUILayout.LabelField("Animation & Floating", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(popDurationProp);
        EditorGUILayout.PropertyField(popOvershootProp);
        EditorGUILayout.PropertyField(floatAmplitudeProp);
        EditorGUILayout.PropertyField(floatFrequencyProp);

        EditorGUILayout.Space(6);

        // 3. Interaction Settings
        EditorGUILayout.LabelField("Interaction Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(playerTagProp);
        EditorGUILayout.PropertyField(destroyOnCollectProp);
        EditorGUILayout.PropertyField(disableOnCollectProp);

        EditorGUILayout.Space(6);

        // 4. Objective Settings
        EditorGUILayout.LabelField("Objective Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(objectiveActionProp);
        EditorGUILayout.PropertyField(targetObjectiveProp);

        int actionValue = objectiveActionProp.enumValueIndex;
        // 0 = AddProgress, 1 = StartNewObjective, 2 = StartAndAddProgress
        bool needsStep = (actionValue == 0 || actionValue == 2);

        if (needsStep)
        {
            ObjectiveData objData = targetObjectiveProp.objectReferenceValue as ObjectiveData;

            if (objData != null && objData.steps != null && objData.steps.Length > 0)
            {
                List<string> options = new List<string>();
                List<string> stepIDs = new List<string>();

                int selectedIndex = 0;
                string currentID = targetStepIDProp.stringValue;

                for (int i = 0; i < objData.steps.Length; i++)
                {
                    var s = objData.steps[i];
                    string id = s.stepID;
                    string label = string.IsNullOrEmpty(s.description) ? id : $"{id}: \"{s.description}\"";
                    options.Add(label);
                    stepIDs.Add(id);

                    if (id == currentID)
                    {
                        selectedIndex = i;
                    }
                }

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"Steps from [{objData.name}]", EditorStyles.miniBoldLabel);

                int newIndex = EditorGUILayout.Popup("Select Step ID", selectedIndex, options.ToArray());
                if (newIndex >= 0 && newIndex < stepIDs.Count)
                {
                    targetStepIDProp.stringValue = stepIDs[newIndex];
                }

                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.TextField("Selected Step ID", targetStepIDProp.stringValue);
                }
                EditorGUILayout.EndVertical();
            }
            else
            {
                if (objData == null)
                {
                    EditorGUILayout.HelpBox("Assign an ObjectiveData to Target Objective to pick its steps from a dropdown.", MessageType.Info);
                }
                else
                {
                    EditorGUILayout.HelpBox($"Objective '{objData.name}' has no steps in its steps array.", MessageType.Warning);
                }

                EditorGUILayout.PropertyField(targetStepIDProp);
            }

            EditorGUILayout.PropertyField(progressAmountProp);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
