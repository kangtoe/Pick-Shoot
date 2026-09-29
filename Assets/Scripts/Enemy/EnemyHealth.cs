using UnityEngine;

namespace PickShoot
{
    public class EnemyHealth : MonoBehaviour
    {
        [SerializeField] EnemyData data;
        [SerializeField] float flashDuration = 0.08f;
        [SerializeField] Color flashColor = Color.white;
        [SerializeField] Color damageNumberColor = Color.white;
        [SerializeField] GameObject deathEffectPrefab;
        [SerializeField] AudioClip hitClip;
        [SerializeField] AudioClip deathClip;

        // 죽는 순간 (사망 이펙트 뒤, 파괴 직전). 웨이브 종료 정리(Kill)로 죽을 때도 난다. 마디가 죽으면 사슬을 다시 잇는 서펜트가 받는다.
        public event System.Action Died;

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
            DamageNumbers.Show(DamageNumberOrigin(), amount, damageNumberColor);
            if (current <= 0) Die(playSound: true);
            else if (hitClip != null) SoundPlayer.Play(hitClip);
        }

        // 데미지 숫자는 피격당한 스프라이트의 위쪽 가장자리에서 떠오른다.
        Vector3 DamageNumberOrigin()
        {
            var p = transform.position;
            if (sr != null) p.y = sr.bounds.max.y;
            return p;
        }

        // 외부에서 강제 사망 (예: 웨이브 종료 시 잔존 적 정리).
        // Die 경로를 재사용해 사망 이펙트가 나타나도록 한다. 다수가 동시에 정리되므로 사망음은 내지 않는다.
        public void Kill()
        {
            if (current <= 0) return;
            current = 0;
            Die(playSound: false);
        }

        void Die(bool playSound)
        {
            if (deathEffectPrefab != null)
                Instantiate(deathEffectPrefab, transform.position, Quaternion.identity);
            if (playSound && deathClip != null) SoundPlayer.Play(deathClip);
            Died?.Invoke();
            Destroy(gameObject);
        }
    }
}
