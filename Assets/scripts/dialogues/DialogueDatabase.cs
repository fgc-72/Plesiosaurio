using UnityEngine;

[CreateAssetMenu(fileName = "DialogueDatabase", menuName = "Nami/Dialogue Database")]
public class DialogueDatabase : ScriptableObject
{
    public DialogueLine[] lines;

    public DialogueLine GetById(string id)
    {
        foreach (var line in lines)
        {
            if (line.id == id) return line;
        }

        Debug.LogWarning($"No dialogue line '{id}'");
        return null;
    }
}