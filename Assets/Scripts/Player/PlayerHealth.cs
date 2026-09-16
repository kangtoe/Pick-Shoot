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

        void OnGUI()
        {
            DrawDamageFlash();

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(12f, 44f, 240f, 32f), $"HP    {current} / {maxHp}", style);
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
