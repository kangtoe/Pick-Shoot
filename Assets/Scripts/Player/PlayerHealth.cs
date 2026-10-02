using System;
using UnityEngine;

namespace PickShoot
{
    // 플레이어의 HP·피격·사망과 레벨 업 성장. 레벨 업(PlayerExperience.LeveledUp)마다 최대 체력이 levelUpMaxHpGain만큼 오르고 완충되며,
    // 머리 위에 "LV UP!"이 처치 경험치와 같은 움직임(DamageNumbers의 Rise)으로 떠오른다 (GDD §3.3).
    // 사망 순간 구독을 풀므로, 그 뒤 날아가던 탄환의 처치로 레벨이 올라도 여기엔 닿지 않는다.
    [RequireComponent(typeof(PlayerMovement))]
    public class PlayerHealth : MonoBehaviour
    {
        [SerializeField] PlayerExperience experience;
        // 시작 최대 체력. 러닝 중의 최대치는 max — 레벨 업으로 오른다.
        [SerializeField] int maxHp = 10;
        [SerializeField, Min(0)] int levelUpMaxHpGain = 1;
        [SerializeField] float invincibilityTime = 0.8f;
        [SerializeField] float damageFlashDuration = 0.35f;
        [SerializeField] Color damageFlashColor = new Color(1f, 0.15f, 0.15f, 0.4f);
        [SerializeField] Color damageNumberColor = new Color(1f, 0.2f, 0.2f);
        // 레벨 업 문구의 색. 기본값은 시트의 파란 게이지 채움 색 (#778FDB) — HUD 경험치 게이지와 같은 색.
        [SerializeField] Color levelUpTextColor = new Color32(119, 143, 219, 255);
        [SerializeField] GameObject deathEffectPrefab;
        [SerializeField] AudioClip hurtClip;
        [SerializeField] AudioClip deathClip;

        // 글꼴에 있는 문자(A-Z 0-9 % + - 공백)만 써야 한다. 없는 문자는 PixelGui가 예외로 알린다.
        const string LevelUpText = "LV UP!";
        // 플로팅 텍스트 한 줄의 높이 (월드 유닛): 16텍셀 × 글자 배율 2 = 월드 8텍셀 = 0.5유닛.
        const float TextLineUnits = 0.5f;

        int max;
        int current;
        // 같은 프레임에 여러 레벨이 오르면(한 번의 적립으로 문턱 여럿) 문구를 한 줄씩 위로 쌓는다 — 등속 상승은 흩뿌림이 없어 겹치면 하나로 보인다.
        float levelUpTextTime = -1f;
        int levelUpTextStack;
        float invincibleUntil;
        float damageFlashStart = -1f;
        SpriteRenderer sr;
        PlayerMovement movement;
        Color originalColor;

        public int Current => current;
        public int Max => max;
        public bool IsAlive => current > 0;
        public bool IsInvincible => Time.time < invincibleUntil;

        public event Action Died;

        void Awake()
        {
            if (experience == null)
                throw new System.Exception("[PickShoot] PlayerHealth.experience 미할당. PlayerExperience(GameManager) 참조 필요 (레벨 업 체력 성장).");
            max = maxHp;
            current = max;
            sr = GetComponent<SpriteRenderer>();
            movement = GetComponent<PlayerMovement>();
            if (sr != null) originalColor = sr.color;
            experience.LeveledUp += OnLeveledUp;
        }

        void OnDestroy()
        {
            if (experience != null) experience.LeveledUp -= OnLeveledUp;
        }

        // 웨이브 클리어 대기가 끝난 뒤 WaveManager가 호출. 다음 웨이브를 온전한 체력으로 맞이한다.
        public void RestoreFull()
        {
            if (!IsAlive)
                throw new System.Exception("[PickShoot] 사망한 플레이어에 RestoreFull 호출됨.");
            current = max;
        }

        // 레벨 업 (GDD §3.3): 최대 체력을 올리고 완충한다. 한 번의 적립으로 여러 레벨이 오르면 레벨마다 불린다.
        void OnLeveledUp()
        {
            if (!IsAlive)
                throw new System.Exception("[PickShoot] 사망한 플레이어에 레벨 업이 전달됨 — 사망 시 구독이 풀려야 함.");
            max += levelUpMaxHpGain;
            current = max;
            ShowLevelUpText();
        }

        // 레벨 업 알림: 머리 위(피격 숫자와 같은 자리)에서 "LV UP!"이 등속으로 떠오른다. 완충으로 머리 위 체력 게이지가 사라지는 순간이라 가리지 않는다.
        void ShowLevelUpText()
        {
            levelUpTextStack = Time.time == levelUpTextTime ? levelUpTextStack + 1 : 0;
            levelUpTextTime = Time.time;
            DamageNumbers.Show(DamageNumberOrigin() + Vector3.up * (TextLineUnits * levelUpTextStack), LevelUpText, levelUpTextColor, DamageNumbers.Motion.Rise);
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

        // 접촉 피해량은 닿은 적의 EnemyData가 정한다 (Enemies §1).
        void TryDamageFrom(Collision2D col)
        {
            var enemy = col.collider.GetComponent<EnemyHealth>();
            if (enemy == null) return;
            TryTakeDamage(enemy.ContactDamage);
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
                experience.LeveledUp -= OnLeveledUp;
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

        // 데미지 숫자와 레벨 업 문구는 스프라이트의 위쪽 가장자리에서 떠오른다.
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
