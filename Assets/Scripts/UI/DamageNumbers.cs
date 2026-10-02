using System.Collections.Generic;
using UnityEngine;

namespace PickShoot
{
    // 피해 숫자·처치 경험치 등 월드에 붙어 뜨는 짧은 문구의 단일 창구. 씬에 정확히 하나 존재해야 한다.
    // 문구는 월드 좌표에 묶이고, 매 프레임 화면 좌표로 바꿔 OnGUI에서 PixelUiSkin 비트맵 글꼴로 그린다 (픽셀 UI 규약: PixelGui 기준 캔버스).
    // 표시는 셋 (GDD §3.3):
    //   누적 피해(Tally) — 적 머리 위에 받은 피해의 합계. 적을 따라다니며 맞을 때마다 더해지고, 한동안 맞지 않으면 지워져 0으로 돌아간다.
    //   Pop — 맞은 자리에서 위로 튀어올랐다가 가라앉는 낱개 숫자 (Terraria 전투 텍스트 모델). 플레이어 피격이 쓴다.
    //   Rise — 등속으로 곧장 떠오르는 문구. 처치 경험치 "XP N"과 플레이어 레벨 업 "LV UP!"이 쓴다.
    // 색은 호출자가 정한다 — 적 피해는 흰색(EnemyHealth), 플레이어 피격은 붉은색(PlayerHealth), 처치 경험치는 노란색(PlayerExperience), 레벨 업은 게이지의 파란색(PlayerHealth).
    // 카드 선택·일시정지 화면이 떠 있는 동안은 그리지 않는다 (HUD와 같은 조건) — 멈춘 문구가 덮개 위에 남아 화면을 가리지 않게. 시간이 멈춰 있으므로 재개하면 그 자리에서 이어진다.
    public class DamageNumbers : MonoBehaviour
    {
        [SerializeField] PixelUiSkin skin;
        [SerializeField] UpgradeManager upgradeManager;
        [SerializeField] PauseScreen pauseScreen;

        // 낱개 피격 숫자(Pop, 지금은 플레이어 피격)의 움직임 — 위로 미는 충격에 직사각형 안의 무작위 충격(가로·세로 범위 따로)을 더한 처음 속도로 출발해 중력과 지수 감쇠를 받는다.
        // Terraria 전투 텍스트의 지속 피해 숫자(매 프레임 중력을 더하고 ×0.92로 감쇠)에 치명타의 무작위 속도를 더한 것과 같은 꼴이다. 감쇠가 중력에도
        // 걸리므로 낙하는 종단 속도 gravity / damping으로 수렴한다 — 튀어 오른 기세가 꺾인 뒤 물속처럼 일정하게 내려앉는다. 무작위 충격 덕에 한 번에
        // 여럿 떠도 갈라져 포개지지 않는다 (출발점을 흩뿌리는 방식과 방향만 무작위인 방식은 시도 뒤 뺐다). 위치는 프레임마다 적분하지 않고 닫힌 식으로
        // 구한다 (프레임률 무관). 크기 연출(커졌다 작아짐)은 픽셀 UI의 정수 배율 때문에 하지 않는다.
        [System.Serializable]
        public class PopParams
        {
            [Min(0.01f)] public float duration = 0.7f;
            // 위로 미는 충격 — 처음 속도의 세로 성분 (유닛/초).
            [Min(0f)] public float upSpeed = 4f;
            // 무작위 충격의 범위 (유닛/초). 가로는 ±x, 세로는 ±y 안에서 따로 무작위인 벡터를 위로 미는 충격에 더한다 — 가로로 벌어지는 폭과
            // 튀는 높이의 들쭉날쭉함을 따로 정한다. y가 upSpeed 이상이면 아래로 뽑힌 숫자는 솟지 않고 바로 가라앉는다.
            public Vector2 randomSpeed = new Vector2(2f, 1f);
            // 중력 (유닛/초²). 감쇠와 함께 종단 속도 gravity / damping을 정한다.
            [Min(0f)] public float gravity = 3f;
            // 지수 감쇠율 (1/초). 클수록 빨리 멈추고 튀는 거리(≈ 처음 속도 / 감쇠율)가 짧아진다. Terraria는 60fps 기준 매 프레임 ×0.92 ≈ 5/초.
            [Min(0.01f)] public float damping = 6f;
            // 진행률이 이 값을 지나면 끝까지 선형으로 투명해진다.
            [Range(0f, 1f)] public float fadeStart = 0.5f;
        }

        // 처치 경험치·레벨 업(Rise)의 움직임 — 등속으로 곧장 위로 오르며 투명해진다. 중력도 감쇠도 흩뿌림도 없다.
        [System.Serializable]
        public class RiseParams
        {
            [Min(0.01f)] public float duration = 0.8f;
            // 오르는 속도 (유닛/초). 지속 시간과의 곱이 오르는 거리.
            [Min(0f)] public float speed = 1.5f;
            [Range(0f, 1f)] public float fadeStart = 0.5f;
        }

