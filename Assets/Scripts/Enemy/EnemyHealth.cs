using UnityEngine;

namespace PickShoot
{
    public class EnemyHealth : MonoBehaviour
    {
        [SerializeField] EnemyData data;
        [SerializeField] float flashDuration = 0.08f;
        [SerializeField] Color flashColor = Color.white;
        [SerializeField] GameObject deathEffectPrefab;

        int current;
        float flashUntil;
        SpriteRenderer sr;
        Color originalColor;

        void Awake()
        {
            if (data == null)
                throw new System.Exception($"[PickShoot] EnemyHealth.data 미설정 ({name}).");
            current = data.MaxHp;
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

        // 외부에서 강제 사망 (예: 웨이브 종료 시 잔존 적 정리).
        // Die 경로를 재사용해 사망 이펙트가 나타나도록 한다.
        public void Kill()
        {
            if (current <= 0) return;
            current = 0;
            Die();
        }

        void Die()
        {
            if (deathEffectPrefab != null)
                Instantiate(deathEffectPrefab, transform.position, Quaternion.identity);
            Destroy(gameObject);
        }
    }
}
