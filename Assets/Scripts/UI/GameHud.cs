using System.Collections.Generic;
using UnityEngine;

namespace PickShoot
{
    // 인게임 HUD 단일 창구. PixelUiSkin(Kenney Interface 시트)으로 OnGUI에 그린다.
    //   체력: 좌상단 패널에 칸 단위 핍으로 자세히, 플레이어 머리 위에는 비율만 보이는 작은 게이지.
    //   무기: 링에서 조준 기준 왼쪽에 있는 슬롯은 화면 하단 왼쪽 패널, 오른쪽 슬롯은 하단 오른쪽 패널.
    //   웨이브: 상단 중앙 배너 + 남은 시간 바.
    //   커서: 하드웨어 커서를 숨기고 그 자리에 크로스헤어 — 중심점(조준점) + 모든 자루의 탄창 비율 평균을 보여주는 링.
    //         링은 사격마다 벌어졌다가 서서히 되돌아온다 (시트에 링 조각이 없어 CrosshairTexture가 런타임에 텍셀 단위로 그린다).
    // 레이아웃은 텍셀 단위로 정의하고 HUD 배율(PixelGui.TextScale — 업그레이드 카드의 글자 배율과 같은 값, 패널 배율의 절반)을
    // 곱해 기준 캔버스 픽셀로 바꾼다. 카드 패널보다 얇은 테두리라 게임 화면을 덜 가린다. 캔버스는 월드 카메라와 같은 규칙
    // (화면 세로 = 270텍셀)으로 보이므로 해상도·종횡비가 달라도 HUD가 화면에서 차지하는 비율은 월드 스프라이트처럼 그대로다.
    // 다른 OnGUI(피격 플래시·데스 화면)보다 뒤에 깔리도록 GUI.depth를 크게 두고, 업그레이드 카드가 떠 있는 동안은 아예 숨긴다.
    public class GameHud : MonoBehaviour
    {
        [SerializeField] PixelUiSkin skin;
        [SerializeField] PlayerHealth playerHealth;
        [SerializeField] PlayerWeapons playerWeapons;
        [SerializeField] WaveManager waveManager;
        [SerializeField] UpgradeManager upgradeManager;
        // 소진한 탄약 핍의 투명도. 남은 탄약은 불투명.
        [SerializeField, Range(0f, 1f)] float spentPipAlpha = 0.25f;
        // 탄약이 부족해 쏠 수 없는 무기 스프라이트 위 X 아이콘의 깜빡임 주기 (초, 실시간).
        [SerializeField, Min(0.01f)] float emptyBlinkInterval = 0.3f;

        [Header("Crosshair")]
        // 커서 자리의 크로스헤어 = 중심점 + 탄약 링. 치수는 텍셀 단위이며 HUD 배율로 확대된다. 중심점은 링의 채움 색을 쓴다.
        [SerializeField, Min(2f)] float cursorRingRadiusTexels = 11f;
        [SerializeField, Min(1)] int cursorRingThicknessTexels = 3;
        // 링 안팎 윤곽선 두께. 1텍셀은 밝은 땅 위에서 너무 가늘게 보여 2로 둔다. 중심점 윤곽선은 이 값과 무관하게 1텍셀.
        [SerializeField, Min(1)] int cursorRingOutlineTexels = 2;
        [SerializeField] Color cursorRingFillColor = Color.white;
        [SerializeField] Color cursorRingEmptyColor = new Color(0.25f, 0.22f, 0.2f, 0.6f);
        [SerializeField] Color cursorRingOutlineColor = new Color(0.1f, 0.08f, 0.08f, 0.85f);
        // 중심점 반경. 1이면 2×2텍셀.
        [SerializeField, Min(1f)] float cursorDotRadiusTexels = 1f;
        // 사격 한 번에 링 반경이 벌어지는 양: 쏜 무기의 반동 배율(WeaponData.RecoilStrength) 1당 텍셀 수. 무거운 무기일수록 크게 벌어진다.
        [SerializeField, Min(0f)] float cursorRingKickTexels = 4f;
        // 벌어진 링이 되돌아오는 속도 (지수 감쇠율, 1/초). 무기 반동(PlayerWeapons.recoilReturnSpeed)보다 느리게 둬 서서히 조여든다.
        [SerializeField, Min(0.01f)] float cursorRingReturnSpeed = 6f;

        [Header("Player Gauge")]
        // 플레이어 머리 위 체력 게이지의 색. 테두리 없이 바탕 위에 남은 비율만큼 채운다. 채움 기본값은 시트 게이지 채움 조각의 붉은색.
        [SerializeField] Color playerGaugeBackColor = Color.black;
        [SerializeField] Color playerGaugeFillColor = new Color32(221, 103, 76, 255);