        [SerializeField] PopParams pop = new PopParams();
        [SerializeField] RiseParams rise = new RiseParams();
        // 적 머리 위 누적 피해(Tally)가 마지막 타격 뒤 사라지기까지의 시간(초). 그 안에 또 맞으면 더해지고 시간은 다시 센다.
        [SerializeField, Min(0.01f)] float tallyHoldSeconds = 1.5f;
        // 그 시간의 마지막 이만큼(초) 동안 선형으로 투명해진다 — 갑자기 꺼지지 않게. 다시 맞으면 바로 선명해진다.
        [SerializeField, Min(0f)] float tallyFadeSeconds = 0.5f;

        public enum Motion { Pop, Rise }

        struct Popup
        {
            public Vector3 Origin;
            public string Text;
            public Color Color;
            public float StartTime;
            public Motion Motion;
            // 처음 속도 (유닛/초). Pop은 위 + 직사각형 안 무작위, Rise는 위로 끝까지 이 속도.
            public Vector2 Velocity;
        }

        // 적 하나의 누적 피해 표시. anchor(적)를 따라다니며 맞을 때마다 합계가 늘고, tallyHoldSeconds 동안 맞지 않으면 지워진다 — 다음 타격은 0부터.
        // 적이 죽어 anchor가 파괴되면 바로 지운다 — 죽는 자리에는 사망 이펙트와 처치 경험치가 뜬다.
        class Tally
        {
            public Transform Anchor;
            // anchor 위치에서 글자 아래 가장자리까지 (월드, 위로). 보통 스프라이트 위쪽 가장자리.
            public float TopOffset;
            // 이번 프레임에 그릴 자리. Update가 anchor를 따라간다.
            public Vector3 Position;
            public int Total;
            public Color Color;
            public float LastHitTime;
        }

        static DamageNumbers instance;
        readonly List<Popup> popups = new List<Popup>();
        readonly List<Tally> tallies = new List<Tally>();
        Camera cam;

