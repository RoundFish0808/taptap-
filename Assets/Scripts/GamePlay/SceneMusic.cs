using UnityEngine;

namespace ReverseDefense
{
    // Scene-owned audio: unloading the scene also stops and destroys its music.
    [DisallowMultipleComponent, RequireComponent(typeof(AudioSource))]
    public sealed class SceneMusic : MonoBehaviour
    {
        public static bool Muted { get; private set; }
        private static event System.Action<bool> MuteChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetMusicSetting()
        {
            Muted = false;
            MuteChanged = null;
        }

        public static void ToggleMute()
        {
            Muted = !Muted;
            MuteChanged?.Invoke(Muted);
        }

        public AudioClip Music;
        [Range(0f, 1f)] public float Volume = 0.5f;
        private AudioSource source;

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.clip = Music;
            source.volume = Volume;
        }

        private void OnEnable()
        {
            MuteChanged += ApplyMute;
            ApplyMute(Muted);
            if (Music == null)
            {
                Debug.LogWarning("场景音乐：请在 Music 中拖入本场景的音频文件。", this);
                return;
            }
            source.Play();
        }

        private void OnDisable()
        {
            MuteChanged -= ApplyMute;
            if (source != null) source.Stop();
        }

        private void ApplyMute(bool muted)
        {
            if (source != null) source.mute = muted;
        }

        private void OnValidate()
        {
            if (source != null) source.volume = Volume;
        }
    }
}
