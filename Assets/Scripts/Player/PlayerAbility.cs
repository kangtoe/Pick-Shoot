using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PickShoot
{
    // 스페이스 능력 (Characters §1·§2). 러닝 시작에 고른 캐릭터(CharacterData)의 능력 하나를 SetCharacter로 받는다. 입력·쿨다운·리차지 배율·효과음은
    // 여기서 공통으로 다루고, 발동했을 때 하는 일만 종류별로 가른다.
    //   대시: PlayerMovement.RequestDash — 몸을 옮기고 회피를 켠다 (GDD §3.5). 잔상은 PlayerMovement가 남긴다.
    //   회수 펄스: PlayerPickup.Pulse — 반경 안의 자기 탄환을 모두 자석에 건다. 끌림이 처음부터 최대인 것 말고는 평소 자석과 같다.
    //   충격파: 반경 안의 적을 플레이어에게서 바깥으로 미는 넉백 (피해 없음). 폭발 탄환의 넉백과 같은 방사형이라 넉백을 무시하는 적(서펜트)은 안 밀리고,
    //          돌진 중인 돌진형은 밀린다 (Enemies §4).
    // 펄스는 빨려 드는 고리, 충격파는 퍼져 나가는 고리를 반경만큼 그린다 (AbilityEffect, 발동한 자리에 머문다).
    // 캐릭터 자산에 발동 무적이 있으면 쓰는 순간부터 그 시간 동안 피해를 받지 않는다 (PlayerHealth가 IsInvulnerable을 본다). 대시의 회피와 달리 몸은 적과
    // 그대로 부딪힌다 — 막을 뿐 빠져나가게 해 주지는 않는다. 그동안 플레이어를 따라다니는 막이 보인다.
    // 쿨다운은 쓴 순간부터 세고, 리차지 배율(Relics §2)은 시작하는 쿨다운에만 적용한다 — 진행 중인 쿨다운은 건드리지 않는다. 머리 위 원형 표시는 GameHud가
    // CooldownProgress를 읽어 그린다.
    [RequireComponent(typeof(PlayerMovement))]
    [RequireComponent(typeof(PlayerPickup))]
    [RequireComponent(typeof(PlayerStats))]
    public class PlayerAbility : MonoBehaviour
    {
        // 능력의 연출 (AbilityEffect 프리팹). 색의 알파는 연출 전체의 불투명도 — 모양마다의 옅고 진함은 AbilityEffect가 정한다.
        // 무적 막은 능력과 무관하게 한 색이다 — 어느 캐릭터든 "지금 피해를 받지 않는다"로 같게 읽히도록. 모래 바닥에 묻히지 않는 색이어야 한다 — 따뜻한 흰색·금색은
        // 바닥과 색조가 같아 거의 보이지 않았다 (Recordings/ability_fx_*_mockup_2026-10-14.png).
        [SerializeField] AbilityEffect effectPrefab;
        [SerializeField] Color pulseEffectColor = new Color(0.55f, 0.9f, 1f, 0.9f);
        [SerializeField] Color shockwaveEffectColor = Color.white;
        [SerializeField] Color shieldEffectColor = new Color(0.6f, 0.85f, 1f, 1f);
        // 충격파가 미는 레이어 (적).
        [SerializeField] LayerMask shockwaveLayers;

        PlayerMovement movement;
        PlayerPickup pickup;
        PlayerStats stats;
        // 고른 캐릭터 (SetCharacter). 고르기 전에는 null — 그동안 게임은 멈춰 있어야 한다.
        CharacterData character;
        ContactFilter2D shockwaveFilter;
        // 마지막으로 쓴 시각과 다시 쓸 수 있는 시각, 발동 무적이 풀리는 시각.
        float startTime;
        float readyTime;
        float invulnerableUntil;
        static readonly List<Collider2D> shockwaveHits = new List<Collider2D>();

        public bool Ready => Time.time >= readyTime;
        // 쿨다운 진행도. 0 = 방금 씀, 1 = 사용 가능. GameHud의 머리 위 원형 표시가 읽는다.
        public float CooldownProgress => Ready ? 1f : Mathf.InverseLerp(startTime, readyTime, Time.time);
        // 발동 무적 중 — 어떤 피해도 받지 않는다 (PlayerHealth).
        public bool IsInvulnerable => Time.time < invulnerableUntil;

        // 러닝 시작 화면이 고른 캐릭터의 능력을 넣는다 (UpgradeManager). 다른 캐릭터를 다시 고르면 바뀐다 — 게임이 멈춘 시작 화면에서만 부른다.
        public void SetCharacter(CharacterData data)
        {
            if (data == null)
                throw new System.Exception("[PickShoot] PlayerAbility.SetCharacter: 캐릭터가 null.");
            character = data;
        }

        void Awake()
        {
            movement = GetComponent<PlayerMovement>();
            pickup = GetComponent<PlayerPickup>();
            stats = GetComponent<PlayerStats>();
            if (effectPrefab == null)
                throw new System.Exception("[PickShoot] PlayerAbility.effectPrefab 미할당. 펄스·충격파·무적 막의 연출(AbilityEffect 프리팹) 필요.");
            if (shockwaveLayers.value == 0)
                throw new System.Exception("[PickShoot] PlayerAbility.shockwaveLayers 미설정. 충격파가 밀 레이어(Enemy) 필요.");
            shockwaveFilter = new ContactFilter2D();
            shockwaveFilter.SetLayerMask(shockwaveLayers);
            shockwaveFilter.useTriggers = false;
        }

        void Update()
        {
            // 게임이 멈춘 동안(일시정지·카드 선택, timeScale 0)의 입력은 받지 않는다 — 재개하자마자 능력이 나가지 않게.
            // 쿨다운 중의 입력은 버린다 (선입력 없음).
            if (Time.timeScale <= 0f || !SpacePressed() || !Ready) return;
            if (character == null)
                throw new System.Exception("[PickShoot] PlayerAbility: 캐릭터 미설정인데 스페이스 입력 — 러닝 시작 화면에서 캐릭터를 고른 뒤에만 게임이 움직여야 함 (Characters §5).");
            // 리차지가 아주 높아 대시가 끝나기 전에 쿨다운이 돌면 그 입력도 버린다.
            if (character.AbilityKind == AbilityKind.Dash && movement.IsDashing) return;
            Activate();
        }

        static bool SpacePressed()
        {
            var kb = Keyboard.current;
            return kb != null && kb.spaceKey.wasPressedThisFrame;
        }

        void Activate()
        {
            startTime = Time.time;
            readyTime = startTime + character.AbilityCooldown / stats.RechargeMultiplier;
            switch (character.AbilityKind)
            {
                case AbilityKind.Dash:
                    movement.RequestDash(character.DashDistance, character.DashEvadeGrace);
                    break;
                case AbilityKind.Pulse:
                    pickup.Pulse(character.AbilityRadius);
                    SpawnEffect().PlayPulse(character.AbilityRadius, pulseEffectColor);
                    break;
                case AbilityKind.Shockwave:
                    PushEnemies(character.AbilityRadius, character.ShockwaveKnockback);
                    SpawnEffect().PlayShockwave(character.AbilityRadius, shockwaveEffectColor);
                    break;
                default:
                    throw new System.Exception($"[PickShoot] PlayerAbility: 알 수 없는 능력 {character.AbilityKind} ('{character.name}').");
            }
            if (character.AbilityInvulnerability > 0f)
            {
                invulnerableUntil = Time.time + character.AbilityInvulnerability;
                SpawnEffect().PlayShield(transform, character.AbilityInvulnerability, shieldEffectColor);
            }
            if (character.AbilityClip != null) SoundPlayer.Play(character.AbilityClip, character.AbilityVolume);
        }

        // 반경 안의 적을 플레이어 중심에서 바깥으로 민다 — 거리와 무관하게 같은 세기 (폭발 탄환의 넉백과 같다, Bullet.Explode). 피해는 없다.
        void PushEnemies(float radius, float knockback)
        {
            Vector2 center = transform.position;
            Physics2D.OverlapCircle(center, radius, shockwaveFilter, shockwaveHits);
            foreach (var hit in shockwaveHits)
            {
                var mover = hit.GetComponent<EnemyMover>();
                if (mover == null) continue;
                Vector2 away = (Vector2)hit.transform.position - center;
                // 적의 몸은 플레이어와 부딪히므로 중심이 겹칠 일은 없다 — 겹쳤다면 방향이 없으니 밀지 않는다.
                if (away.sqrMagnitude < 0.0001f) continue;
                mover.ApplyKnockback(away.normalized * knockback);
            }
        }

        AbilityEffect SpawnEffect() => Instantiate(effectPrefab, transform.position, Quaternion.identity);
    }
}
