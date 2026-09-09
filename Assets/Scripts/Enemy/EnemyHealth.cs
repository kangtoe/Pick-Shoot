using UnityEngine;

namespace PickShoot
{
    public class EnemyHealth : MonoBehaviour
    {
        [SerializeField] int maxHp = 3;
        [SerializeField] float flashDuration = 0.08f;
        [SerializeField] Color flashColor = Color.white;

        int current;
        float flashUntil;
        SpriteRenderer sr;
        Color originalColor;

        void Awake()
        {
            current = maxHp;
            sr = GetComponent<SpriteRenderer>();
            if (sr != null) originalColor = sr.color;
        }

        void Update()
        {
            if (sr == null) return;
            if (Time.time < flashUntil)
            {
                sr.color = flashColor;
            }
            else if (sr.color != originalColor)
            {
                sr.color = originalColor;
            }
        }

        public void TakeDamage(int amount)
        {
            if (current <= 0) return;
            current -= amount;
            flashUntil = Time.time + flashDuration;
            if (current <= 0) Die();
        }

        void Die()
        {
            Destroy(gameObject);
        }
    }
}
