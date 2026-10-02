using System.Collections.Generic;
using UnityEngine;

namespace PickShoot
{
    // 인게임 HUD 단일 창구. PixelUiSkin(Kenney Interface 시트)으로 OnGUI에 그린다.
    //   체력: 좌상단 패널에 칸 단위 핍으로 자세히, 플레이어 머리 위에는 비율만 보이는 작은 게이지.
    //   스코어: 우상단에 러닝 누적 총점 (GDD §3.3). 카드 화면도 같은 자리에 같은 모양으로 그린다 (DrawScoreLine).
    //   대시: 쿨다운 중에만 머리 위 체력 게이지 위쪽에 시계 방향으로 차오르는 작은 원판 (GDD §3.5).
    //   무기: 링에서 조준 기준 왼쪽에 있는 슬롯은 화면 하단 왼쪽 구석, 오른쪽 슬롯은 하단 오른쪽 구석에 거울상으로.
    //         패널 배경 없이 슬롯마다 한 줄(스프라이트 · 레벨 · 탄창)만 바닥 위에 바로 그린다. 스프라이트 밑에만 작은 판을 깐다.
    //   웨이브: 상단 중앙 배너 + 남은 시간 바.
    //   커서: 하드웨어 커서를 숨기고 그 자리에 크로스헤어 — 중심점(조준점) + 모든 자루의 탄창 비율 평균을 보여주는 링.
    //         링은 사격마다 벌어졌다가 서서히 되돌아온다 (시트에 링 조각이 없어 CrosshairTexture가 런타임에 텍셀 단위로 그린다).
    //         월드 위에 뜨는 표시라 아래 HUD 배율이 아니라 월드 텍셀 배율(PixelGui.PanelScale)로 띄운다.
    // 레이아웃은 텍셀 단위로 정의하고 HUD 배율(PixelGui.TextScale — 업그레이드 카드의 글자 배율과 같은 값, 패널 배율의 절반)을
    // 곱해 기준 캔버스 픽셀로 바꾼다. 카드 패널보다 얇은 테두리라 게임 화면을 덜 가린다. 캔버스는 월드 카메라와 같은 규칙
    // (화면 세로 = 270텍셀)으로 보이므로 해상도·종횡비가 달라도 HUD가 화면에서 차지하는 비율은 월드 스프라이트처럼 그대로다.
    // 다른 OnGUI(피격 플래시·데스 화면)보다 뒤에 깔리도록 GUI.depth를 크게 두고, 업그레이드 카드·일시정지 화면이 떠 있는 동안은 아예 숨긴다.
    public class GameHud : MonoBehaviour
    {
        [SerializeField] PixelUiSkin skin;
        [SerializeField] PlayerHealth playerHealth;
        [SerializeField] PlayerWeapons playerWeapons;
        [SerializeField] WaveManager waveManager;
        [SerializeField] UpgradeManager upgradeManager;
        [SerializeField] PauseScreen pauseScreen;
        [SerializeField] RunScore runScore;
        // 소진한 탄약 핍에 곱하는 색. 남은 탄약은 원래 색 그대로. 기본값은 반투명 검정 — 핍이 탄창에 난 빈 자리(어두운 실루엣)로 보인다.
        // 패널 없이 바닥 위에 바로 그리므로, 원래 색을 흐리게만 하면 밝은 바닥에 묻혀 최대치가 읽히지 않는다.
        [SerializeField] Color spentPipColor = new Color(0f, 0f, 0f, 0.35f);
        // 무기 스프라이트 밑에 까는 작은 판의 색. 기본값은 시트 회색 패널의 바탕색과 시트 외곽선 색(#47324B).
        [SerializeField] Color weaponPlateFillColor = new Color32(153, 154, 196, 255);
        [SerializeField] Color weaponPlateOutlineColor = new Color32(71, 50, 75, 255);
        // 탄약이 부족해 쏠 수 없는 무기 스프라이트 위 X 아이콘의 깜빡임 주기 (초, 실시간).
        [SerializeField, Min(0.01f)] float emptyBlinkInterval = 0.3f;

