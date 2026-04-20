using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    public static AudioManager instance;

    public Sound[] sounds;

    [SerializeField] AudioMixer mixer;

    public const string MUSIC_KEY = "musicVolume";
    public const string SFX_KEY = "sfxVolume";
    public const string AMBIENCE_KEY = "ambienceVolume";

    void Awake()
    {
        // if instance is null, store a reference to this instance
        if (instance == null)
        {
            // a reference does not exist, so store it
            instance = this;
        }
        else
        {
            // Another instance of this gameobject has been made so destroy it
            // as we already have one
            Destroy(gameObject);
            return;
        }

        DontDestroyOnLoad(gameObject);


        foreach (Sound s in sounds)
        {
            s.source = gameObject.AddComponent<AudioSource>();
            s.source.clip = s.clip;
            s.source.volume = s.volume;
            s.source.pitch = s.pitch;
            s.source.loop = s.loop;
        }

        LoadVolume();
    }

    public void Play(string name)
    {
        Sound s = Array.Find(sounds, sound => sound.name == name);
        if (s == null)
        {
            Debug.LogWarning("Sound: " + name + " was not found.");
            return;
        }
        s.source.Play();
    }

    void LoadVolume() //Volume saved in VolumeSettings.cs
    {
        float musicVolume = PlayerPrefs.GetFloat(MUSIC_KEY, 1f);
        float sfxVolume = PlayerPrefs.GetFloat(SFX_KEY, 1f);
        float ambienceVolume = PlayerPrefs.GetFloat(AMBIENCE_KEY, 1f);

        mixer.SetFloat(VolumeSettings.MIXER_MUSIC, Mathf.Log10(musicVolume) * 20);
        mixer.SetFloat(VolumeSettings.MIXER_SFX, Mathf.Log10(sfxVolume) * 20);
        mixer.SetFloat(VolumeSettings.MIXER_AMBIENCE, Mathf.Log10(ambienceVolume) * 20);
    }
}