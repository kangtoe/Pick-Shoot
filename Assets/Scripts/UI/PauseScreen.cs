using UnityEngine;
using UnityEngine.InputSystem;

namespace PickShoot
{
    // ESC로 게임을 멈추고 화면을 어둡게 덮는 일시정지 화면. PixelUiSkin(Kenney Interface 시트)으로 OnGUI에 그린다.
    // 구성은 보상 화면(UpgradeManager)과 같은 결 (2026-10-12): 빌드 격자의 12칸 폭이 묶음의 좌우 끝이고, 그 위 띠의 왼쪽에 제목 `PAUSED`(패널 없는
    // 아웃라인 글꼴, 패널 배율)와 부제 재개 안내(일반 글꼴, 글자 배율), 오른쪽 끝에 같은 꼴의 `WAVE N`과 `LEVEL 5   XP 230`(라벨 일반·값 아웃라인 글꼴)을 둔다.
    // 띠 아래 160px(HeaderGapTexels — 보기 카드가 무기 줄 위에 들 자리)에 현재 빌드 격자(BuildGridPainter — 제목 `WEAPONS (7/12)` 아래 보유 무기 한 줄,
    // 제목 `RELICS (5)` 아래 유물 격자, 패널 배율 타일).
    // 그전(2026-10-02 ~ 10-12)엔 가운데 회색 상태 카드(위 끝에 걸친 파란 `PAUSED` 배지, WAVE·LEVEL·XP 표) 아래 격자, 맨 아래 가운데 재개 안내였다 — 카드는
    // 화면 가운데, 격자는 12칸 폭의 왼쪽 끝이라 정렬 축이 둘로 갈려 자루가 적을 때 흩어져 보였고, 배지는 보상 화면이 2026-10-08 걷은 꼴이 여기만 남아 있었다.
    // 버튼(RESUME·RESTART)을 더한 안, 두 열(왼쪽 상태 카드·버튼, 오른쪽 6열 격자) 안과 아이템이 적을 때·많을 때로 비교해 이 꼴을 골랐다
    // (Recordings/pause_mockup_2026-10-12_*.png). 아이템이 적으면 띠 양 끝 사이가 빈다.
    // 일시정지 중엔 HUD가 숨으므로 웨이브도 여기서 보이고, 누적 경험치 총량은 여기서만 보인다 (GDD §3.3).
    // 타일에 마우스를 올리면 그 자루·유물의 보기 카드(UpgradeCardPainter.DrawOwnedWeapon·DrawOwnedRelic — 보상 화면 카드와 같은 틀의 회색 카드)가 뜬다
    // (2026-10-12, 그전엔 툴팁). 하드웨어 커서는 멈춘 동안 GameHud가 되살린다.
    // 멈춤은 업그레이드 카드와 같은 방식(Time.timeScale = 0)이라 물리·코루틴·사격이 함께 멎는다.
    // 카드 선택 중(이미 멈춰 있음)과 플레이어 사망 뒤(데스 화면이 이어받음)에는 ESC를 받지 않는다.
    // 멈춘 동안 HUD는 GameHud가 걷고 하드웨어 커서를 되살린다 — 볼륨 조절 등 클릭 조작이 들어올 자리.
    // OnGUI가 PlayerHealth의 데미지 플래시 위에 그려지도록 실행 순서를 뒤로 민다.
    [DefaultExecutionOrder(100)]
    public class PauseScreen : MonoBehaviour
    {
        [SerializeField] PlayerHealth playerHealth;
        [SerializeField] UpgradeManager upgradeManager;
        [SerializeField] PlayerExperience experience;
        [SerializeField] WaveManager waveManager;

        [Header("Presentation")]
        [SerializeField] PixelUiSkin skin;
        [SerializeField, Range(0f, 1f)] float dimAlpha = 0.7f;

        // 글꼴에 있는 문자(A-Z 0-9 % + - 공백)만 써야 한다. 없는 문자는 PixelGui가 예외로 알린다.
        const string Title = "PAUSED";
        const string Hint = "PRESS ESC TO RESUME";
        static readonly string[] StatLabels = { "LEVEL", "XP" };
        const int StatPairGapSpaces = 3;     // 띠 오른쪽 아랫줄의 쌍 사이 (공백 칸 수). 라벨 → 값은 한 칸.
        // 위 띠 → 빌드 격자 (패널 텍셀, 160px). 묶음이 세로 가운데라 간격을 벌리면 띠는 올라가고 격자는 내려간다 — 무기 줄 위에 가장 긴 보기 카드(512px,
        // 기본 수치 특성이 있는 자루에 다른 수치 개조)가 타일과의 간격 8px를 두고 화면 위 끝에서 24px 떨어져 드는 값이다 (유물 한 줄일 때 무기 줄 y = 544).
        // 보상 화면의 띠 → 카드(UpgradeManager.CardGapTexels)와 같은 10(40px)으로 두었더니 무기 줄이 화면 세로 가운데에 와 위아래가 476px씩이라 그 카드가
        // 화면 위 끝에 붙어 타일 위쪽 28px를 가렸다. 묶음 전체·격자만 36px 내리는 안, 간격 112·144·192px(카드 위 여백 0·16·40px)과 비교해 골랐다 —
        // 192px는 아무것도 띄우지 않았을 때 띠가 격자와 따로 놀았다 (Recordings/pause_shift_mockup_2026-10-12_*.png·pause_gap_mockup_2026-10-12_*.png, 2026-10-12).
        // 보기 카드가 더 길어지면 위 여백이 그만큼 줄고, 24px를 넘으면 다시 화면 끝에 붙는다.
        const int HeaderGapTexels = 40;
        const int SectionGapTexels = 12;     // 무기 줄 아래 → 유물 제목 (글자 텍셀). 제목과 그 타일 사이(BuildGridPainter, 4)보다 넓어 섹션이 갈린다.