        [Header("Crosshair")]
        // 커서 자리의 크로스헤어 = 중심점 + 탄약 링. 치수는 텍셀 단위이며 다른 HUD(TextScale)와 달리 월드 텍셀 배율(PixelGui.PanelScale)로
        // 확대된다 — HUD 배율은 텍셀이 월드 스프라이트 픽셀의 절반이라 링이 매끈한 원으로 보여 픽셀아트와 겉돈다.
        // 반경은 중심 텍셀에서 센 텍셀 수 (CrosshairTexture 참고). 중심점은 링의 채움 색을 쓴다. 윤곽선은 링에도 중심점에도 두르지 않는다 —
        // 시트 외곽선 색(#47324B)으로 링 안팎을 둘러 봤으나 이 텍셀 크기에서는 선이 띠만큼 두꺼워져 조준점이 무거워 보였다.
        [SerializeField, Min(2)] int cursorRingRadiusTexels = 6;
        [SerializeField, Min(1)] int cursorRingThicknessTexels = 2;
        [SerializeField] Color cursorRingFillColor = Color.white;
        [SerializeField] Color cursorRingEmptyColor = new Color(0.25f, 0.22f, 0.2f, 0.6f);
        // 중심점 반경. 0이면 중심 텍셀 하나, 1이면 3×3텍셀.
        [SerializeField, Min(0)] int cursorDotRadiusTexels = 0;
        // 사격 한 번에 링 반경이 벌어지는 양: 쏜 무기의 반동 배율(WeaponData.RecoilStrength) 1당 텍셀 수. 무거운 무기일수록 크게 벌어진다.
        [SerializeField, Min(0f)] float cursorRingKickTexels = 2f;
        // 벌어진 링이 되돌아오는 속도 (지수 감쇠율, 1/초). 무기 반동(PlayerWeapons.recoilReturnSpeed)보다 느리게 둬 서서히 조여든다.
        [SerializeField, Min(0.01f)] float cursorRingReturnSpeed = 6f;

        [Header("Player Gauge")]
        // 플레이어 머리 위 체력 게이지의 색. 테두리 없이 바탕 위에 남은 비율만큼 채운다. 채움 기본값은 시트 게이지 채움 조각의 붉은색.
        [SerializeField] Color playerGaugeBackColor = Color.black;
        [SerializeField] Color playerGaugeFillColor = new Color32(221, 103, 76, 255);
        // 머리 위 대시 쿨다운 원판의 채움 색. 바탕은 체력 게이지와 같은 playerGaugeBackColor.
        [SerializeField] Color dashDialFillColor = Color.white;

