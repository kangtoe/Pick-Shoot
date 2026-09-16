using UnityEngine;
using UnityEngine.InputSystem;

namespace PickShoot
{
    // 마우스 조준. 탑다운 픽셀아트 캐릭터는 회전시키지 않고 조준각만 노출하며,
    // 왼쪽을 조준하면 스프라이트를 좌우 반전한다. 무기 링(PlayerWeapons)이 AimAngleDeg를 읽어 회전한다.
    // 플레이어 Rigidbody2D는 회전 고정(Freeze Rotation)이어야 충돌로 몸체가 돌지 않는다.
    [RequireComponent(typeof(SpriteRenderer))]
    public class PlayerAiming : MonoBehaviour
    {
        Camera cam;
        SpriteRenderer sr;

        public float AimAngleDeg { get; private set; }
        public Vector2 AimDirection { get; private set; } = Vector2.right;

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            cam = Camera.main;
            if (cam == null)
                throw new System.Exception("[PickShoot] MainCamera가 없습니다. 씬에 MainCamera 태그 카메라 필요.");
        }

        void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            Vector3 screen = mouse.position.ReadValue();
            screen.z = -cam.transform.position.z;
            Vector3 world = cam.ScreenToWorldPoint(screen);

            Vector2 dir = (Vector2)(world - transform.position);
            if (dir.sqrMagnitude < 0.0001f) return;
            dir.Normalize();

            AimDirection = dir;
            AimAngleDeg = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            sr.flipX = dir.x < 0f;
        }
    }
}
