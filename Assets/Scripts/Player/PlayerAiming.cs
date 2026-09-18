using UnityEngine;
using UnityEngine.InputSystem;

namespace PickShoot
{
    // 마우스 조준. 탑다운 픽셀아트 캐릭터는 회전시키지 않고 조준각만 노출하며,
    // 왼쪽을 조준하면 스프라이트를 좌우 반전한다.
    // 무기 링(PlayerWeapons)은 AimAngleDeg를 읽어 회전하고, 각 무기는 AimWorldPosition(커서 월드 좌표)을 제 위치에서 조준한다.
    // 플레이어 Rigidbody2D는 회전 고정(Freeze Rotation)이어야 충돌로 몸체가 돌지 않는다.
    [RequireComponent(typeof(SpriteRenderer))]
    public class PlayerAiming : MonoBehaviour
    {
        Camera cam;
        SpriteRenderer sr;

        public float AimAngleDeg { get; private set; }
        public Vector2 AimDirection { get; private set; } = Vector2.right;
        // 커서의 월드 좌표. 첫 Update 전에는 플레이어 오른쪽 1유닛으로 두어 기본 조준각(0°)과 맞춘다.
        public Vector2 AimWorldPosition { get; private set; }

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            cam = Camera.main;
            if (cam == null)
                throw new System.Exception("[PickShoot] MainCamera가 없습니다. 씬에 MainCamera 태그 카메라 필요.");
            AimWorldPosition = (Vector2)transform.position + Vector2.right;
        }

        void Update()
        {
            // 게임이 멈춘 동안(일시정지·카드 선택, timeScale 0)은 조준을 갱신하지 않는다 — 하드웨어 커서로 UI를 조작하는 동안
            // 무기가 커서를 따라 도는 것을 막는다. 사격(PlayerWeapons)과 같은 기준.
            if (Time.timeScale <= 0f) return;
            var mouse = Mouse.current;
            if (mouse == null) return;

            Vector3 screen = mouse.position.ReadValue();
            screen.z = -cam.transform.position.z;
            Vector3 world = cam.ScreenToWorldPoint(screen);
            AimWorldPosition = world;

            Vector2 dir = (Vector2)(world - transform.position);
            // 커서가 플레이어 바로 위면 방향이 정의되지 않으므로 직전 조준각을 유지한다.
            if (dir.sqrMagnitude < 0.0001f) return;
            dir.Normalize();

            AimDirection = dir;
            AimAngleDeg = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            sr.flipX = dir.x < 0f;
        }
    }
}
