using UnityEngine;

namespace PickShoot
{
    // 이동 중 걷기 프레임(시트의 연속 2장: 정지 포즈·걷기 포즈)을 번갈아 보여주는 스프라이트 애니메이션.
    // 프레임 전환은 시간이 아니라 이동 거리로 구동해 빠른 캐릭터는 빠르게, 느린 캐릭터는 느리게 걷는다.
    // 멈추면 첫 프레임(정지 포즈)으로 돌아간다. 좌우 반전(flipX)·머티리얼(피격 플래시)은 건드리지 않는다.
    // 걷기는 스스로 추진 중일 때만 재생한다(Propelled). 입력을 놓고 관성으로 미끄러지는 동안은 정지 포즈다.
    // Animator·AnimationClip 대신 이 컴포넌트 하나로 처리하며, 프레임 목록은 프리팹(적)이 보관한다. 플레이어는 씬에서 비워 두고 러닝 시작에 고른 캐릭터의
    // 프레임(CharacterData.Frames, 4장 주기)을 SetFrames로 받는다 (Characters §5) — 고르기 전에 게임이 움직이면 예외.
    // 포즈를 직접 정하는 동안(돌진형의 준비·돌진·회복)은 이 컴포넌트를 끈다. 다시 켜지면 기준 위치를 새로 잡는다.
    [RequireComponent(typeof(SpriteRenderer))]
    public class SpriteWalkCycle : MonoBehaviour
    {
        [SerializeField] Sprite[] frames;
        [SerializeField, Min(0.01f)] float strideLength = 0.5f; // 프레임 한 장당 이동 거리 (월드 유닛)
        [SerializeField, Min(0f)] float idleSpeed = 0.3f;       // 이 속도 미만이면 정지로 간주

        SpriteRenderer sr;
        Vector2 lastPosition;
        float traveled;

        // 스스로 추진 중인지. 입력이 있을 때만 걷는 플레이어가 매 프레임 갱신하고,
        // 추적 중 항상 걷는 적은 기본값(true)을 그대로 둔다.
        public bool Propelled { get; set; } = true;
        // 정지 포즈. 이 컴포넌트를 끄고 포즈를 직접 정하는 쪽이 돌아갈 그림으로 쓴다.
        public Sprite IdleFrame => frames[0];

        void Awake()
        {
            // 비어 있으면 SetFrames를 기다리는 껍데기(플레이어). 한 장이라도 들어 있으면 온전한 주기여야 한다.
            if (frames != null && frames.Length > 0) RequireFrames(frames);
            sr = GetComponent<SpriteRenderer>();
        }

        // 프레임 주기를 바꾼다 — 캐릭터를 고를 때 (UpgradeManager). 멈춘 화면에서 부르므로 정지 포즈를 바로 보인다 (LateUpdate는 deltaTime 0이면 그리지 않는다).
        public void SetFrames(Sprite[] newFrames)
        {
            RequireFrames(newFrames);
            frames = newFrames;
            traveled = 0f;
            sr.sprite = frames[0];
        }

        void RequireFrames(Sprite[] f)
        {
            if (f == null || f.Length < 2)
                throw new System.Exception($"[PickShoot] SpriteWalkCycle.frames는 2장 이상이어야 합니다 ({name}).");
            for (int i = 0; i < f.Length; i++)
                if (f[i] == null)
                    throw new System.Exception($"[PickShoot] SpriteWalkCycle.frames[{i}] 미설정 ({name}).");
        }

        // 꺼져 있던 동안 옮겨진 거리를 걷기로 세지 않도록 기준 위치를 다시 잡는다. 첫 활성화 때도 여기서 잡는다.
        void OnEnable()
        {
            lastPosition = transform.position;
            traveled = 0f;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Vector2 pos = transform.position;
            float moved = Vector2.Distance(pos, lastPosition);
            lastPosition = pos;

            // 프레임이 비어 있으면(플레이어, 캐릭터를 고르기 전) 그릴 것이 없다. 시작 화면이 게임을 멈추는 첫 프레임은 deltaTime이 이미 양수로 정해진 뒤라 여기까지
            // 오므로, 제자리에 있는 한 넘어가고 그 상태로 실제로 움직였을 때만 예외 — 캐릭터 없이 게임이 돈 것이다.
            if (frames == null || frames.Length == 0)
            {
                if (moved > 0.0001f)
                    throw new System.Exception($"[PickShoot] SpriteWalkCycle: 프레임 미설정인데 움직임 ({name}). 플레이어는 러닝 시작 화면에서 캐릭터를 고른 뒤에만 움직여야 함 (Characters §5).");
                return;
            }

            if (!Propelled || moved / dt < idleSpeed)
            {
                traveled = 0f;
                sr.sprite = frames[0];
                return;
            }

            traveled = (traveled + moved) % (strideLength * frames.Length);
            sr.sprite = frames[(int)(traveled / strideLength) % frames.Length];
        }
    }
}
