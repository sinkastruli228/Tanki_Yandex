using UnityEngine;

[DisallowMultipleComponent]
public sealed class SceneAudioController : MonoBehaviour
{
    [SerializeField] private AudioSource ambientSource;
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioClip ambientClip;
    [SerializeField] private AudioClip musicClip;
    [SerializeField] private float ambientVolume = 0.38f;
    [SerializeField] private float musicVolume = 0.34f;

    public void Configure(AudioClip ambient, AudioClip music)
    {
        ambientClip = ambient;
        musicClip = music;

        ambientSource = ConfigureLoopSource("Ambient Loop", ambientSource, ambientClip, ambientVolume);
        musicSource = ConfigureLoopSource("Music Ambient Loop", musicSource, musicClip, musicVolume);
    }

    private AudioSource ConfigureLoopSource(string objectName, AudioSource source, AudioClip clip, float volume)
    {
        if (clip == null)
        {
            if (source != null)
            {
                source.Stop();
            }

            return source;
        }

        if (source == null)
        {
            Transform existing = transform.Find(objectName);
            GameObject sourceObject = existing != null ? existing.gameObject : new GameObject(objectName);
            sourceObject.transform.SetParent(transform, false);
            source = sourceObject.GetComponent<AudioSource>();
            if (source == null)
            {
                source = sourceObject.AddComponent<AudioSource>();
            }
        }

        source.clip = clip;
        source.loop = true;
        source.playOnAwake = false;
        source.volume = volume;
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;

        if (Application.isPlaying && !source.isPlaying)
        {
            source.Play();
        }
        else if (!Application.isPlaying && source.isPlaying)
        {
            source.Stop();
        }

        return source;
    }
}
