using System;
using UnityEngine;

/// <summary>
/// Modelo de datos serializable en JSON para el guardado de la partida.
/// Guarda la escena actual, la posición y rotación de Nami y Mara,
/// habilidades desbloqueadas, estado de misiones y progreso de rocas.
/// </summary>
[Serializable]
public class SaveData
{
    public int version = 1;
    public string saveTimestamp;        // Fecha y hora legible (ej: 2026-10-30 12:45)
    public string sceneName;            // Nombre de la escena donde se guardó (ej: "SampleScene", "acto2")
    public string savepointName;        // Identificador del checkpoint o "ManualSave"

    [Header("Jugador (Nami)")]
    public Vector3 namiPosition;
    public Vector3 namiEulerAngles;

    [Header("Mara")]
    public bool hasMara;
    public Vector3 maraPosition;
    public Vector3 maraEulerAngles;

    [Header("Habilidades Desbloqueadas")]
    public bool canMoveVertical;
    public bool canSprint;
    public bool canHeadbutt;
    public bool canListen;

    [Header("Misiones y Progreso")]
    public string currentMissionId;
    public int brokenRocksCount;
}
