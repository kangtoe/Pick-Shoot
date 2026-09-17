using System;
using UnityEngine;

namespace PickShoot
{
    public class PlayerHealth : MonoBehaviour
    {
        [SerializeField] int maxHp = 3;
        [SerializeField] float invincibilityTime = 0.8f;
        [SerializeField] float damageFlashDuration = 0.35f;
        [SerializeField] Color damageFlashColor = new Color(1f, 0.15f, 0.15f, 0.4f);
        [SerializeField] Color damageNumberColor = new Color(1f, 0.2f, 0.2f);
        [SerializeField] GameObject deathEffectPrefab;
        [SerializeField] AudioClip hurtClip;
        [SerializeField] AudioClip deathClip;

        int current;
        float invincibleUntil;
        float damageFlashStart = -1f;
        SpriteRenderer sr;
        Color originalColor;

        public int Current => current;
        public int Max => maxHp;
        public bool IsAlive => current > 0;
        public bool IsInvincible => Time.time < invincibleUntil;

        public event Action Died;

        void Awake()
        {
            current = maxHp;
            sr = GetComponent<SpriteRenderer>();
            if (sr != null) originalColor = sr.color;
        }

        // 웨이브 클리어 대기가 끝난 뒤 WaveManager가 호출. 다음 웨이브를 온전한 체력으로 맞이한다.
        public void RestoreFull()
        {
            if (!IsAlive)
                throw new System.Exception("[PickShoot] 사망한 플레이어에 RestoreFull 호출됨.");
            current = maxHp;
        }

        void Update()
        {
            if (sr == null) return;
            if (IsInvincible)
            {
                var c = originalColor;
                c.a = 0.4f + Mathf.PingPong(Time.time * 8f, 0.6f);
                sr.color = c;
            }
            else if (sr.color != originalColor)
            {
                sr.color = originalColor;
            }
        }

        void OnCollisionEnter2D(Collision2D col) => TryDamageFrom(col);
        void OnCollisionStay2D(Collision2D col) => TryDamageFrom(col);

        void TryDamageFrom(Collision2D col)
        {
            if (col.collider.GetComponent<EnemyHealth>() == null) return;
            TryTakeDamage(1);
        }

        public bool TryTakeDamage(int amount)
        {
            if (!IsAlive || IsInvincible) return false;
            current -= amount;
            invincibleUntil = Time.time + invincibilityTime;
            damageFlashStart = Time.time;
            DamageNumbers.Show(DamageNumberOrigin(), amount, damageNumberColor);
            if (current <= 0)
            {
                current = 0;
                Died?.Invoke();
                if (deathEffectPrefab != null)
                    Instantiate(deathEffectPrefab, transform.position, Quaternion.identity);
                if (deathClip != null) SoundPlayer.Play(deathClip);
                Destroy(gameObject);
            }
            else if (hurtClip != null)
            {
                SoundPlayer.Play(hurtClip);
            }
            return true;
        }

        // 데미지 숫자는 피격당한 스프라이트의 위쪽 가장자리에서 떠오른다.
        Vector3 DamageNumberOrigin()
        {
            var p = transform.position;
            if (sr != null) p.y = sr.bounds.max.y;
            return p;
        }

        // 체력 수치 표시는 GameHud가 맡는다. 여기서는 전체 화면 피격 플래시만 그린다.
        void OnGUI()
        {
            DrawDamageFlash();
        }

        void DrawDamageFlash()
        {
            if (damageFlashStart < 0f) return;
            float elapsed = Time.time - damageFlashStart;
            if (elapsed >= damageFlashDuration) return;

            float t = 1f - (elapsed / damageFlashDuration);
            var col = damageFlashColor;
            col.a *= t;

            var prev = GUI.color;
            GUI.color = col;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = prev;
        }
    }
}