        // IMGUI는 depth가 작을수록 위에 그린다. 다른 스크립트는 기본값 0이므로 HUD는 그 뒤에 깔린다.
        const int HudGuiDepth = 10;
        const int MarginTexels = 8;      // 화면 가장자리 → 패널·무기 줄
        const int PaddingTexels = 8;     // 패널 테두리 → 내용
        const int GapTexels = 4;         // 행 사이·이웃 요소 사이
        const int ColumnGapTexels = 8;   // 열 사이
        // 체력 핍은 16텍셀 타일이지만 보이는 칸이 10텍셀이라 타일 간격보다 촘촘히 겹쳐 놓는다. 여백 2텍셀.
        const int HpPipStrideTexels = 12;
        // 무기 줄 사이의 세로 간격(줄 위 가장자리 → 다음 줄 위 가장자리). 글자·핍 타일(16텍셀)보다 조금 넓다.
        // 무기 스프라이트 타일(24텍셀)은 이보다 크지만 위아래가 투명 여백이라 이웃 줄과 겹쳐도 그림끼리는 닿지 않는다.
        // 그림은 그 밑의 판(WeaponPlate*) 안에 들어간다.
        const int WeaponRowPitchTexels = 20;
        // 무기 스프라이트 밑에 까는 판. 무기 그림은 어두운 보라 계열이라 바닥·울타리·적 위에 바로 놓으면 묻히기 쉬워 아이콘에만
        // 작은 배경을 준다. 시트 조각 없이 단색 사각형으로 그린다 — 시트의 작은 판 조각은 흰 테두리와 리벳이 총 그림과 겹쳐 어수선했다.
        // 외곽선 두께는 글꼴·핍의 외곽선과 같은 2텍셀, 네 귀는 1텍셀씩 깎는다. 크기는 가장 큰 그림(저격소총 22×12텍셀)에 외곽선과
        // 여백 1텍셀을 두른 값이고, 높이가 줄 간격(WeaponRowPitchTexels)보다 작아 이웃 줄의 판과 2텍셀 떨어진다.
        const int WeaponPlateWidthTexels = 28;
        const int WeaponPlateHeightTexels = 18;
        const int WeaponPlateOutlineTexels = 2;
        // 탄약 핍(시트 16텍셀 타일)에서 실제 그림이 있는 가로 구간: 왼쪽 여백 4텍셀 뒤로 8텍셀 (외곽선 2 + 몸통 4 + 외곽선 2).
        const int AmmoPipInsetTexels = 4;
        const int AmmoPipWidthTexels = 8;
        // 이웃 핍 사이의 간격. 몸통 4 + 외곽선 2 = 6이면 이웃끼리 외곽선이 정확히 포개져, 낱발이 빈틈없이 꽂힌 탄창 한 덩이로 보인다.
        // 탄창 크기와 무관하게 늘 이 간격이다 — 탄창이 큰 만큼 줄이 길어질 뿐 핍이 촘촘해지거나 간격이 들쭉날쭉해지지 않는다.
        // 레벨 상한(Weapon.MaxLevel)과 무기별 탄창 가산(WeaponData.LevelMaxAmmoAdd)이 탄창 크기를 묶으므로 줄 길이도 그 안에 머문다
        // (표준 +2 기준 가장 긴 줄은 SMG Lv9 = 34발 = 214텍셀).
        const int AmmoPipStrideTexels = 6;
        // 게이지 채움 조각의 둥근 양 끝이 프레임 안쪽에서 시작하는 위치. 채움 폭은 이 안쪽 구간에 비례한다.
        const int BarFillInsetTexels = 3;
        // 플레이어 머리 위 체력 게이지. 시트 조각 없이 단색 사각형 두 장(바탕 + 채움)으로 그린다 — 시트의 게이지 프레임은 선이 아주
        // 잘아(이 크기에서 1080p 기준 1px) 해상도에 따라 선이 빠지거나 굵기가 어긋나 보였다. 선이 없으니 치수를 전부 캐릭터의 월드
        // 크기에 비례시킬 수 있다 — 해상도·카메라 배율과 무관하게 캐릭터 대비 같은 비율. 캐릭터를 따라 움직이므로 캔버스에 싣지 않고
        // 화면 픽셀에 맞춰 그린다. 아래 값은 월드 텍셀(스프라이트 픽셀, PPU 16) 단위.
        //   Width: 폭. 캐릭터의 보이는 폭이 약 21텍셀이라 그보다 조금 좁게.
        //   Thickness: 두께.
        //   SpriteTopPadding: 캐릭터 시트가 타일 위쪽에 두는 투명 여백 (걷기 5, 정지 6). 보이는 머리 꼭대기를 잡는 기준.
        //   Gap: 보이는 머리 꼭대기에서 게이지 아래 가장자리까지.
        const int PlayerGaugeWidthTexels = 16;
        const int PlayerGaugeThicknessTexels = 3;
        const int PlayerSpriteTopPaddingTexels = 5;
        const int PlayerGaugeGapTexels = 2;
        // 머리 위 대시 쿨다운 원판. 체력 게이지와 같이 캔버스 밖에서 월드 텍셀 크기로 그린다 (RadialFillTexture가 텍셀 단위로 래스터라이즈).
        // 체력 게이지가 보이든 말든(풀피면 숨는다) 늘 게이지 자리 위쪽 같은 곳에 뜬다 — 피격 순간 표시가 튀어 오르지 않게.
        //   Radius: 중심 텍셀에서 센 반경. 3이면 지름 7텍셀.
        //   Gap: 체력 게이지 위 가장자리에서 원판 아래 가장자리까지.
        const int DashDialRadiusTexels = 3;
        const int DashDialGapTexels = 2;

        PlayerMovement playerMovement;
        SpriteRenderer playerSprite;
        Camera cam;
        readonly List<int> sideSlots = new List<int>(PlayerWeapons.MaxSlots);
        readonly CrosshairTexture crosshair = new CrosshairTexture();
        readonly RadialFillTexture dashDial = new RadialFillTexture();
        // 크로스헤어 링이 사격으로 벌어져 있는 양 (텍셀, 0 = 제자리). 발사 시 세팅되고 지수 감쇠로 복귀.
        float cursorRingKick;
        // 무기 줄의 레벨 열 폭(텍셀). 레벨은 한 자리(1~Weapon.MaxLevel)뿐이라 "LV n" 중 가장 넓은 것에 맞춰 Awake에서 한 번 잰다 —
        // 글꼴이 가변폭이라 숫자마다 폭이 다르다.
        int levelColumnTexels;

        // HUD(크로스헤어 포함)를 그리는 조건.
        //   플레이어는 사망 시 파괴된다. 그 뒤는 데스 화면이 이어받으므로 HUD를 걷는다.
        //   카드 선택·일시정지 화면은 게임을 멈추고 화면을 덮는다. 그 위에 HUD가 보이면 어색하므로 숨긴다.
        bool HudVisible => playerHealth != null && !upgradeManager.IsActive && !pauseScreen.IsPaused;

