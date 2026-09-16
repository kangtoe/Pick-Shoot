using UnityEngine;
using UnityEngine.InputSystem;

namespace PickShoot
{
    // WASD 가속·마찰 이동. 걷기 애니메이션은 이동 입력이 있는 동안만 재생하고,
    // 입력을 놓은 뒤 관성으로 미끄러지는 동안은 정지 포즈를 유지한다.
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(SpriteWalkCycle))]
    public class PlayerMovement : MonoBehaviour
    {
        [SerializeField] float moveSpeed = 6f;
        [SerializeField] float acceleration = 40f;
        [SerializeField] float friction = 15f;
        [SerializeField, Range(0f, 1f)] float wallBounceRetention = 0.55f;

        Rigidbody2D rb;
        SpriteWalkCycle walkCycle;
        Vector2 input;

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            walkCycle = GetComponent<SpriteWalkCycle>();
        }

        void Update()
        {
            input = ReadInput();
            walkCycle.Propelled = input.sqrMagnitude > 0.0001f;
        }

        static Vector2 ReadInput()
        {
            var kb = Keyboard.current;
            if (kb == null) return Vector2.zero;

            float x = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
            float y = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
            var v = new Vector2(x, y);
            return v.sqrMagnitude > 1f ? v.normalized : v;
        }

        void FixedUpdate()
        {
            var target = input * moveSpeed;
            float rate = input.sqrMagnitude > 0.0001f ? acceleration : friction;
            rb.linearVelocity = Vector2.MoveTowards(rb.linearVelocity, target, rate * Time.fixedDeltaTime);
        }

        void OnCollisionEnter2D(Collision2D _)
        {
            rb.linearVelocity *= wallBounceRetention;
        }
    }
}
