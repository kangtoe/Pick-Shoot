using System.Collections.Generic;
using UnityEngine;

namespace PickShoot
{
    // 효과음 원샷 재생 단일 창구. 씬에 정확히 하나 존재해야 하며, AudioSource는 Awake에서 직접 만든다 (2D·비루프).
    // 같은 클립이 아주 짧은 간격으로 연달아 요청되면(다중 회수·다중 명중) 한 번만 재생해 볼륨 폭주를 막는다.
    // 클립 보관은 각 시스템(WeaponData·EnemyHealth 등)의 직렬화 필드가 담당하고, 이 클래스는 재생만 한다.
    public class SoundPlayer : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] float masterVolume = 0.8f;
        [SerializeField, Min(0f)] float sameClipMinInterval = 0.04f;

        static SoundPlayer instance;
        AudioSource source;
        readonly Dictionary<AudioClip, float> lastPlayTime = new Dictionary<AudioClip, float>();

        void Awake()
        {
            if (instance != null && instance != this)
                throw new System.Exception($"[PickShoot] SoundPlayer가 씬에 둘 이상 존재 ({instance.name}, {name}).");
            instance = this;
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        // volumeScale: 아주 자주 울리는 효과음(탄환 튕김 등)을 호출자가 masterVolume 대비 낮춰 재생할 때 쓴다.
        public static void Play(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null)
                throw new System.Exception("[PickShoot] SoundPlayer.Play: clip이 null. 호출자가 클립 할당 여부를 먼저 확인해야 합니다.");
            if (instance == null)
                throw new System.Exception("[PickShoot] 씬에 SoundPlayer가 없습니다. 효과음 재생용 SoundPlayer 오브젝트 필요.");
            instance.PlayInternal(clip, volumeScale);
        }

        void PlayInternal(AudioClip clip, float volumeScale)
        {
            // 업그레이드 UI(timeScale=0) 중에도 재생돼야 하므로 unscaledTime 기준.
            float now = Time.unscaledTime;
            if (lastPlayTime.TryGetValue(clip, out float last) && now - last < sameClipMinInterval) return;
            lastPlayTime[clip] = now;
            source.PlayOneShot(clip, masterVolume * volumeScale);
        }
    }
}
