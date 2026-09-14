using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PickShoot
{
    // 플레이어의 다중 무기 슬롯 관리자. 사격 입력 처리·발사 우선순위 결정·HUD 표시.
    // 각 무기는 플레이어를 중심으로 하는 원형 링 위에 균등 분포로 배치되며,
    // 링은 플레이어 회전(=조준 방향)을 따라 자연스럽게 회전한다 (GDD §7.2).
    public class PlayerWeapons : MonoBehaviour
    {
        public const int MaxSlots = 4;

        [SerializeField] List<WeaponData> startingWeapons = new List<WeaponData>();
        [SerializeField] Sprite weaponSprite;
        [SerializeField] float ringRadius = 1.0f;
        [SerializeField] float arcBaseDegrees = 30f;
        [SerializeField, Range(0f, 1f)] float arcExponent = 0.7f;
        [SerializeField] float weaponVisualScale = 0.3f;
        [SerializeField] int weaponSortingOrder = 6;
        [SerializeField, Min(0f)] float globalShotInterval = 0.1f;
        [SerializeField, Min(0f)] float ringMaxAngularSpeed = 720f;

        Collider2D myCollider;
        Transform ringPivot;
        readonly List<Weapon> slots = new List<Weapon>(MaxSlots);
        readonly List<Transform> slotVisuals = new List<Transform>(MaxSlots);
        float nextGlobalShotTime;
        float currentRingAngleDeg;

        public IReadOnlyList<Weapon> Slots => slots;
        public bool HasEmptySlot => slots.Count < MaxSlots;

        void Awake()
        {
            myCollider = GetComponent<Collider2D>();
            if (myCollider == null)
                throw new System.Exception("[PickShoot] PlayerWeapons는 같은 GameObject의 Collider2D를 요구합니다.");
            if (weaponSprite == null)
                throw new System.Exception("[PickShoot] PlayerWeapons.weaponSprite 미할당. 링 위 무기 시각화용 sprite 필요.");
            if (startingWeapons.Count == 0)
                throw new System.Exception("[PickShoot] PlayerWeapons.startingWeapons가 비어 있음. 최소 1자루의 WeaponData가 필요합니다.");
            if (startingWeapons.Count > MaxSlots)
                throw new System.Exception($"[PickShoot] PlayerWeapons.startingWeapons가 최대 슬롯({MaxSlots})을 초과.");

            ringPivot = new GameObject("WeaponRingPivot").transform;
            ringPivot.SetParent(transform, false);
            currentRingAngleDeg = transform.eulerAngles.z;
            ringPivot.rotation = Quaternion.Euler(0f, 0f, currentRingAngleDeg);

            for (int i = 0; i < startingWeapons.Count; i++)
            {
                var data = startingWeapons[i];
                if (data == null)
                    throw new System.Exception($"[PickShoot] PlayerWeapons.startingWeapons[{i}]가 null.");
                slots.Add(new Weapon(data));
                slotVisuals.Add(CreateVisual(data));
            }
            RearrangeRing();
        }

        // 새 슬롯에 무기 추가. 같은 종류라도 별도 자루로 추가 (GDD §7.3).
        public void AddWeapon(WeaponData data)
        {
            if (data == null)
                throw new System.Exception("[PickShoot] PlayerWeapons.AddWeapon: data가 null.");
            if (!HasEmptySlot)
                throw new System.Exception($"[PickShoot] PlayerWeapons.AddWeapon: 슬롯 가득 참 ({MaxSlots}). 호출자가 HasEmptySlot을 먼저 검증해야 함.");
            slots.Add(new Weapon(data));
            slotVisuals.Add(CreateVisual(data));
            RearrangeRing();
        }

        void LateUpdate()
        {
            // 인스펙터에서 arc 파라미터를 튜닝하면 즉시 반영. 슬롯 4자루 이하라 비용 무시할 수준.
            RearrangeRing();
        }

        Transform CreateVisual(WeaponData data)
        {
            var go = new GameObject($"Weapon_{data.DisplayName}");
            go.transform.SetParent(ringPivot, false);
            go.transform.localScale = new Vector3(weaponVisualScale, weaponVisualScale, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = weaponSprite;
            sr.color = data.VisualColor;
            sr.sortingOrder = weaponSortingOrder;
            return go.transform;
        }

        void RearrangeRing()
        {
            int n = slotVisuals.Count;
            // 자루 수에 따른 sub-linear arc 확장 (GDD §7.2).
            // arcExponent < 1 → 자루가 늘수록 총 arc 증가폭이 점차 줄어듦.
            float totalArc = n <= 1 ? 0f : arcBaseDegrees * Mathf.Pow(n - 1, arcExponent);
            for (int i = 0; i < n; i++)
            {
                // 조준 방향(local +x, 0°) 중심 부채꼴로 대칭 분포.
                // n=1: 0°, n>=2: [-arc/2, +arc/2] 균등.
                float angleDeg = n == 1 ? 0f : -totalArc * 0.5f + totalArc * i / (n - 1);
                float rad = angleDeg * Mathf.Deg2Rad;
                slotVisuals[i].localPosition = new Vector3(
                    Mathf.Cos(rad) * ringRadius,
                    Mathf.Sin(rad) * ringRadius,
                    0f);
            }
        }

        void Update()
        {
            // 링은 플레이어 조준각을 최대 각속도로 따라간다 → 스냅이 아닌 약간의 지연.
            float targetAngleDeg = transform.eulerAngles.z;
            currentRingAngleDeg = Mathf.MoveTowardsAngle(currentRingAngleDeg, targetAngleDeg, ringMaxAngularSpeed * Time.deltaTime);
            ringPivot.rotation = Quaternion.Euler(0f, 0f, currentRingAngleDeg);

            if (Time.timeScale <= 0f) return;
            var mouse = Mouse.current;
            if (mouse == null) return;
            if (!mouse.leftButton.isPressed) return;

            float now = Time.time;
            if (now < nextGlobalShotTime) return;

            int bestIdx = -1;
            float bestReady = float.PositiveInfinity;
            for (int i = 0; i < slots.Count; i++)
            {
                var w = slots[i];
                if (!w.CanFire(now)) continue;
                if (w.NextReadyTime < bestReady)
                {
                    bestReady = w.NextReadyTime;
                    bestIdx = i;
                }
            }
            if (bestIdx < 0) return;

            Vector3 pos = slotVisuals[bestIdx].position;
            Vector2 dir = ringPivot.right;
            slots[bestIdx].Fire(pos, dir, myCollider, now);
            nextGlobalShotTime = now + globalShotInterval;
        }

        void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                normal = { textColor = Color.white }
            };
            float y = 8f;
            for (int i = 0; i < slots.Count; i++)
            {
                var w = slots[i];
                GUI.Label(new Rect(12f, y, 360f, 28f),
                    $"[{i + 1}] {w.Data.DisplayName} Lv.{w.Level}  {w.CurrentAmmo} / {w.MaxAmmo}", style);
                y += 24f;
            }
        }
    }
}
