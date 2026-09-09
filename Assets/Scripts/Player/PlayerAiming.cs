using UnityEngine;
using UnityEngine.InputSystem;

namespace PickShoot
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerAiming : MonoBehaviour
    {
        Rigidbody2D rb;
        Camera cam;

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
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

            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            rb.MoveRotation(angle);
        }
    }
}