        // IMGUI는 depth가 작을수록 위에 그린다. 다른 스크립트는 기본값 0이므로 HUD는 그 뒤에 깔린다.
        const int HudGuiDepth = 10;
        const int MarginTexels = 8;      // 화면 가장자리 → 패널
        const int PaddingTexels = 8;     // 패널 테두리 → 내용
        const int GapTexels = 4;         // 행 사이·이웃 요소 사이
        const int ColumnGapTexels = 8;   // 열 사이
        const int EntryGapTexels = 8;    // 무기 항목(두 줄) 사이 — 행 간격보다 넓혀 항목 경계가 보이도록
        // 체력 핍은 16텍셀 타일이지만 보이는 칸이 10텍셀이라 타일 간격보다 촘촘히 겹쳐 놓는다. 여백 2텍셀.
        const int HpPipStrideTexels = 12;
        // 탄약 핍 줄의 고정 폭. 탄창 크기와 무관하게 이 폭을 양 끝까지 채우므로 탄창이 커져도 패널은 그대로고 핍 간격만 줄어든다.
        const int AmmoPipRowTexels = 96;
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

        SpriteRenderer playerSprite;
        Camera cam;
        readonly List<int> sideSlots = new List<int>(PlayerWeapons.MaxSlots);
        readonly CrosshairTexture crosshair = new CrosshairTexture();
        // 크로스헤어 링이 사격으로 벌어져 있는 양 (텍셀, 0 = 제자리). 발사 시 세팅되고 지수 감쇠로 복귀.
        float cursorRingKick;

        // HUD(크로스헤어 포함)를 그리는 조건.
        //   플레이어는 사망 시 파괴된다. 그 뒤는 데스 화면이 이어받으므로 HUD를 걷는다.
        //   카드 선택 화면은 게임을 멈추고 화면을 덮는다. 그 위에 HUD가 보이면 어색하므로 숨긴다.
        bool HudVisible => playerHealth != null && !upgradeManager.IsActive;

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
            playerSprite = playerHealth.GetComponent<SpriteRenderer>();
            if (playerSprite == null)
                throw new System.Exception("[PickShoot] GameHud: 플레이어에 SpriteRenderer 없음 (머리 위 게이지 위치 기준).");
            cam = Camera.main;
            if (cam == null)
                throw new System.Exception("[PickShoot] MainCamera가 필요합니다 (머리 위 게이지 월드→화면 변환용).");
            skin.Validate();
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
        }

