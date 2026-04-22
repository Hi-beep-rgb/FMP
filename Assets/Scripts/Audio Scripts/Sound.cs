using UnityEngine.Audio;
using UnityEngine;
using Unity.VisualScripting.InputSystem;

[System.Serializable]
public class Sound
{
    public string name;

    public AudioClip clip;
    public AudioPlayableOutput output;

    [Range(0f, 1f)]
    public float volume;
    [Range(0.1f, 3f)]
    public float pitch;


    public bool loop;

    /*[HideInInspector]*/
    public AudioSource source;
    public AudioMixerGroup type;

}