        void Awake()
        {
            if (skin == null)
                throw new System.Exception("[PickShoot] GameHud.skin 미할당. PixelUiSkin 자산(Assets/Data/UI) 필요.");
            if (playerHealth == null)
                throw new System.Exception("[PickShoot] GameHud.playerHealth 미할당.");
            if (playerWeapons == null)
                throw new System.Exception("[PickShoot] GameHud.playerWeapons 미할당.");
            if (waveManager == null)
                throw new System.Exception("[PickShoot] GameHud.waveManager 미할당.");
            if (upgradeManager == null)
                throw new System.Exception("[PickShoot] GameHud.upgradeManager 미할당 (카드 선택 중 HUD 숨김 판정에 필요).");
            if (pauseScreen == null)
                throw new System.Exception("[PickShoot] GameHud.pauseScreen 미할당 (일시정지 중 HUD 숨김 판정에 필요).");
            if (runScore == null)
                throw new System.Exception("[PickShoot] GameHud.runScore 미할당 (우상단 스코어 표기에 필요).");
            playerMovement = playerHealth.GetComponent<PlayerMovement>();
            if (playerMovement == null)
                throw new System.Exception("[PickShoot] GameHud: 플레이어에 PlayerMovement 없음 (대시 쿨다운 표시에 필요).");
            playerSprite = playerHealth.GetComponent<SpriteRenderer>();
            if (playerSprite == null)
                throw new System.Exception("[PickShoot] GameHud: 플레이어에 SpriteRenderer 없음 (머리 위 게이지 위치 기준).");
            cam = Camera.main;
            if (cam == null)
                throw new System.Exception("[PickShoot] MainCamera가 필요합니다 (머리 위 게이지 월드→화면 변환용).");
            skin.Validate();
            for (int level = 1; level <= Weapon.MaxLevel; level++)
                levelColumnTexels = Mathf.Max(levelColumnTexels, PixelGui.MeasureText(skin.FontPlain, LevelLabel(level), 1));
            playerWeapons.Fired += OnWeaponFired;
        }

        void OnDisable()
        {
            Cursor.visible = true;
        }

        void OnDestroy()
        {
            if (playerWeapons != null) playerWeapons.Fired -= OnWeaponFired;
            crosshair.Dispose();
            dashDial.Dispose();
        }

        void Update()
        {
            // 크로스헤어가 커서를 대신하는 동안만 하드웨어 커서를 숨긴다. HUD가 걷히는 카드 선택·일시정지·데스 화면에서는 커서가 돌아온다
            // (카드는 클릭으로 고른다). 상태가 바뀌는 순간만 잡지 않고 매 프레임 맞춘다 — 에디터는 Esc로 커서를 되살린다.
            Cursor.visible = !HudVisible;

            // 표시 반경은 텍셀 단위로 반올림하므로 0.5 미만은 이미 제자리로 보인다.
            cursorRingKick = cursorRingKick < 0.5f ? 0f : cursorRingKick * Mathf.Exp(-cursorRingReturnSpeed * Time.deltaTime);
        }

        // 이미 더 벌어져 있으면 그대로 둔다 — 여러 자루 연사 중에 가벼운 무기가 링을 도로 좁히지 않게.
        void OnWeaponFired(Weapon weapon)
        {
            cursorRingKick = Mathf.Max(cursorRingKick, weapon.Data.RecoilStrength * cursorRingKickTexels);
        }

        void OnGUI()
        {
            GUI.depth = HudGuiDepth;
            if (Event.current.type != EventType.Repaint) return;
            if (!HudVisible) return;

            // 머리 위 표시(체력 게이지·대시 쿨다운)만 캔버스 밖에서 화면 픽셀로 그린다. 월드에 붙은 표시라 패널 밑에 깔리도록 먼저.
            DrawPlayerOverhead();

            int hudScale = PixelGui.TextScale;
            using var canvas = PixelGui.BeginCanvas();
            DrawHealthPanel(hudScale);
            DrawScoreLine(skin, canvas, runScore.Total);
            DrawWeaponRows(canvas, hudScale, leftSide: true);
            DrawWeaponRows(canvas, hudScale, leftSide: false);
            DrawWaveBanner(canvas, hudScale);
            // 커서는 패널 위로 지나갈 수 있으므로 마지막에 그려 항상 패널 위에 보이게 한다. 월드 위에 뜨는 표시라 월드 텍셀 배율로 그린다.
            DrawCrosshair(PixelGui.PanelScale);
        }

