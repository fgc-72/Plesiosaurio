using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class ListenableSound : MonoBehaviour
{
    public static readonly List<ListenableSound> All = new List<ListenableSound>();

    public AudioSource Source { get; private set; }

    private void Awake()
    {
        Source = GetComponent<AudioSource>();
    }

    private void OnEnable()
    {
        All.Add(this);
    }

    private void OnDisable()
    {
        All.Remove(this);
    }
}