        bool paused;
        float prevTimeScale;
        // 빌드 격자의 출처. 플레이어 오브젝트의 컴포넌트라 playerHealth에서 함께 찾는다.
        PlayerWeapons weapons;
        PlayerStats stats;

        public bool IsPaused => paused;

        void Awake()
        {
            if (playerHealth == null)
                throw new System.Exception("[PickShoot] PauseScreen.playerHealth 미할당 (사망 뒤 일시정지 차단에 필요).");
            if (upgradeManager == null)
                throw new System.Exception("[PickShoot] PauseScreen.upgradeManager 미할당 (카드 선택 중 일시정지 차단에 필요).");
            if (experience == null)
                throw new System.Exception("[PickShoot] PauseScreen.experience 미할당 (레벨·누적 경험치 표기에 필요).");
            if (waveManager == null)
                throw new System.Exception("[PickShoot] PauseScreen.waveManager 미할당 (웨이브 표기에 필요).");
            if (skin == null)
                throw new System.Exception("[PickShoot] PauseScreen.skin 미할당. PixelUiSkin 자산(Assets/Data/UI) 필요.");
            skin.Validate();
            weapons = playerHealth.GetComponent<PlayerWeapons>();
            if (weapons == null)
                throw new System.Exception("[PickShoot] PauseScreen: 플레이어에 PlayerWeapons 없음 (빌드 격자의 무기 줄).");
            stats = playerHealth.GetComponent<PlayerStats>();
            if (stats == null)
                throw new System.Exception("[PickShoot] PauseScreen: 플레이어에 PlayerStats 없음 (빌드 격자의 유물 격자).");
        }

        // 멈춘 채 파괴되면(씬 재시작 등) timeScale이 0으로 남아 다음 씬까지 멎으므로 되돌린다.
        void OnDestroy()
        {
            if (paused) Resume();
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (!kb.escapeKey.wasPressedThisFrame) return;

            if (paused)
            {
                Resume();
                return;
            }
            // 플레이어는 사망 시 파괴된다. 그 뒤는 데스 화면이, 카드 선택 중은 UpgradeManager가 화면을 맡는다.
            if (playerHealth == null || upgradeManager.IsActive) return;
            Pause();
        }

        void Pause()
        {
            prevTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            paused = true;
        }

        void Resume()
        {
            Time.timeScale = prevTimeScale;
            paused = false;
        }

