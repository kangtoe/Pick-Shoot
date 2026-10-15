using UnityEngine;

namespace PickShoot
{
    // 적의 HP·피격·사망 (Enemies §1). 피격 순간 스프라이트를 흰 실루엣으로 한 번 점멸한다 — 렌더러의 색 틴트(SpriteRenderer.color)는 곱셈이라
    // 흰색을 넣어도 원본 그대로이므로, 텍스처 알파만 읽고 단색으로 채우는 SpriteFlash 머티리얼로 바꿔 끼웠다가 flashDuration 뒤 원래 것으로 되돌린다.
    // 켜고 끄기뿐이고 페이드는 없다(픽셀아트). 색 틴트는 건드리지 않으므로 다른 연출이 색을 써도 겹치지 않는다.
    // 죽는 타격에는 플래시가 없다 — 그 자리에서 파괴되고 사망 이펙트가 대신한다. 플레이어가 죽였으면 처치 경험치(PlayerExperience)가 적립되고 노란 XP N이 뜬다.
    // 소리는 피격음 하나뿐이다 — 죽는 타격도 같은 피격음을 내고 사망음은 따로 없다. 피해 없이 죽는 정리(Kill)는 소리가 없다.
    [RequireComponent(typeof(SpriteRenderer))]
    public class EnemyHealth : MonoBehaviour
    {
        [SerializeField] EnemyData data;
        [SerializeField, Min(0f)] float flashDuration = 0.08f;
        [SerializeField] Material flashMaterial;
        [SerializeField] Color damageNumberColor = Color.white;
        [SerializeField] GameObject deathEffectPrefab;
        [SerializeField] AudioClip hitClip;
        [SerializeField, Range(0f, 1f)] float hitVolume = 0.3f;

        // 죽는 순간 (사망 이펙트 뒤, 파괴 직전). 웨이브 종료 정리(Kill)로 죽을 때도 난다. 마디가 죽으면 사슬을 다시 잇는 서펜트가 받는다.
        public event System.Action Died;
        // 플레이어에게 죽는 순간 — 피해로 HP가 다한 때만. 웨이브 종료 정리(Kill)로 죽을 때는 나지 않는다 — 그때 낳은 적은 정리에서 빠진다.
        // 죽을 때 졸개를 놓는 소환사가 받는다. Died보다 먼저 난다.
        public event System.Action Slain;

        // 원래 그림의 머티리얼 — 피격 점멸 중에는 렌더러의 것이 잠시 SpriteFlash라, 몸의 모습을 베끼는 쪽(돌진 잔상)이 대신 쓴다.
        public Material BodyMaterial => originalMaterial;
        // 몸이 닿은 플레이어가 입는 피해. PlayerHealth가 접촉 충돌에서 읽는다 (Enemies §1). 챔피언·울트라의 가산이 붙는다 (Enemies §7).
        public int ContactDamage => data.ContactDamage + rank.DamageBonus;
        // HP가 다해 이번 프레임 끝에 파괴될 적. 유도 탄환(Bullet)이 표적에서 뺀다.
        public bool IsDead => current <= 0;
        // 등급의 이동 속도 배율. EnemyMover가 EnemyData의 이동 속도에 곱한다.
        public float SpeedMultiplier => rank.SpeedMultiplier;
        // 입은 등급. 서펜트의 몸통은 그대로, 소환사의 졸개는 웨이브 강화만 물려받는다 (Enemies §7).
        public EnemyRank Rank => rank;

        int current;
        EnemyRank rank = EnemyRank.Normal;
        bool rankApplied;
        float flashUntil;
        bool flashing;
        SpriteRenderer sr;
        Material originalMaterial;

        void Awake()
        {
            if (data == null)
                throw new System.Exception($"[PickShoot] EnemyHealth.data 미설정 ({name}).");
            if (flashMaterial == null)
                throw new System.Exception($"[PickShoot] EnemyHealth.flashMaterial 미설정 ({name}). 피격 점멸용 SpriteFlash 머티리얼(Assets/Art/Materials) 필요.");
            current = data.MaxHp;
            sr = GetComponent<SpriteRenderer>();
            originalMaterial = sr.sharedMaterial;
        }

        void Update()
        {
            if (flashing && Time.time >= flashUntil) EndFlash();
        }

        // 등급·웨이브 강화를 입힌다 (Enemies §7) — 스폰 직후 한 번, 맞기 전에. HP·접촉 피해·경험치는 여기서 바뀌고 이동 속도는 EnemyMover가 SpeedMultiplier로 읽는다.
        // 등급은 색 없이 크기로만 읽힌다 — 몸 전체(콜라이더 포함)를 Scale만큼 키운다.
        public void ApplyRank(EnemyRank newRank)
        {
            if (rankApplied)
                throw new System.Exception($"[PickShoot] EnemyHealth.ApplyRank: 등급을 두 번 입힘 ({name}).");
            if (current != data.MaxHp)
                throw new System.Exception($"[PickShoot] EnemyHealth.ApplyRank: 이미 피해를 입은 적에 등급을 입힘 ({name}, HP {current}/{data.MaxHp}). 스폰 직후에만 부른다.");
            rank = newRank;
            rankApplied = true;
            current = Mathf.Max(1, Mathf.RoundToInt(data.MaxHp * rank.HpMultiplier));
            transform.localScale *= rank.Scale;
        }

        public void TakeDamage(int amount)
        {
            if (current <= 0) return;
            current -= amount;
            // 머리 위 누적 피해에 더한다 (GDD §3.3). 타격마다 낱개 숫자를 띄우지 않는다.
            DamageNumbers.Accumulate(transform, DamageNumberOrigin(), amount, damageNumberColor);
            if (hitClip != null) SoundPlayer.Play(hitClip, hitVolume);
            if (current <= 0)
            {
                Die(slain: true);
                return;
            }
            BeginFlash();
        }

        // 피격 순간부터 flashDuration 동안 흰 실루엣. 점멸 중에 또 맞으면 끝나는 시각만 늘어난다.
        void BeginFlash()
        {
            sr.sharedMaterial = flashMaterial;
            flashUntil = Time.time + flashDuration;
            flashing = true;
        }

        void EndFlash()
        {
            sr.sharedMaterial = originalMaterial;
            flashing = false;
        }

        // 누적 피해 숫자와 처치 경험치는 스프라이트의 위쪽 가장자리를 기준으로 뜬다.
        Vector3 DamageNumberOrigin()
        {
            var p = transform.position;
            p.y = sr.bounds.max.y;
            return p;
        }

        // 외부에서 강제 사망 (예: 웨이브 종료 시 잔존 적 정리).
        // Die 경로를 재사용해 사망 이펙트가 나타나도록 한다. 피해가 아니므로 피격음은 나지 않는다.
        public void Kill()
        {
            if (current <= 0) return;
            current = 0;
            Die(slain: false);
        }

        // slain: 플레이어가 죽인 것(처치 경험치·Slain). 정리(Kill)는 아니다.
        void Die(bool slain)
        {
            if (deathEffectPrefab != null)
                Instantiate(deathEffectPrefab, transform.position, Quaternion.identity);
            // 처치 경험치는 플레이어가 죽인 때만 (GDD §3.3). 플로팅 텍스트는 데미지 숫자와 같은 자리를 기준으로 뜬다 — PlayerExperience가 한 줄 위로 올린다.
            if (slain) PlayerExperience.Award(Mathf.RoundToInt(data.ExpValue * rank.ExpMultiplier), DamageNumberOrigin());
            if (slain) Slain?.Invoke();
            Died?.Invoke();
            Destroy(gameObject);
        }
    }
}
