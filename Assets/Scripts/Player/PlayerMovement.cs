using UnityEngine;
using UnityEngine.InputSystem;

namespace PickShoot
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerMovement : MonoBehaviour
    {
        [SerializeField] float moveSpeed = 6f;
        [SerializeField] float acceleration = 40f;
        [SerializeField] float friction = 15f;
        [SerializeField, Range(0f, 1f)] float wallBounceRetention = 0.55f;

        Rigidbody2D rb;
        Vector2 input;

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null)
            {
                input = Vector2.zero;
                return;
            }

            float x = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
            float y = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
            input = new Vector2(x, y);
            if (input.sqrMagnitude > 1f) input.Normalize();
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
