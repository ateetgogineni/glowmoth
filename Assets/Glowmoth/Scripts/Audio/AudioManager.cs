using UnityEngine;

namespace Glowmoth
{
    /// <summary>
    /// A fixed set of AudioSources used round-robin (a voice pool), plus one looping music source.
    /// No AudioSources are created after startup.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        // Each world plays the same loop in a different key.
        static readonly float[] WorldPitch = { 1f, 0.8909f, 0.9439f, 1.1225f };

        AudioSource[] voices;
        AudioSource music;
        int nextVoice;
        int musicWorld;

        public static AudioManager Create(Transform parent)
        {
            var go = new GameObject("Audio");
            go.transform.SetParent(parent, false);
            Instance = go.AddComponent<AudioManager>();
            Instance.Init();
            return Instance;
        }

        void Init()
        {
            voices = new AudioSource[6];
            for (int i = 0; i < voices.Length; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                voices[i] = s;
            }

            music = gameObject.AddComponent<AudioSource>();
            music.clip = SfxSynth.Music;
            music.loop = true;
            music.volume = 0.32f;
            music.playOnAwake = false;
        }

        public void Play(AudioClip clip, float volume = 1f, float pitchJitter = 0.05f)
        {
            if (clip == null || !SaveSystem.Sound) return;
            AudioSource s = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;
            s.clip = clip;
            s.volume = volume;
            s.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            s.Play();
        }

        public void PlayMusic(int world)
        {
            musicWorld = Mathf.Clamp(world, 0, WorldPitch.Length - 1);
            music.pitch = WorldPitch[musicWorld];
            if (SaveSystem.Music)
            {
                if (!music.isPlaying) music.Play();
            }
            else
            {
                music.Stop();
            }
        }

        public void RefreshMusic()
        {
            PlayMusic(musicWorld);
        }
    }
}