        // 좌상단 회색 패널: "HP" + 체력 핍 (왼쪽부터 남은 체력만큼 붉은 칸, 나머지 빈 칸).
        void DrawHealthPanel(int s)
        {
            var font = skin.FontPlain;
            int tile = skin.TileSize * s;
            int lineH = font.lineHeight * s;
            int pad = PaddingTexels * s;
            int colGap = ColumnGapTexels * s;
            int margin = MarginTexels * s;
            int stride = HpPipStrideTexels * s;

            int labelW = PixelGui.MeasureText(font, "HP", s);
            int contentW = labelW + colGap + PipsWidth(playerHealth.Max, HpPipStrideTexels, s);
            int contentH = lineH;
            int panelW = CeilToMultiple(contentW + 2 * pad, tile);
            int panelH = CeilToMultiple(contentH + 2 * pad, tile);
            var panel = new Rect(margin, margin, panelW, panelH);
            PixelGui.DrawNineSlice(skin.PanelGrey, panel, skin.TileSize, s);

            // 타일 배수 올림으로 생긴 여분은 상하좌우 균등 분배.
            int x0 = (int)panel.x + (panelW - contentW) / 2;
            int y = (int)panel.y + (panelH - contentH) / 2;
            PixelGui.DrawText(font, "HP", x0, y, s);
            int x = x0 + labelW + colGap;
            for (int i = 0; i < playerHealth.Max; i++)
            {
                var cell = i < playerHealth.Current ? skin.BarCellRed : skin.BarCellEmpty;
                PixelGui.DrawSprite(cell, new Rect(x + i * stride, y, tile, tile));
            }
        }

        // 우상단 한 줄: "SCORE" · 러닝 누적 총점, 오른쪽 끝 정렬. 좌상단 HP 줄과 같은 y·같은 결(패널 없음, 글자 배율). 정적인 이유 — 카드 화면은
        // HUD를 숨기므로 UpgradeManager가 같은 자리에 같은 모양으로 다시 그린다. 화면이 바뀌어도 숫자가 제자리에 있어야 리롤 문턱과 견주기 쉽다.
        public static void DrawScoreLine(PixelUiSkin skin, PixelGui.CanvasScope canvas, int score)
        {
            int s = PixelGui.TextScale;
            int margin = MarginTexels * s;
            PixelGui.DrawTextRight(skin.FontPlain, $"SCORE {score}", canvas.Width - margin, margin, s);
        }

        // 플레이어 스프라이트 위에 체력 비율 게이지와 대시 쿨다운 원판을 가운데 정렬로 띄운다 (숫자 없음).
        // 게이지는 풀피면, 원판은 대시가 준비되면 그리지 않는다 — 다쳤을 때·쿨다운 중에만 눈에 띄게.
        // 캔버스 밖에서 호출한다 — 좌표·크기는 화면 픽셀.
        void DrawPlayerOverhead()
        {
            bool showGauge = playerHealth.Current < playerHealth.Max;
            bool showDashDial = !playerMovement.DashReady;
            if (!showGauge && !showDashDial) return;

            var b = playerSprite.bounds;
            float unitsPerTexel = 1f / playerSprite.sprite.pixelsPerUnit;
            // 앵커 = 보이는 머리 꼭대기(타일 위 가장자리에서 투명 여백만큼 아래) + gap. 여기가 게이지의 아래 가장자리.
            float anchorY = b.max.y - (PlayerSpriteTopPaddingTexels - PlayerGaugeGapTexels) * unitsPerTexel;
            var anchor = new Vector3(b.center.x, anchorY, b.center.z);
            Vector3 sp = cam.WorldToScreenPoint(anchor);
            if (sp.z < 0f) return;
            // 월드 텍셀 1개가 차지하는 화면 픽셀 수 (직교 카메라라 위치와 무관).
            float texelPx = cam.WorldToScreenPoint(anchor + Vector3.right * unitsPerTexel).x - sp.x;

            // 화면 픽셀로 반올림. 폭은 짝수로 맞춰 캐릭터 중심선 좌우가 같게 한다.
            int barW = Mathf.Max(2, Mathf.RoundToInt(PlayerGaugeWidthTexels * texelPx / 2f) * 2);
            int barH = Mathf.Max(1, Mathf.RoundToInt(PlayerGaugeThicknessTexels * texelPx));
            // GUI 좌표는 위가 0.
            int centerX = Mathf.RoundToInt(sp.x);
            int barY = Mathf.RoundToInt(Screen.height - sp.y) - barH;

            if (showGauge)
            {
                int x = centerX - barW / 2;
                int fillW = Mathf.RoundToInt(barW * (float)playerHealth.Current / playerHealth.Max);
                PixelGui.FillRect(new Rect(x, barY, barW, barH), playerGaugeBackColor);
                PixelGui.FillRect(new Rect(x, barY, fillW, barH), playerGaugeFillColor);
            }

            if (showDashDial)
            {
                dashDial.Refresh(DashDialRadiusTexels, playerMovement.DashCooldownProgress, dashDialFillColor, playerGaugeBackColor);
                int size = Mathf.RoundToInt(dashDial.SizeTexels * texelPx);
                int y = barY - Mathf.RoundToInt(DashDialGapTexels * texelPx) - size;
                GUI.DrawTexture(new Rect(centerX - size / 2, y, size, size), dashDial.Texture, ScaleMode.StretchToFill, true);
            }
        }