        void Update()
        {
            // 크로스헤어가 커서를 대신하는 동안만 하드웨어 커서를 숨긴다. HUD가 걷히는 카드 선택·데스 화면에서는 커서가 돌아온다
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

            // 머리 위 게이지만 캔버스 밖에서 화면 픽셀로 그린다. 월드에 붙은 표시라 패널 밑에 깔리도록 먼저.
            DrawPlayerGauge();

            int hudScale = PixelGui.TextScale;
            using var canvas = PixelGui.BeginCanvas();
            DrawHealthPanel(hudScale);
            DrawWeaponPanel(canvas, hudScale, leftSide: true);
            DrawWeaponPanel(canvas, hudScale, leftSide: false);
            DrawWaveBanner(canvas, hudScale);
            // 커서는 패널 위로 지나갈 수 있으므로 마지막에 그려 항상 패널 위에 보이게 한다.
            DrawCrosshair(hudScale);
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

        // 플레이어 스프라이트 위에 비율 게이지를 가운데 정렬로 띄운다 (숫자 없음). 풀피면 그리지 않는다 — 다쳤을 때만 눈에 띄게.
        // 캔버스 밖에서 호출한다 — 좌표·크기는 화면 픽셀.
        void DrawPlayerGauge()
        {
            if (playerHealth.Current >= playerHealth.Max) return;

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
            int x = Mathf.RoundToInt(sp.x) - barW / 2;
            int y = Mathf.RoundToInt(Screen.height - sp.y) - barH;
            int fillW = Mathf.RoundToInt(barW * (float)playerHealth.Current / playerHealth.Max);
            PixelGui.FillRect(new Rect(x, y, barW, barH), playerGaugeBackColor);
            PixelGui.FillRect(new Rect(x, y, fillW, barH), playerGaugeFillColor);
        }

        // 프레임 + 비율만큼 잘라낸 붉은 채움. 채움 조각은 프레임과 같은 자리에 그리면 안쪽에 딱 맞고, 오른쪽부터 줄어든다.
        void DrawGauge(Rect bar, float ratio, int s)
        {
            PixelGui.DrawBarStrip(skin.BarFrame, bar, skin.TileSize, s);
            int inset = BarFillInsetTexels * s;
            int fillW = inset + Mathf.RoundToInt((bar.width - 2 * inset) * Mathf.Clamp01(ratio));
            PixelGui.DrawBarStrip(skin.BarFillRed, bar, skin.TileSize, s, fillW);
        }

        // 하단 한쪽 회색 패널: 링의 해당 쪽에 있는 슬롯만. 슬롯마다 두 줄 — 윗줄은 이름·레벨, 아랫줄은 스프라이트(탄약 부족 표시가 그 위에 겹침)·탄약 핍.
        // 한 줄에 늘어놓는 것보다 폭이 좁아 게임 화면을 덜 가린다. 슬롯 번호는 카드에서만 쓰므로 HUD에는 두지 않는다.
        // 해당 쪽에 슬롯이 없으면 패널도 그리지 않는다.
        void DrawWeaponPanel(PixelGui.CanvasScope canvas, int s, bool leftSide)
        {
            var slots = playerWeapons.Slots;
            sideSlots.Clear();
            for (int i = 0; i < slots.Count; i++)
                if (playerWeapons.IsOnLeftOfAim(i) == leftSide) sideSlots.Add(i);
            if (sideSlots.Count == 0) return;

            var font = skin.FontPlain;
            int tile = skin.TileSize * s;
            int lineH = font.lineHeight * s;
            int pad = PaddingTexels * s;
            int gap = GapTexels * s;
            int colGap = ColumnGapTexels * s;
            int entryGap = EntryGapTexels * s;
            int margin = MarginTexels * s;

            // 열 폭은 패널 안 모든 항목에서 공통 — 이름 길이가 달라도 레벨과 핍이 세로로 나란히 정렬되도록.
            int spriteW = 0, spriteH = 0, nameW = 0, levelW = 0;
            for (int k = 0; k < sideSlots.Count; k++)
            {
                var w = slots[sideSlots[k]];
                var r = w.Data.WeaponSprite.rect;
                spriteW = Mathf.Max(spriteW, Mathf.RoundToInt(r.width) * s);
                spriteH = Mathf.Max(spriteH, Mathf.RoundToInt(r.height) * s);
                nameW = Mathf.Max(nameW, PixelGui.MeasureText(font, NameLabel(w), s));
                levelW = Mathf.Max(levelW, PixelGui.MeasureText(font, LevelLabel(w), s));
            }
            // 핍 줄 폭은 고정 — 탄창이 커져도 패널이 넓어지지 않고 핍 간격이 줄어든다.
            int pipsW = AmmoPipRowTexels * s;
            // 아이콘 열: 무기 스프라이트와 그 위에 겹쳐 깜빡이는 탄약 부족 X(타일 한 칸) 중 넓은 쪽.
            // X에 따로 자리를 주지 않아 그만큼 패널이 좁고, 깜빡여도 핍이 움직이지 않는다.
            int iconW = Mathf.Max(spriteW, tile);
            // 두 줄 중 넓은 쪽이 항목 폭.
            int ammoLineH = Mathf.Max(Mathf.Max(lineH, tile), spriteH);
            int entryH = lineH + gap + ammoLineH;
            int contentW = Mathf.Max(nameW + colGap + levelW, iconW + gap + pipsW);
            int contentH = sideSlots.Count * entryH + (sideSlots.Count - 1) * entryGap;

            int panelW = CeilToMultiple(contentW + 2 * pad, tile);
            int panelH = CeilToMultiple(contentH + 2 * pad, tile);
            int panelX = leftSide ? margin : canvas.Width - margin - panelW;
            var panel = new Rect(panelX, canvas.Height - margin - panelH, panelW, panelH);
            PixelGui.DrawNineSlice(skin.PanelGrey, panel, skin.TileSize, s);

            int x0 = (int)panel.x + (panelW - contentW) / 2;
            int y = (int)panel.y + (panelH - contentH) / 2;

            bool blinkOn = Mathf.FloorToInt(Time.unscaledTime / emptyBlinkInterval) % 2 == 0;
            for (int k = 0; k < sideSlots.Count; k++)
            {
                if (k > 0) y += entryGap;
                var w = slots[sideSlots[k]];

                PixelGui.DrawText(font, NameLabel(w), x0, y, s);
                PixelGui.DrawText(font, LevelLabel(w), x0 + nameW + colGap, y, s);

                int lineY = y + lineH + gap;
                int iconY = lineY + (ammoLineH - tile) / 2;
                var sprite = w.Data.WeaponSprite;
                int sw = Mathf.RoundToInt(sprite.rect.width) * s;
                int sh = Mathf.RoundToInt(sprite.rect.height) * s;
                PixelGui.DrawSprite(sprite, new Rect(x0 + (iconW - sw) / 2, lineY + (ammoLineH - sh) / 2, sw, sh));

                // 탄약이 0이라 쏠 수 없으면 무기 스프라이트 위에 붉은 X를 깜빡여 회수를 재촉한다.
                // (사격분에 못 미치는 잔량은 남은 만큼 발사되므로 부족 표시 대상이 아니다 — Weapons §4)
                if (w.CurrentAmmo <= 0 && blinkOn)
                    PixelGui.DrawSprite(skin.IconCross, new Rect(x0 + (iconW - tile) / 2, iconY, tile, tile));

                DrawAmmoPips(w, x0 + iconW + gap, iconY, s);
                y += entryH;
            }
        }

        // 탄환 한 발이 핍 하나. 왼쪽부터 남은 탄약만큼 불투명, 나머지는 흐리게 — 최대치와 잔량이 한눈에 보인다.
        // 간격이 좁아 겹칠 때 남은 탄약(왼쪽)이 위에 오도록 오른쪽부터 그린다.
        void DrawAmmoPips(Weapon w, int x, int y, int s)
        {
            int tile = skin.TileSize * s;
            var spent = new Color(1f, 1f, 1f, spentPipAlpha);
            for (int i = w.MaxAmmo - 1; i >= 0; i--)
            {
                GUI.color = i < w.CurrentAmmo ? Color.white : spent;
                PixelGui.DrawSprite(skin.IconBulletPlain, new Rect(x + AmmoPipOffsetTexels(i, w.MaxAmmo) * s, y, tile, tile));
            }
            GUI.color = Color.white;
        }

        // i번째 핍의 줄 안 x 오프셋(텍셀). 탄창 크기와 무관하게 첫 핍은 왼쪽 끝·마지막 핍은 오른쪽 끝에 고정하고 사이를
        // 최대한 고르게 나눈다 — 반올림 탓에 이웃 간격이 1텍셀씩 다를 수 있다. 간격을 정수로 내려 같은 값으로 늘어놓으면
        // 개수에 따라 줄 끝이 비어 배열 폭이 들쭉날쭉해지므로 그렇게 하지 않는다.
        int AmmoPipOffsetTexels(int index, int count)
        {
            if (count <= 1) return 0;
            int span = AmmoPipRowTexels - skin.TileSize;
            if (count - 1 > span)
                throw new System.Exception($"[PickShoot] GameHud: 탄약 핍 {count}개는 폭 {AmmoPipRowTexels}텍셀 줄에 들어가지 않는다. AmmoPipRowTexels를 늘려야 한다.");
            // index * span / (count - 1)을 정수 연산으로 반올림.
            return (2 * index * span + (count - 1)) / (2 * (count - 1));
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

        // 커서 자리에 크로스헤어(중심점 + 전체 탄창 비율 링). 텍스처는 상태가 바뀔 때만 다시 그리고, 다른 HUD와 같은 배율로 띄운다.
        // 사격으로 벌어진 양은 텍셀 단위로 반올림해 반경에 더한다 — 링이 늘 온전한 텍셀 원 모양으로 한 칸씩 조여든다
        // (소수 반경을 그대로 쓰면 되돌아오는 동안 둘레 텍셀이 제각각 깜빡인다).
        void DrawCrosshair(int s)
        {
            float radius = cursorRingRadiusTexels + Mathf.Round(cursorRingKick);
            crosshair.Refresh(playerWeapons.Slots, radius, cursorRingThicknessTexels, cursorRingOutlineTexels, cursorDotRadiusTexels,
                cursorRingFillColor, cursorRingEmptyColor, cursorRingOutlineColor);

            int size = crosshair.SizeTexels * s;
            // 캔버스가 열린 동안 Repaint 이벤트의 mousePosition은 캔버스 좌표(위가 0)라 그대로 중심으로 쓴다. 한 변이 짝수 텍셀이라 반은 정수.
            Vector2 m = Event.current.mousePosition;
            var rect = new Rect(Mathf.RoundToInt(m.x) - size / 2, Mathf.RoundToInt(m.y) - size / 2, size, size);
            GUI.DrawTexture(rect, crosshair.Texture, ScaleMode.StretchToFill, true);
        }

        // 글꼴에 있는 문자(A-Z 0-9 % + - 공백)만 써야 한다. 없는 문자는 PixelGui가 예외로 알린다.
        static string NameLabel(Weapon w) => w.Data.DisplayName.ToUpperInvariant();
        static string LevelLabel(Weapon w) => $"LV {w.Level}";

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