        void OnGUI()
        {
            if (!paused) return;
            if (Event.current.type != EventType.Repaint) return;

            int scale = PixelGui.PanelScale;
            int textScale = PixelGui.TextScale;
            int lineH = skin.FontPlain.lineHeight * textScale;

            // 어둡게 덮는 막은 화면 픽셀 그대로 (캔버스를 열기 전).
            GUI.color = new Color(0f, 0f, 0f, dimAlpha);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            using var canvas = PixelGui.BeginCanvas();

            // 묶음의 좌우 끝은 12칸 격자의 폭 — 자루가 늘어도 띠·격자가 움직이지 않는다.
            int gridScale = scale;
            BuildGridPainter.RequireFits(gridScale, canvas.Width);
            int blockW = BuildGridPainter.Width(gridScale);
            int left = canvas.Width / 2 - blockW / 2;
            int right = left + blockW;

            // 위 띠 = 제목 줄(아웃라인 글꼴 패널 배율) + 부제 한 줄(글자 배율) — 보상 화면의 띠와 같은 96px.
            int titleH = skin.FontOutlined.lineHeight * scale;
            int headerH = titleH + lineH;
            int headerGap = HeaderGapTexels * scale;
            int relicRows = BuildGridPainter.RelicRows(BuildGridPainter.RelicKinds(stats).Count);
            // 격자 묶음 = 제목 줄 + 무기 줄 + 섹션 간격 + 제목 줄 + 유물 격자.
            int rowTitleH = BuildGridPainter.TitleHeight(skin);
            int tileH = ItemTilePainter.Size(gridScale);
            int sectionGap = SectionGapTexels * textScale;
            int gridH = rowTitleH + tileH + sectionGap + rowTitleH + BuildGridPainter.Height(skin, gridScale, relicRows);
            // 묶음 = 위 띠 + 빌드 격자(무기 한 줄 + 유물 줄들). 세로 중앙에.
            int blockH = headerH + headerGap + gridH;
            if (blockH > canvas.Height)
                throw new System.Exception($"[PickShoot] PauseScreen: 묶음 높이 {blockH}px가 캔버스 {canvas.Height}px를 넘음 (유물 {relicRows}줄). 격자 배율을 낮추거나 줄을 줄일 것.");
            int y = (canvas.Height - blockH) / 2;

            // 띠의 왼쪽(제목·부제)과 오른쪽(웨이브·레벨·XP)이 맞닿으면 예외. 12칸 폭(1520px)에서는 넉넉하다.
            string wave = $"WAVE {waveManager.CurrentWave}";
            int leftW = Mathf.Max(PixelGui.MeasureText(skin.FontOutlined, Title, scale), PixelGui.MeasureText(skin.FontPlain, Hint, textScale));
            int rightW = Mathf.Max(PixelGui.MeasureText(skin.FontOutlined, wave, scale), StatLineWidth(textScale));
            if (left + leftW >= right - rightW)
                throw new System.Exception($"[PickShoot] PauseScreen: 위 띠의 왼쪽({leftW}px)과 오른쪽({rightW}px)이 묶음 폭 {blockW}px 안에서 맞닿음. 문구를 줄일 것.");
            PixelGui.DrawText(skin.FontOutlined, Title, left, y, scale);
            PixelGui.DrawText(skin.FontPlain, Hint, left, y + titleH, textScale);
            PixelGui.DrawTextRight(skin.FontOutlined, wave, right, y, scale);
            DrawStatLine(right, y + titleH, textScale);

            int gridY = y + headerH + headerGap;
            var mouse = Event.current.mousePosition;
            BuildGridPainter.DrawTitle(skin, BuildGridPainter.WeaponTitle(weapons), left, gridY);
            int weaponY = gridY + rowTitleH;
            int slot = BuildGridPainter.DrawWeaponRow(skin, weapons, left, weaponY, gridScale, mouse, out var weaponTile);
            int relicTitleY = weaponY + tileH + sectionGap;
            BuildGridPainter.DrawTitle(skin, BuildGridPainter.RelicTitle(stats), left, relicTitleY);
            var relic = BuildGridPainter.DrawRelicGrid(skin, stats, left, relicTitleY + rowTitleH, gridScale, relicRows, mouse, out var relicTile);

            // 보기 카드는 다 그린 뒤 맨 마지막에 — 위 띠·이웃 타일 위로 뜬다.
            if (slot >= 0)
                UpgradeCardPainter.DrawOwnedWeapon(skin, canvas, weapons.Slots[slot], stats, weaponTile);
            else if (relic != null)
                UpgradeCardPainter.DrawOwnedRelic(skin, canvas, relic, stats.CountOf(relic), relicTile);
        }

        // StatLabels와 같은 순서의 값.
        string StatValue(int i)
        {
            switch (i)
            {
                case 0: return experience.Level.ToString();
                case 1: return experience.Total.ToString();
                default: throw new System.Exception($"[PickShoot] PauseScreen: 띠에 없는 항목 {i}.");
            }
        }

        // 띠 오른쪽 아랫줄 `LEVEL 5   XP 230`의 폭 (캔버스 픽셀). 라벨은 일반 글꼴, 값은 아웃라인 글꼴 — 그전 상태 카드 표와 같은 짝.
        int StatLineWidth(int textScale)
        {
            int space = skin.FontPlain.spaceWidth * textScale;
            int w = 0;
            for (int i = 0; i < StatLabels.Length; i++)
            {
                if (i > 0) w += StatPairGapSpaces * space;
                w += PixelGui.MeasureText(skin.FontPlain, StatLabels[i], textScale) + space + PixelGui.MeasureText(skin.FontOutlined, StatValue(i), textScale);
            }
            return w;
        }

        // rightX에 줄의 오른쪽 끝을 맞춘다.
        void DrawStatLine(int rightX, int y, int textScale)
        {
            int space = skin.FontPlain.spaceWidth * textScale;
            int x = rightX - StatLineWidth(textScale);
            for (int i = 0; i < StatLabels.Length; i++)
            {
                if (i > 0) x += StatPairGapSpaces * space;
                PixelGui.DrawText(skin.FontPlain, StatLabels[i], x, y, textScale);
                x += PixelGui.MeasureText(skin.FontPlain, StatLabels[i], textScale) + space;
                string value = StatValue(i);
                PixelGui.DrawText(skin.FontOutlined, value, x, y, textScale);
                x += PixelGui.MeasureText(skin.FontOutlined, value, textScale);
            }
        }
    }
}
