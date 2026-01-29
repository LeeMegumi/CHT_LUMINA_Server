using UnityEngine;

public class Lumina_Custom_Audio : MonoBehaviour
{
    public AudioSource uLipsync_Audiosource;
    public AudioSource customAudiosource;

    public AudioClip[] LuminaAudioClips;
    public void PlayCustomAudio(AudioClip clip)
    {
        customAudiosource.clip = clip;
        customAudiosource.Play();
        uLipsync_Audiosource.clip = clip;
        uLipsync_Audiosource.Play();
        uLipsync_Audiosource.loop = false;
    }
}
