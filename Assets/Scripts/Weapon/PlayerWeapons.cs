using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PickShoot
{
    // 플레이어의 다중 무기 슬롯 관리자. 사격 입력 처리·발사 우선순위 결정·HUD 표시.
    // GDD §7.1~§7.5 참조. 매 프레임 발사 가능한 무기 중 가장 오래 기다린 무기 하나를 발사한다.
    public class PlayerWeapons : MonoBehaviour
    {
        public const int MaxSlots = 4;

        [SerializeField] Transform muzzle;
        [SerializeField] List<WeaponData> startingWeapons = new List<WeaponData>();

        Collider2D myCollider;
        readonly List<Weapon> slots = new List<Weapon>(MaxSlots);

        public IReadOnlyList<Weapon> Slots => slots;

        void Awake()
        {
            myCollider = GetComponent<Collider2D>();
            if (myCollider == null)
                throw new System.Exception("[PickShoot] PlayerWeapons는 같은 GameObject의 Collider2D를 요구합니다.");
            if (muzzle == null)
                throw new System.Exception("[PickShoot] PlayerWeapons.muzzle 미할당.");
            if (startingWeapons.Count == 0)
                throw new System.Exception("[PickShoot] PlayerWeapons.startingWeapons가 비어 있음. 최소 1자루의 WeaponData가 필요합니다.");
            if (startingWeapons.Count > MaxSlots)
                throw new System.Exception($"[PickShoot] PlayerWeapons.startingWeapons가 최대 슬롯({MaxSlots})을 초과.");

            for (int i = 0; i < startingWeapons.Count; i++)
            {
                var data = startingWeapons[i];
                if (data == null)
                    throw new System.Exception($"[PickShoot] PlayerWeapons.startingWeapons[{i}]가 null.");
                slots.Add(new Weapon(data));
            }
        }

        void Update()
        {
            if (Time.timeScale <= 0f) return;
            var mouse = Mouse.current;
            if (mouse == null) return;
            if (!mouse.leftButton.isPressed) return;

            float now = Time.time;
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

            Vector3 pos = muzzle.position;
            Vector2 dir = transform.right;
            slots[bestIdx].Fire(pos, dir, myCollider, now);
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
                GUI.Label(new Rect(12f, y, 320f, 28f),
                    $"[{i + 1}] {w.Data.DisplayName}  {w.CurrentAmmo} / {w.MaxAmmo}", style);
                y += 24f;
            }
        }
    }
}
