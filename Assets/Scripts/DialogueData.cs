using UnityEngine;

[System.Serializable]
public class DialogueLine
{
    public string speaker;
    public Sprite portrait; // optional
    [TextArea(2, 5)] public string text;
}

[CreateAssetMenu(fileName = "NewDialogue", menuName = "Dialogue/Dialogue Data")]
public class DialogueData : ScriptableObject
{
    public DialogueLine[] lines;
}