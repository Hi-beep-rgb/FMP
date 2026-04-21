using UnityEngine;

public class MuteAudio : MonoBehaviour
{
    [SerializeField] AudioSource ForestAmbienceSource;
    [SerializeField] AudioSource musicSource;

    public void MuteAmbienceToggle(bool muted)
    {
        if(muted)
        {
            ForestAmbienceSource.volume = 0;
        }
        else
        {
            ForestAmbienceSource.volume = 1;
        }
    }
    public void MuteMusicToggle(bool muted)
    {
        if (muted)
        {
            musicSource.volume = 0;
        }
        else
        {
            musicSource.volume = 1;
        }
    }
}