        // 프레임 + 비율만큼 잘라낸 붉은 채움. 채움 조각은 프레임과 같은 자리에 그리면 안쪽에 딱 맞고, 오른쪽부터 줄어든다.
        void DrawGauge(Rect bar, float ratio, int s)
        {
            PixelGui.DrawBarStrip(skin.BarFrame, bar, skin.TileSize, s);
            int inset = BarFillInsetTexels * s;
            int fillW = inset + Mathf.RoundToInt((bar.width - 2 * inset) * Mathf.Clamp01(ratio));
            PixelGui.DrawBarStrip(skin.BarFillRed, bar, skin.TileSize, s, fillW);
        }

        // 하단 한쪽 구석: 링의 해당 쪽에 있는 슬롯만, 슬롯마다 한 줄. 줄 전체를 감싸는 패널 배경은 두지 않는다 — 글꼴·핍은 시트의
        // 어두운 외곽선을 두르고 있어 바닥 위에서 그대로 읽히고, 전투 공간을 가리는 면적이 그림 자체뿐이다.
        // 무기 스프라이트만 작은 판(DrawWeaponPlate) 위에 얹는다.
        // 한 줄은 화면 가장자리에서 안쪽으로 무기 스프라이트(탄약 부족 표시가 그 위에 겹침) · 레벨 · 탄창 순이다. 오른쪽 구석은 왼쪽의
        // 거울상 — 스프라이트도 좌우 반전해 양쪽 모두 총구가 탄창 쪽(화면 안쪽)을 향한다.
        // 무기 이름과 슬롯 번호는 카드에서만 쓰고 HUD에는 두지 않는다 — 종류는 스프라이트로 구분되고, 이름 줄이 없어야
        // 12슬롯(한쪽 6줄)까지 늘어도 구석에 낮게 머문다. 그쪽의 첫 슬롯이 화면 아래 여백선에 닿고, 이후 슬롯은 슬롯 순서대로
        // 그 위에 쌓인다 — 슬롯의 좌우는 고정(짝수 인덱스는 왼쪽, 홀수는 오른쪽)이라 무기가 늘어도 기존 줄은 제자리에 있다.
        // 해당 쪽에 슬롯이 없으면 아무것도 그리지 않는다.
        void DrawWeaponRows(PixelGui.CanvasScope canvas, int s, bool leftSide)
        {
            var slots = playerWeapons.Slots;
            sideSlots.Clear();
            for (int i = 0; i < slots.Count; i++)
                if (playerWeapons.IsOnLeftOfAim(i) == leftSide) sideSlots.Add(i);
            if (sideSlots.Count == 0) return;

            var font = skin.FontPlain;
            int tile = skin.TileSize * s;
            int gap = GapTexels * s;
            int rowPitch = WeaponRowPitchTexels * s;
            int margin = MarginTexels * s;

            // 열 폭은 그쪽 모든 줄에서 공통 — 스프라이트 크기가 달라도 레벨과 탄창이 세로로 나란히 정렬되도록.
            // 아이콘 열은 판·무기 스프라이트·그 위에 겹쳐 깜빡이는 탄약 부족 X(타일 한 칸) 중 가장 넓은 쪽. X에 따로 자리를 주지 않아
            // 깜빡여도 레벨·탄창이 움직이지 않는다. 레벨 열은 고정 폭(levelColumnTexels)이라 레벨이 올라도 탄창이 밀리지 않는다.
            int plateW = WeaponPlateWidthTexels * s;
            int plateH = WeaponPlateHeightTexels * s;
            int iconW = Mathf.Max(tile, plateW);
            for (int k = 0; k < sideSlots.Count; k++)
                iconW = Mathf.Max(iconW, Mathf.RoundToInt(slots[sideSlots[k]].Data.WeaponSprite.rect.width) * s);

            // 가로 배치는 여백선(edge)에서 화면 안쪽으로 잰 거리로 다룬다 (InwardX). 왼쪽 구석은 +x, 오른쪽 구석은 -x로 자란다.
            int edge = leftSide ? margin : canvas.Width - margin;
            int levelInset = iconW + gap;
            int pipsInset = levelInset + levelColumnTexels * s + gap;
            int y = canvas.Height - margin - tile;

            bool blinkOn = Mathf.FloorToInt(Time.unscaledTime / emptyBlinkInterval) % 2 == 0;
            for (int k = 0; k < sideSlots.Count; k++)
            {
                var w = slots[sideSlots[k]];

                // 판과 스프라이트는 아이콘 열 가로 가운데·줄(타일 높이) 세로 가운데. 타일보다 크면 줄 위아래로 고르게 넘친다.
                DrawWeaponPlate(new Rect(InwardX(edge, leftSide, (iconW - plateW) / 2, plateW), y + (tile - plateH) / 2, plateW, plateH), s);
                var sprite = w.Data.WeaponSprite;
                int sw = Mathf.RoundToInt(sprite.rect.width) * s;
                int sh = Mathf.RoundToInt(sprite.rect.height) * s;
                var spriteRect = new Rect(InwardX(edge, leftSide, (iconW - sw) / 2, sw), y + (tile - sh) / 2, sw, sh);
                if (leftSide) PixelGui.DrawSprite(sprite, spriteRect);
                else PixelGui.DrawSpriteMirrored(sprite, spriteRect);

                // 탄약이 0이라 쏠 수 없으면 무기 스프라이트 위에 붉은 X를 깜빡여 회수를 재촉한다.
                // (사격분에 못 미치는 잔량은 남은 만큼 발사되므로 부족 표시 대상이 아니다 — Weapons §4)
                if (w.CurrentAmmo <= 0 && blinkOn)
                    PixelGui.DrawSprite(skin.IconCross, new Rect(InwardX(edge, leftSide, (iconW - tile) / 2, tile), y, tile, tile));

                // 레벨은 열 안에서 스프라이트 쪽 끝에 붙인다 (왼쪽 구석은 왼쪽 정렬, 오른쪽 구석은 오른쪽 정렬).
                string level = LevelLabel(w.Level);
                PixelGui.DrawText(font, level, InwardX(edge, leftSide, levelInset, PixelGui.MeasureText(font, level, s)), y, s);

                DrawAmmoPips(w, InwardX(edge, leftSide, pipsInset, 0), leftSide, y, s);
                y -= rowPitch;
            }
        }

