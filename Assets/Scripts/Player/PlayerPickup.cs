using UnityEngine;

namespace PickShoot
{
    public class PlayerPickup : MonoBehaviour
    {
        [SerializeField] PlayerAmmo ammo;
        [SerializeField] float pickupRadius = 0.9f;

        readonly Collider2D[] hits = new Collider2D[16];

        void Awake()
        {
            if (ammo == null)
                throw new System.Exception("[PickShoot] PlayerPickup.ammo 미할당.");
        }

        void FixedUpdate()
        {
            int count = Physics2D.OverlapCircleNonAlloc(transform.position, pickupRadius, hits);
            for (int i = 0; i < count; i++)
            {
                var bullet = hits[i].GetComponent<Bullet>();
                if (bullet == null || !bullet.IsPickable) continue;

                if (!ammo.TryAdd()) return;
                Destroy(bullet.gameObject);
            }
        }
    }
}