        void Awake()
        {
            if (instance != null && instance != this)
                throw new System.Exception($"[PickShoot] DamageNumbers가 씬에 둘 이상 존재 ({instance.name}, {name}).");
            if (skin == null)
                throw new System.Exception("[PickShoot] DamageNumbers.skin 미할당. PixelUiSkin 자산(Assets/Data/UI) 필요.");
            skin.Validate();
            if (upgradeManager == null)
                throw new System.Exception("[PickShoot] DamageNumbers.upgradeManager 미할당 (카드 선택 중 숨김 판정에 필요).");
            if (pauseScreen == null)
                throw new System.Exception("[PickShoot] DamageNumbers.pauseScreen 미할당 (일시정지 중 숨김 판정에 필요).");
            cam = Camera.main;
            if (cam == null)
                throw new System.Exception("[PickShoot] MainCamera가 필요합니다 (데미지 숫자 월드→화면 변환용).");
            instance = this;
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        // 피격 데미지 숫자 — 위로 튀어올랐다가 가라앉는다 (Pop). worldPos는 출발점 (보통 피격당한 스프라이트의 위쪽 가장자리).
        public static void Show(Vector3 worldPos, int amount, Color color)
        {
            Show(worldPos, amount.ToString(), color, Motion.Pop);
        }

        // 임의 문구("XP10" 등)를 정해진 움직임으로 띄운다. 글꼴에 없는 문자는 그릴 때 PixelGui가 예외로 알린다.
        public static void Show(Vector3 worldPos, string text, Color color, Motion motion)
        {
            if (instance == null)
                throw new System.Exception("[PickShoot] 씬에 DamageNumbers가 없습니다. 데미지 숫자 표시용 DamageNumbers 오브젝트 필요.");
            instance.Add(worldPos, text, color, motion);
        }

        // 적 머리 위 누적 피해에 amount를 더한다 (GDD §3.3). anchor는 따라갈 적, worldTop은 글자를 놓을 자리 (보통 스프라이트 위쪽 가장자리).
        public static void Accumulate(Transform anchor, Vector3 worldTop, int amount, Color color)
        {
            if (instance == null)
                throw new System.Exception("[PickShoot] 씬에 DamageNumbers가 없습니다. 누적 피해 표시용 DamageNumbers 오브젝트 필요.");
            if (anchor == null)
                throw new System.Exception("[PickShoot] DamageNumbers.Accumulate: anchor가 null.");
            instance.AddToTally(anchor, worldTop, amount, color);
        }

        void AddToTally(Transform anchor, Vector3 worldTop, int amount, Color color)
        {
            Tally tally = null;
            for (int i = 0; i < tallies.Count && tally == null; i++)
                if (tallies[i].Anchor == anchor) tally = tallies[i];
            if (tally == null)
            {
                tally = new Tally { Anchor = anchor };
                tallies.Add(tally);
            }
            tally.TopOffset = worldTop.y - anchor.position.y;
            tally.Position = worldTop;
            tally.Total += amount;
            tally.Color = color;
            tally.LastHitTime = Time.time;
        }

        void Add(Vector3 worldPos, string text, Color color, Motion motion)
        {
            var popup = new Popup { Origin = worldPos, Text = text, Color = color, StartTime = Time.time, Motion = motion };
            switch (motion)
            {
                case Motion.Pop:
                    popup.Velocity = new Vector2(
                        Random.Range(-pop.randomSpeed.x, pop.randomSpeed.x),
                        pop.upSpeed + Random.Range(-pop.randomSpeed.y, pop.randomSpeed.y));
                    break;
                case Motion.Rise:
                    popup.Velocity = Vector2.up * rise.speed;
                    break;
                default:
                    throw new System.Exception($"[PickShoot] DamageNumbers: 알 수 없는 Motion {motion}");
            }
            popups.Add(popup);
        }

        float Duration(Motion motion) => motion == Motion.Rise ? rise.duration : pop.duration;
        float FadeStart(Motion motion) => motion == Motion.Rise ? rise.fadeStart : pop.fadeStart;

        // 출발점에서의 변위 (월드). elapsed는 뜬 뒤 지난 시간(초).
        Vector3 Offset(Popup p, float elapsed)
        {
            switch (p.Motion)
            {
                case Motion.Rise:
                    // 등속. 중력도 감쇠도 없다.
                    return (Vector3)(p.Velocity * elapsed);
                case Motion.Pop:
                {
                    // dv/dt = −k·v + g (g = 중력, 아래로)의 해 v(t) = (v0 − vT)·e^(−kt) + vT  (vT = g/k, 종단 속도)를 적분한
                    // p(t) = (v0 − vT)·(1 − e^(−kt))/k + vT·t.
                    Vector2 terminal = Vector2.down * (pop.gravity / pop.damping);
                    float decayed = (1f - Mathf.Exp(-pop.damping * elapsed)) / pop.damping;
                    return (Vector3)((p.Velocity - terminal) * decayed + terminal * elapsed);
                }
                default:
                    throw new System.Exception($"[PickShoot] DamageNumbers: 알 수 없는 Motion {p.Motion}");
            }
        }

        void Update()
        {
            for (int i = popups.Count - 1; i >= 0; i--)
                if (Time.time - popups[i].StartTime >= Duration(popups[i].Motion)) popups.RemoveAt(i);
            for (int i = tallies.Count - 1; i >= 0; i--)
            {
                var tally = tallies[i];
                // 한동안 맞지 않았거나 적이 죽어 파괴됐으면 지운다.
                if (Time.time - tally.LastHitTime >= tallyHoldSeconds || tally.Anchor == null)
                {
                    tallies.RemoveAt(i);
                    continue;
                }
                tally.Position = tally.Anchor.position + Vector3.up * tally.TopOffset;
            }
        }

        void OnGUI()
        {
            if ((popups.Count == 0 && tallies.Count == 0) || Event.current.type != EventType.Repaint) return;
            if (upgradeManager.IsActive || pauseScreen.IsPaused) return;

            using var canvas = PixelGui.BeginCanvas();
            var prevColor = GUI.color;
            for (int i = 0; i < tallies.Count; i++)
            {
                var tally = tallies[i];
                float remaining = tallyHoldSeconds - (Time.time - tally.LastHitTime);
                var c = tally.Color;
                if (tallyFadeSeconds > 0f) c.a *= Mathf.Clamp01(remaining / tallyFadeSeconds);
                DrawAt(canvas, tally.Total.ToString(), tally.Position, c);
            }
            for (int i = 0; i < popups.Count; i++)
            {
                var p = popups[i];
                float elapsed = Time.time - p.StartTime;
                float t = Mathf.Clamp01(elapsed / Duration(p.Motion));
                var c = p.Color;
                c.a *= 1f - Mathf.InverseLerp(FadeStart(p.Motion), 1f, t);
                DrawAt(canvas, p.Text, p.Origin + Offset(p, elapsed), c);
            }
            GUI.color = prevColor;
        }

        // 문구의 아래쪽 가장자리 가운데를 월드 좌표 worldPos에 맞춰 그린다. 캔버스 좌표는 위가 0.
        void DrawAt(PixelGui.CanvasScope canvas, string text, Vector3 worldPos, Color color)
        {
            Vector3 screenPoint = cam.WorldToScreenPoint(worldPos);
            if (screenPoint.z < 0f) return;
            int textScale = PixelGui.TextScale;
            var font = skin.FontOutlined;
            GUI.color = color;
            Vector2 sp = canvas.FromScreenPoint(screenPoint);
            PixelGui.DrawTextCentered(font, text, Mathf.RoundToInt(sp.x), Mathf.RoundToInt(sp.y) - font.lineHeight * textScale, textScale);
        }
    }
}