        // 여백선 edge에서 화면 안쪽으로 inset만큼 떨어져 시작하는 폭 width짜리 요소의 왼쪽 x.
        // 오른쪽 구석(leftSide = false)에서는 안쪽이 -x 방향이라 요소의 오른쪽 끝이 edge - inset에 온다.
        static int InwardX(int edge, bool leftSide, int inset, int width)
        {
            return leftSide ? edge + inset : edge - inset - width;
        }

        // 무기 스프라이트 밑의 판: 네 귀를 1텍셀씩 깎은 외곽선 색 사각형 위에 바탕색을 채운다.
        // 귀를 깎은 모양은 가로로 긴 사각형과 세로로 긴 사각형을 겹쳐 만든다 — 겹쳐 그리므로 비정수 배율에서도 조각 사이에 틈이 없다.
        void DrawWeaponPlate(Rect r, int s)
        {
            int o = WeaponPlateOutlineTexels * s;
            PixelGui.FillRect(new Rect(r.x + s, r.y, r.width - 2 * s, r.height), weaponPlateOutlineColor);
            PixelGui.FillRect(new Rect(r.x, r.y + s, r.width, r.height - 2 * s), weaponPlateOutlineColor);
            PixelGui.FillRect(new Rect(r.x + o, r.y + o, r.width - 2 * o, r.height - 2 * o), weaponPlateFillColor);
        }

        // 탄창: 탄환 한 발이 핍 하나. 핍을 외곽선이 포개지는 간격으로 붙여 한 덩이로 그린다. 레벨 쪽 끝(rowX)부터 남은 탄약만큼은
        // 원래 색, 나머지는 spentPipColor를 곱한 어두운 빈 자리 — 최대치와 잔량이 한눈에 보인다. 쏠수록 화면 안쪽 끝부터 빈다.
        // growRight: 줄이 rowX에서 오른쪽으로 자라는가 (왼쪽 구석). 아니면 왼쪽으로 자란다.
        // 간격은 탄창 크기와 무관하게 AmmoPipStrideTexels로 일정하다.
        // 이웃끼리 겹치므로 남은 탄약이 빈 자리 위에 오도록 줄 끝(index가 큰 쪽)부터 그린다 — 경계에서 남은 핍의 외곽선이 온전히 보인다.
        void DrawAmmoPips(Weapon w, int rowX, bool growRight, int y, int s)
        {
            int tile = skin.TileSize * s;
            for (int i = w.MaxAmmo - 1; i >= 0; i--)
            {
                int offset = i * AmmoPipStrideTexels * s;
                // 핍 그림(타일 안 AmmoPipInsetTexels부터 AmmoPipWidthTexels 폭)의 왼쪽 x → 타일의 왼쪽 x.
                int pipX = growRight ? rowX + offset : rowX - offset - AmmoPipWidthTexels * s;
                GUI.color = i < w.CurrentAmmo ? Color.white : spentPipColor;
                PixelGui.DrawSprite(skin.IconBulletPlain, new Rect(pipX - AmmoPipInsetTexels * s, y, tile, tile));
            }
            GUI.color = Color.white;
        }

