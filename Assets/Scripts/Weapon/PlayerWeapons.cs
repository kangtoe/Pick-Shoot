using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PickShoot
{
    // 플레이어의 다중 무기 슬롯 관리자. 사격 입력 처리·발사 우선순위 결정. 슬롯·탄약 표시는 GameHud가 Slots를 읽어 그린다.
    // 각 무기는 플레이어를 중심으로 하는 원형 링 위에 균등 분포로 배치되며,
    // 링은 조준 방향(PlayerAiming.AimAngleDeg)을 따라 자연스럽게 회전한다 (GDD §7.2).
    // 링 회전은 무기의 위치만 정하고, 총구 방향은 무기마다 제 위치에서 커서(PlayerAiming.AimWorldPosition)를 향한다 (Weapons §2).
    // 무기 스프라이트는 오른쪽(+x)을 향해 그려져 있으므로, 자기 조준 방향이 왼쪽인 무기는 상하 반전해 거꾸로 보이지 않게 한다.
    // 발사 연출(반동 킥·총구 화염)도 여기서 담당한다. 무기 비주얼을 소유하는 곳이 링 위치와 반동 오프셋을 함께 계산해야
    // 매 프레임 RearrangeRing이 위치를 덮어써도 반동이 살아남기 때문.
    public class PlayerWeapons : MonoBehaviour
    {
        public const int MaxSlots = 12;

        [SerializeField] List<WeaponData> startingWeapons = new List<WeaponData>();
        [SerializeField] float ringRadius = 1.0f;
        [SerializeField, Min(0f)] float ringRadiusPerExtraWeapon = 0.04f;
        [SerializeField] float arcBaseDegrees = 30f;
        [SerializeField, Range(0f, 1f)] float arcExponent = 0.7f;
        [SerializeField] int weaponSortingOrder = 6;
        [SerializeField, Min(0f)] float globalShotInterval = 0.1f;
        [SerializeField, Min(0f)] float ringMaxAngularSpeed = 720f;

        [Header("Fire Feedback")]
        [SerializeField] MuzzleFlash muzzleFlashPrefab;
        [SerializeField, Range(0f, 1f)] float muzzleFlashWhiteBlend = 0.4f;
        [SerializeField, Min(0f)] float recoilDistance = 0.15f;
        [SerializeField, Min(0f)] float recoilAngleDegrees = 10f;
        [SerializeField, Min(0f)] float recoilReturnSpeed = 18f;
        // 모든 자루의 탄약이 0인 채 사격을 시도했을 때의 헛방 소리.
        [SerializeField] AudioClip emptyClip;

        Collider2D myCollider;
        PlayerAiming aiming;
        Transform ringPivot;
        readonly List<Weapon> slots = new List<Weapon>(MaxSlots);
        readonly List<Transform> slotVisuals = new List<Transform>(MaxSlots);
        readonly List<SpriteRenderer> slotRenderers = new List<SpriteRenderer>(MaxSlots);
        // 슬롯별 반동 킥 잔량 (0 = 제자리). 발사 시 WeaponData.RecoilStrength로 세팅되고 지수 감쇠로 복귀.
        readonly List<float> slotRecoil = new List<float>(MaxSlots);
        float nextGlobalShotTime;
        float currentRingAngleDeg;
        // 헛방 소리는 "탄약이 바닥난 순간"마다 한 번만 낸다. 사격 버튼을 떼거나 다시 한 발이라도 쏘면 풀린다.
        bool emptySounded;

        public IReadOnlyList<Weapon> Slots => slots;
        public bool HasEmptySlot => slots.Count < MaxSlots;
        // 사격할 때마다 쏜 무기를 알린다. GameHud가 크로스헤어 링을 벌리는 데 쓴다.
        public event System.Action<Weapon> Fired;

        // 슬롯이 링에서 조준 기준 왼쪽(반시계) 부채꼴에 있는가. 정중앙(홀수 자루의 슬롯 1)은 왼쪽으로 친다.
        // GameHud가 무기 표시를 화면 하단 좌우 패널로 나누는 기준.
        public bool IsOnLeftOfAim(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= slots.Count)
                throw new System.Exception($"[PickShoot] PlayerWeapons.IsOnLeftOfAim: slotIndex {slotIndex}가 범위 밖 (슬롯 {slots.Count}개).");
            return CenterOutOffset(slotIndex, slots.Count) >= 0f;
        }

        void Awake()
        {
            myCollider = GetComponent<Collider2D>();
            if (myCollider == null)
                throw new System.Exception("[PickShoot] PlayerWeapons는 같은 GameObject의 Collider2D를 요구합니다.");
            aiming = GetComponent<PlayerAiming>();
            if (aiming == null)
                throw new System.Exception("[PickShoot] PlayerWeapons는 같은 GameObject의 PlayerAiming을 요구합니다 (조준각 출처).");
            if (startingWeapons.Count == 0)
                throw new System.Exception("[PickShoot] PlayerWeapons.startingWeapons가 비어 있음. 최소 1자루의 WeaponData가 필요합니다.");
            if (startingWeapons.Count > MaxSlots)
                throw new System.Exception($"[PickShoot] PlayerWeapons.startingWeapons가 최대 슬롯({MaxSlots})을 초과.");
            if (muzzleFlashPrefab == null)
                throw new System.Exception("[PickShoot] PlayerWeapons.muzzleFlashPrefab 미할당. 씬의 PlayerWeapons에 MuzzleFlash 프리팹(컴포넌트 참조) 필요.");

            ringPivot = new GameObject("WeaponRingPivot").transform;
            ringPivot.SetParent(transform, false);
            currentRingAngleDeg = aiming.AimAngleDeg;
            ringPivot.rotation = Quaternion.Euler(0f, 0f, currentRingAngleDeg);

            for (int i = 0; i < startingWeapons.Count; i++)
            {
                var data = startingWeapons[i];
                if (data == null)
                    throw new System.Exception($"[PickShoot] PlayerWeapons.startingWeapons[{i}]가 null.");
                AddSlot(data);
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
            AddSlot(data);
            RearrangeRing();
        }

        // 마지막 슬롯의 무기를 버린다. 게임 흐름에는 버리기가 없고 디버그 도구(DebugToolsWindow)가 링 테스트용으로 쓴다.
        // 최소 1자루는 유지한다 (Weapons §2 시작 슬롯 1). 그 무기가 쏜 탄환은 계속 그 Weapon 객체에 귀속돼 회수돼도 링에는 영향 없다.
        public void RemoveLastWeapon()
        {
            if (slots.Count <= 1)
                throw new System.Exception("[PickShoot] PlayerWeapons.RemoveLastWeapon: 슬롯이 1개뿐이라 버릴 수 없음. 호출자가 Slots.Count > 1을 먼저 검증해야 함.");
            int last = slots.Count - 1;
            Destroy(slotVisuals[last].gameObject);
            slots.RemoveAt(last);
            slotVisuals.RemoveAt(last);
            slotRenderers.RemoveAt(last);
            slotRecoil.RemoveAt(last);
            RearrangeRing();
        }

        void AddSlot(WeaponData data)
        {
            slots.Add(new Weapon(data));
            slotVisuals.Add(CreateVisual(data));
            slotRecoil.Add(0f);
        }

        void LateUpdate()
        {
            // 인스펙터에서 arc 파라미터를 튜닝하면 즉시 반영. 슬롯 12자루 이하라 비용 무시할 수준.
            RearrangeRing();

            // 반동은 발사 프레임에 최대치를 보여준 뒤 지수 감쇠로 복귀 (급히 밀렸다가 갈수록 천천히 제자리).
            float decay = Mathf.Exp(-recoilReturnSpeed * Time.deltaTime);
            for (int i = 0; i < slotRecoil.Count; i++)
                slotRecoil[i] = slotRecoil[i] < 0.01f ? 0f : slotRecoil[i] * decay;
        }

        Transform CreateVisual(WeaponData data)
        {
            var go = new GameObject($"Weapon_{data.DisplayName}");
            go.transform.SetParent(ringPivot, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = data.WeaponSprite;
            sr.sortingOrder = weaponSortingOrder;
            slotRenderers.Add(sr);
            return go.transform;
        }

        void RearrangeRing()
        {
            int n = slotVisuals.Count;
            // 자루 수에 따른 sub-linear arc 확장 (GDD §7.2).
            // arcExponent < 1 → 자루가 늘수록 총 arc 증가폭이 점차 줄어듦.
            float totalArc = n <= 1 ? 0f : arcBaseDegrees * Mathf.Pow(n - 1, arcExponent);
            float step = n <= 1 ? 0f : totalArc / (n - 1);
            // 자루가 늘수록 부채꼴 안 간격이 좁아지므로 반경을 자루당 일정량 키워 무기끼리 겹치는 걸 완화한다 (Weapons §2).
            float radius = ringRadius + ringRadiusPerExtraWeapon * (n - 1);
            for (int i = 0; i < n; i++)
            {
                // 조준 방향(local +x, 0°) 중심 부채꼴로 대칭 분포하되,
                // 슬롯은 중앙부터 좌우 번갈아 바깥으로 배정 (Weapons §2).
                // 슬롯 1 이후는 홀수→한쪽, 짝수→반대쪽으로 고정되어 추가 시 좌우가 뒤바뀌지 않는다.
                // 짝수 자루면 정중앙 자리가 없으므로 슬롯 0은 안쪽 한 자리로 반 칸 비켜난다.
                float slotAngleDeg = step * CenterOutOffset(i, n);
                float slotRad = slotAngleDeg * Mathf.Deg2Rad;
                Vector2 slotLocal = new Vector2(Mathf.Cos(slotRad) * radius, Mathf.Sin(slotRad) * radius);

                // 무기마다 제 위치에서 커서를 조준한다 (Weapons §2). 링(부모)이 이미 회전해 있으므로 로컬 회전은 그 차이만큼.
                float aimDeg = AimAngleFrom(ringPivot.TransformPoint(slotLocal));
                float localAimDeg = aimDeg - currentRingAngleDeg;
                float localAimRad = localAimDeg * Mathf.Deg2Rad;
                Vector2 localAimDir = new Vector2(Mathf.Cos(localAimRad), Mathf.Sin(localAimRad));
                // 왼쪽 조준 시 스프라이트가 상하 반전되므로 "총구가 위로 들리는" 회전 방향도 뒤집는다.
                bool facingLeft = Mathf.Cos(aimDeg * Mathf.Deg2Rad) < 0f;
                float recoilTiltSign = facingLeft ? -1f : 1f;

                // 반동: 자기 조준 방향 반대로 밀리고, 총구가 살짝 들린다.
                float kick = slotRecoil[i];
                slotVisuals[i].localPosition = slotLocal - localAimDir * (kick * recoilDistance);
                slotVisuals[i].localRotation = Quaternion.Euler(0f, 0f, localAimDeg + kick * recoilAngleDegrees * recoilTiltSign);
                slotRenderers[i].flipY = facingLeft;
            }
        }

        // 월드 좌표 worldPos에서 커서를 향하는 조준각(월드, 도).
        // 커서가 그 지점 바로 위면 방향이 정의되지 않으므로 링 정면각을 쓴다.
        float AimAngleFrom(Vector2 worldPos)
        {
            Vector2 toCursor = aiming.AimWorldPosition - worldPos;
            if (toCursor.sqrMagnitude < 0.0001f) return currentRingAngleDeg;
            return Mathf.Atan2(toCursor.y, toCursor.x) * Mathf.Rad2Deg;
        }

        // 슬롯 인덱스 → 중앙 기준 칸 단위 오프셋 (양수: 조준 기준 왼쪽, 음수: 오른쪽).
        // 홀수 n: 0, -1, +1, -2, +2 ...   짝수 n: +0.5, -0.5, +1.5, -1.5 ...
        static float CenterOutOffset(int slotIndex, int slotCount)
        {
            bool odd = (slotIndex & 1) == 1;
            float sign = odd ? -1f : 1f;
            float magnitude = slotCount % 2 == 0
                ? slotIndex / 2 + 0.5f
                : (slotIndex + 1) / 2;
            return sign * magnitude;
        }

        void Update()
        {
            // 링은 플레이어 조준각을 최대 각속도로 따라간다 → 스냅이 아닌 약간의 지연.
            float targetAngleDeg = aiming.AimAngleDeg;
            currentRingAngleDeg = Mathf.MoveTowardsAngle(currentRingAngleDeg, targetAngleDeg, ringMaxAngularSpeed * Time.deltaTime);
            ringPivot.rotation = Quaternion.Euler(0f, 0f, currentRingAngleDeg);

            if (Time.timeScale <= 0f) return;
            var mouse = Mouse.current;
            if (mouse == null) return;
            if (!mouse.leftButton.isPressed)
            {
                emptySounded = false;
                return;
            }

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
            if (bestIdx < 0)
            {
                PlayEmptyFeedbackIfOutOfAmmo();
                return;
            }
            emptySounded = false;

            Vector3 pos = slotVisuals[bestIdx].position;
            // 탄환은 그 무기의 위치에서 커서를 향해 날아간다 (Weapons §2) → 여러 자루의 발사선이 조준점으로 수렴.
            float fireRad = AimAngleFrom(pos) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(fireRad), Mathf.Sin(fireRad));
            slots[bestIdx].Fire(pos, dir, myCollider, now);
            PlayFireFeedback(bestIdx);
            Fired?.Invoke(slots[bestIdx]);
            nextGlobalShotTime = now + globalShotInterval;
        }

        // 쏠 수 있는 무기가 없는 이유가 쿨다운이 아니라 "전 자루 탄약 0"일 때만 헛방 소리를 낸다.
        // 버튼을 누른 채 마지막 탄을 쏘면 다음 사격 차례(globalShotInterval 뒤)에 울려 사격음과 겹치지 않는다.
        void PlayEmptyFeedbackIfOutOfAmmo()
        {
            if (emptySounded) return;
            for (int i = 0; i < slots.Count; i++)
                if (slots[i].CurrentAmmo > 0) return;

            emptySounded = true;
            if (emptyClip != null) SoundPlayer.Play(emptyClip);
        }

        // 발사 연출: 반동 킥 세팅 + 무기 비주얼의 자식으로 총구 화염 생성.
        // 자식으로 두면 이동·링 회전·반동을 그대로 따라가서 화염이 총구에서 떨어져 보이지 않는다.
        void PlayFireFeedback(int slotIndex)
        {
            var data = slots[slotIndex].Data;
            slotRecoil[slotIndex] = data.RecoilStrength;

            var flash = Instantiate(muzzleFlashPrefab, slotVisuals[slotIndex]);
            flash.transform.localPosition = new Vector3(data.MuzzleOffset, 0f, 0f);
            flash.Tint(Color.Lerp(data.VisualColor, Color.white, muzzleFlashWhiteBlend));
        }
    }
}
