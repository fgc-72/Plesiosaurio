using UnityEngine;

public class LogDisable : MonoBehaviour
{
    private void OnEnable()  { Debug.Log($"{name} ACTIVADO", this); }
    private void OnDisable() { Debug.Log($"{name} DESACTIVADO\n{System.Environment.StackTrace}", this); }
}