        // 상단 중앙 붉은 배너 "WAVE N" + 그 아래 남은 시간 게이지. 게이지 위에는 단계별 문구(READY / 남은 초 / CLEAR).
        void DrawWaveBanner(PixelGui.CanvasScope canvas, int s)
        {
            if (waveManager.Phase == WavePhase.Idle) return;

            var font = skin.FontOutlined;
            int tile = skin.TileSize * s;
            int lineH = font.lineHeight * s;
            int gap = GapTexels * s;

            string title = $"WAVE {waveManager.CurrentWave}";
            int titleW = PixelGui.MeasureText(font, title, s);
            int bannerW = CeilToMultiple(titleW + 2 * tile, tile);
            int bannerH = 2 * tile;
            int centerX = canvas.Width / 2;
            var banner = new Rect(centerX - bannerW / 2, MarginTexels * s, bannerW, bannerH);
            PixelGui.DrawNineSlice(skin.PanelRed, banner, skin.TileSize, s);
            PixelGui.DrawTextCentered(font, title, centerX, (int)banner.y + (bannerH - lineH) / 2, s);

            var bar = new Rect(banner.x, banner.yMax + gap, bannerW, tile);
            DrawGauge(bar, waveManager.WaveTimeRemaining / waveManager.WaveDuration, s);

            string status = waveManager.Phase switch
            {
                WavePhase.Countdown => "READY",
                WavePhase.Active => Mathf.CeilToInt(waveManager.WaveTimeRemaining).ToString(),
                WavePhase.Cleared => "CLEAR",
                _ => throw new System.Exception($"[PickShoot] GameHud: 알 수 없는 WavePhase {waveManager.Phase}"),
            };
            PixelGui.DrawTextCentered(font, status, centerX, (int)bar.y, s);
        }

        // 커서 자리에 크로스헤어(중심점 + 전체 탄창 비율 링). 텍스처는 상태가 바뀔 때만 다시 그린다. s는 텍셀 1개의 캔버스 픽셀 수.
        // 사격으로 벌어진 양은 텍셀 단위로 반올림해 반경에 더한다 — 링이 늘 온전한 텍셀 원 모양으로 한 칸씩 조여든다
        // (소수 반경을 그대로 쓰면 되돌아오는 동안 둘레 텍셀이 제각각 깜빡인다).
        void DrawCrosshair(int s)
        {
            int radius = cursorRingRadiusTexels + Mathf.RoundToInt(cursorRingKick);
            crosshair.Refresh(playerWeapons.Slots, radius, cursorRingThicknessTexels, cursorDotRadiusTexels, cursorRingFillColor, cursorRingEmptyColor);

            int size = crosshair.SizeTexels * s;
            // 캔버스가 열린 동안 Repaint 이벤트의 mousePosition은 캔버스 좌표(위가 0)라 그대로 중심으로 쓴다.
            // 한 변은 홀수 텍셀이지만 배율이 짝수라 반은 정수 — 중심 텍셀의 한가운데가 커서 위치에 온다.
            Vector2 m = Event.current.mousePosition;
            var rect = new Rect(Mathf.RoundToInt(m.x) - size / 2, Mathf.RoundToInt(m.y) - size / 2, size, size);
            GUI.DrawTexture(rect, crosshair.Texture, ScaleMode.StretchToFill, true);
        }

        // 글꼴에 있는 문자(A-Z 0-9 % + - 공백)만 써야 한다. 없는 문자는 PixelGui가 예외로 알린다.
        static string LevelLabel(int level) => $"LV {level}";

        // 핍 n개가 차지하는 폭. 마지막 핍은 타일 전체 폭으로 센다.
        int PipsWidth(int count, int strideTexels, int s)
        {
            if (count <= 0) return 0;
            return (count - 1) * strideTexels * s + skin.TileSize * s;
        }

        static int CeilToMultiple(int value, int multiple)
        {
            return ((value + multiple - 1) / multiple) * multiple;
        }
    }
}
