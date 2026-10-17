using UnityEngine;

public class PlayerHiding : MonoBehaviour
{
    int zonesInside;
    public bool IsHidden => zonesInside > 0;

    public void Enter() => zonesInside++;
    public void Exit() => zonesInside = Mathf.Max(0, zonesInside - 1);
}