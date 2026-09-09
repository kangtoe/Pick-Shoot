using UnityEngine;
using UnityEngine.InputSystem;

namespace PickShoot
{
    public class PlayerShooter : MonoBehaviour
    {
        [SerializeField] Bullet bulletPrefab;
        [SerializeField] Transform muzzle;
        [SerializeField] PlayerAmmo ammo;
        [SerializeField] float bulletSpeed = 14f;
        [SerializeField] float fireCooldown = 0.2f;

        Collider2D myCollider;
        float nextFireTime;

        void Awake()
        {
            myCollider = GetComponent<Collider2D>();
            if (myCollider == null)
                throw new System.Exception("[PickShoot] PlayerShooter는 같은 GameObject의 Collider2D를 요구합니다.");
            if (ammo == null)
                throw new System.Exception("[PickShoot] PlayerShooter.ammo 미할당.");
        }

        void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;
            if (!mouse.leftButton.isPressed) return;
            if (Time.time < nextFireTime) return;
            if (!ammo.HasAny) return;

            Fire();
            nextFireTime = Time.time + fireCooldown;
        }

        void Fire()
        {
            if (bulletPrefab == null)
                throw new System.Exception("[PickShoot] bulletPrefab 미할당.");
            if (!ammo.TryConsume()) return;

            Vector3 pos = muzzle != null ? muzzle.position : transform.position;
            Vector2 dir = transform.right;

            var bullet = Instantiate(bulletPrefab, pos, Quaternion.identity);
            var bulletCol = bullet.GetComponent<Collider2D>();
            if (bulletCol == null)
                throw new System.Exception("[PickShoot] Bullet prefab에 Collider2D가 없습니다.");
            Physics2D.IgnoreCollision(myCollider, bulletCol, true);
            bullet.Launch(dir * bulletSpeed);
        }
    }
}
