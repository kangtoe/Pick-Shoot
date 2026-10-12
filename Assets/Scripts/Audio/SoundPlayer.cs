using System.Collections.Generic;
using UnityEngine;

namespace PickShoot
{
    // 효과음 원샷 재생 단일 창구. 씬에 정확히 하나 존재해야 하며, AudioSource는 Awake에서 직접 만든다 (2D·비루프).
    // 같은 클립이 겹쳐 볼륨이 폭주하지 않게 세 겹으로 누른다 — 아주 짧은 간격의 연속 요청(다중 회수·다중 명중)은 한 번만,
    // 아직 울리고 있는 같은 클립은 maxVoicesPerClip개까지만, 그 안에서도 이미 울리는 개수만큼 작게 낸다.
    // 클립 보관은 각 시스템(WeaponData·EnemyHealth 등)의 직렬화 필드가 담당하고, 이 클래스는 재생만 한다.
    public class SoundPlayer : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] float masterVolume = 1f;
        [SerializeField, Min(0f)] float sameClipMinInterval = 0.04f;
        // 같은 클립이 동시에 울릴 수 있는 최대 개수. 다 차 있으면 새 요청은 건너뛴다 — 긴 폭발음이 연달아 쌓이는 것을 막는다.
        [SerializeField, Min(1)] int maxVoicesPerClip = 3;
        // 같은 클립이 이미 울리고 있을 때 새 소리에 곱하는 볼륨 배율. 울리는 개수만큼 거듭 곱한다 (두 번째 0.6, 세 번째 0.36).
        [SerializeField, Range(0f, 1f)] float overlapVolumeFactor = 0.6f;

        static SoundPlayer instance;
        AudioSource source;
        // 클립별로 아직 울리고 있는 재생의 시작 시각 (오래된 순).
        readonly Dictionary<AudioClip, List<float>> playingStarts = new Dictionary<AudioClip, List<float>>();

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
            // 업그레이드 UI(timeScale=0) 중에도 재생돼야 하므로 unscaledTime 기준. 소리도 timeScale과 무관하게 흐른다.
            float now = Time.unscaledTime;
            if (!playingStarts.TryGetValue(clip, out var starts))
            {
                starts = new List<float>();
                playingStarts[clip] = starts;
            }
            // 같은 클립이라 길이가 같으므로 먼저 시작한 것부터 끝난다.
            while (starts.Count > 0 && now - starts[0] >= clip.length) starts.RemoveAt(0);
            if (starts.Count > 0 && now - starts[starts.Count - 1] < sameClipMinInterval) return;
            if (starts.Count >= maxVoicesPerClip) return;
            float overlapScale = Mathf.Pow(overlapVolumeFactor, starts.Count);
            starts.Add(now);
            source.PlayOneShot(clip, masterVolume * volumeScale * overlapScale);
        }
    }
}
