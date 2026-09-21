using System;
using UnityEngine;

namespace PickShoot
{
    [RequireComponent(typeof(PlayerMovement))]
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
        PlayerMovement movement;
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
            movement = GetComponent<PlayerMovement>();
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

        // 회피 중(대시 + 끝난 뒤 짧은 유예)에는 어떤 피해도 받지 않는다 (GDD §3.5). 적의 몸은 PlayerMovement가 충돌을 꺼서 통과시키므로 접촉 피해는 애초에
        // 들어오지 않고, 여기서는 접촉 없이 들어오는 피해(추후 적 탄환 등)까지 막는다. false를 받은 공격은 빗나간 것으로 다뤄야 한다.
        public bool TryTakeDamage(int amount)
        {
            if (!IsAlive || IsInvincible || movement.IsEvading) return false;
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
