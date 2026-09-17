using UnityEngine;

namespace PickShoot
{
    // 플레이어를 부드럽게 따라가는 카메라. 아레나(원점 중심) 바깥이 화면을 차지하지 않도록
    // 시야가 viewLimitHalfExtents를 넘지 않는 범위에서만 움직인다.
    // 줌은 Camera 컴포넌트의 직교 크기를 그대로 쓴다.
    [RequireComponent(typeof(Camera))]
    public class PlayerFollowCamera : MonoBehaviour
    {
        [SerializeField] Transform target;
        // 목표 위치에 따라붙는 데 걸리는 대략적인 시간. 작을수록 바짝 붙는다.
        [SerializeField, Min(0f)] float smoothTime = 0.15f;
        // 시야가 닿아도 되는 범위(원점 기준 반폭). 울타리 바깥 면(15)에 여유 한 칸.
        // ArenaDecor가 땅을 까는 범위 안쪽이어야 빈 배경이 보이지 않는다.
        [SerializeField] Vector2 viewLimitHalfExtents = new Vector2(16f, 16f);

        Camera cam;
        Vector2 velocity;

        void Awake()
        {
            cam = GetComponent<Camera>();
            if (target == null)
                throw new System.Exception("[PickShoot] PlayerFollowCamera.target 미설정.");
            if (!cam.orthographic)
                throw new System.Exception("[PickShoot] PlayerFollowCamera는 직교 카메라 전용 (시야 크기를 orthographicSize로 계산).");
        }

        void Start()
        {
            MoveTo(ClampToViewLimit(target.position));
        }

        void LateUpdate()
        {
            // 플레이어가 죽어 파괴되면 마지막 자리에 머문다.
            if (target == null) return;

            Vector2 goal = ClampToViewLimit(target.position);
            MoveTo(Vector2.SmoothDamp(transform.position, goal, ref velocity, smoothTime));
        }

        // 시야가 한계보다 넓은 축(16:9 화면의 가로)은 움직일 여지가 없어 아레나 중앙에 고정된다.
        Vector2 ClampToViewLimit(Vector2 center)
        {
            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;
            float rangeX = Mathf.Max(0f, viewLimitHalfExtents.x - halfWidth);
            float rangeY = Mathf.Max(0f, viewLimitHalfExtents.y - halfHeight);
            return new Vector2(Mathf.Clamp(center.x, -rangeX, rangeX), Mathf.Clamp(center.y, -rangeY, rangeY));
        }

        void MoveTo(Vector2 center)
        {
            transform.position = new Vector3(center.x, center.y, transform.position.z);
        }
    }
}
