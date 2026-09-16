using UnityEngine;

namespace PickShoot
{
    // 이동 중 걷기 프레임(시트의 연속 2장: 정지 포즈·걷기 포즈)을 번갈아 보여주는 스프라이트 애니메이션.
    // 프레임 전환은 시간이 아니라 이동 거리로 구동해 빠른 캐릭터는 빠르게, 느린 캐릭터는 느리게 걷는다.
    // 멈추면 첫 프레임(정지 포즈)으로 돌아간다. 좌우 반전(flipX)·색(피격 플래시)은 건드리지 않는다.
    // 걷기는 스스로 추진 중일 때만 재생한다(Propelled). 입력을 놓고 관성으로 미끄러지는 동안은 정지 포즈다.
    // Animator·AnimationClip 대신 이 컴포넌트 하나로 처리하며, 프레임 목록은 프리팹(플레이어는 씬)이 보관한다.
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

        void Awake()
        {
            if (frames == null || frames.Length < 2)
                throw new System.Exception($"[PickShoot] SpriteWalkCycle.frames는 2장 이상이어야 합니다 ({name}).");
            for (int i = 0; i < frames.Length; i++)
                if (frames[i] == null)
                    throw new System.Exception($"[PickShoot] SpriteWalkCycle.frames[{i}] 미설정 ({name}).");

            sr = GetComponent<SpriteRenderer>();
            lastPosition = transform.position;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Vector2 pos = transform.position;
            float moved = Vector2.Distance(pos, lastPosition);
            lastPosition = pos;

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
