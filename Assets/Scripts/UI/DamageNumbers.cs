using System.Collections.Generic;
using UnityEngine;

namespace PickShoot
{
    // 피격 데미지 숫자 표시 단일 창구. 씬에 정확히 하나 존재해야 한다.
    // 숫자는 월드 좌표에 묶여 위로 떠오르며 사라지고, 매 프레임 화면 좌표로 바꿔 OnGUI에서
    // PixelUiSkin 비트맵 글꼴로 그린다 (픽셀 UI 규약: PixelGui 기준 캔버스).
    // 색은 호출자가 정한다 — 적 피격은 흰색(EnemyHealth), 플레이어 피격은 붉은색(PlayerHealth).
    public class DamageNumbers : MonoBehaviour
    {
        [SerializeField] PixelUiSkin skin;
        [SerializeField, Min(0.01f)] float duration = 0.6f;
        [SerializeField, Min(0f)] float riseDistance = 1f;
        [SerializeField, Range(0f, 1f)] float fadeStart = 0.5f;
        [SerializeField, Min(0f)] float horizontalJitter = 0.3f;

        struct Popup
        {
            public Vector3 Origin;
            public string Text;
            public Color Color;
            public float StartTime;
        }

        static DamageNumbers instance;
        readonly List<Popup> popups = new List<Popup>();
        Camera cam;

        void Awake()
        {
            if (instance != null && instance != this)
                throw new System.Exception($"[PickShoot] DamageNumbers가 씬에 둘 이상 존재 ({instance.name}, {name}).");
            if (skin == null)
                throw new System.Exception("[PickShoot] DamageNumbers.skin 미할당. PixelUiSkin 자산(Assets/Data/UI) 필요.");
            skin.Validate();
            cam = Camera.main;
            if (cam == null)
                throw new System.Exception("[PickShoot] MainCamera가 필요합니다 (데미지 숫자 월드→화면 변환용).");
            instance = this;
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        // worldPos는 숫자가 떠오르기 시작하는 지점 (보통 피격당한 스프라이트의 위쪽 가장자리).
        public static void Show(Vector3 worldPos, int amount, Color color)
        {
            if (instance == null)
                throw new System.Exception("[PickShoot] 씬에 DamageNumbers가 없습니다. 데미지 숫자 표시용 DamageNumbers 오브젝트 필요.");
            instance.Add(worldPos, amount, color);
        }

        void Add(Vector3 worldPos, int amount, Color color)
        {
            // 같은 자리에 연달아 뜨는 숫자(산탄 다발 명중 등)가 완전히 겹치지 않도록 좌우로 조금 흩뿌린다.
            worldPos.x += Random.Range(-horizontalJitter, horizontalJitter);
            popups.Add(new Popup { Origin = worldPos, Text = amount.ToString(), Color = color, StartTime = Time.time });
        }

        void Update()
        {
            for (int i = popups.Count - 1; i >= 0; i--)
                if (Time.time - popups[i].StartTime >= duration) popups.RemoveAt(i);
        }

        void OnGUI()
        {
            if (popups.Count == 0 || Event.current.type != EventType.Repaint) return;

            int textScale = PixelGui.TextScale;
            var font = skin.FontOutlined;
            int lineH = font.lineHeight * textScale;

            using var canvas = PixelGui.BeginCanvas();
            var prevColor = GUI.color;
            for (int i = 0; i < popups.Count; i++)
            {
                var p = popups[i];
                float t = Mathf.Clamp01((Time.time - p.StartTime) / duration);
                // 처음에 빠르게 솟았다가 느려지는 ease-out.
                float rise = riseDistance * (1f - (1f - t) * (1f - t));
                Vector3 screenPoint = cam.WorldToScreenPoint(p.Origin + Vector3.up * rise);
                if (screenPoint.z < 0f) continue;

                var c = p.Color;
                c.a *= 1f - Mathf.InverseLerp(fadeStart, 1f, t);
                GUI.color = c;
                // 숫자의 아래쪽 가장자리를 기준점에 맞춘다. 캔버스 좌표는 위가 0.
                Vector2 sp = canvas.FromScreenPoint(screenPoint);
                int x = Mathf.RoundToInt(sp.x);
                int y = Mathf.RoundToInt(sp.y) - lineH;
                PixelGui.DrawTextCentered(font, p.Text, x, y, textScale);
            }
            GUI.color = prevColor;
        }
    }
}
