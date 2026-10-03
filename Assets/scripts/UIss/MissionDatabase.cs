using UnityEngine;

[CreateAssetMenu(fileName = "MissionDatabase", menuName = "Nami/Mission Database")]
public class MissionDatabase : ScriptableObject
{
    public MissionEntry[] missions;

    public MissionEntry GetById(string id)
    {
        foreach (var m in missions)
            if (m.id == id) return m;

        Debug.LogWarning($"No se encontró la misión '{id}'");
        return null;
    }
}