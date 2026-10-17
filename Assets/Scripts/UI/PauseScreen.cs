using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace PickShoot
{
    // ESC로 게임을 멈추고 화면을 어둡게 덮는 일시정지 화면. PixelUiSkin(Kenney Interface 시트)으로 OnGUI에 그린다.
    // 구성은 보상 화면(UpgradeManager)의 왼쪽 상태 열을 오른쪽 선택지 열 없이 캔버스 가운데로 옮긴 꼴이다 (2026-10-17, 사용자 제안) — 같은 StatusColumnPainter로
    // 같은 크기·y의 열(UpgradeManager.CenteredStatusColumn)에 그리므로 두 화면을 오가도 열 안의 배치가 같다: 띠 왼쪽의 제목 `PAUSED`와 부제 재개 안내, 그 아래
    // `STATS` 스탯 총계 표(2열), `WEAPONS (7/12)` 무기 격자와 `RELICS (5)` 유물 격자(패널 배율 6열). 띠 오른쪽 끝에는 이 화면에만 있는 `WAVE N`(아웃라인 글꼴,
    // 패널 배율)과 `LEVEL 5   XP 230`(라벨 일반·값 아웃라인 글꼴, 글자 배율)을 둔다.
    // 그전(2026-10-12 ~ 10-17)엔 12칸 폭의 가운데 묶음 — 같은 띠 아래 160px 간격(보기 카드가 무기 줄 위에 들 자리) 가운데에 스탯 표 4열, 그 아래 12열 무기 한 줄과
    // 유물 격자 — 이었다. 보상 화면과 같은 정보가 다른 꼴이라 화면을 오갈 때마다 다시 찾아야 했다. 오른쪽에 빈 열을 두고 행동 칸·보기 카드 자리로 쓰는 안도 봤으나
    // 열만 가운데로 옮겼다. 대신 유물 격자의 한계가 보상 화면과 같은 12칸이다 (Relics §7).
    // 그보다 전(2026-10-02 ~ 10-12)엔 가운데 회색 상태 카드(위 끝에 걸친 파란 `PAUSED` 배지, WAVE·LEVEL·XP 표) 아래 격자, 맨 아래 가운데 재개 안내였다
    // (Recordings/pause_mockup_2026-10-12_*.png).
    // 캔버스 오른쪽 아래에 `MAIN MENU` 버튼 (2026-10-15, 웹 빌드를 공개하며 사용자 요청) — 시작 화면 PLAY와 같은 누르는 판(PlateButtonPainter)에 같은 자리이고, 누르면
    // 확인 없이 씬을 다시 불러 시작 화면(캐릭터·무기 고르기)으로 간다 — 사망 뒤 R 재시작(GameOverScreen)과 같은 길. 2026-10-17까지는 아래 가운데였다 — 열이 보상 화면처럼
    // 아래 끝(968px)까지 내려와 그 자리(120px 판, y 928)와 겹친다.
    // 일시정지 중엔 HUD가 숨으므로 웨이브도 여기서 보이고, 누적 경험치 총량은 여기서만 보인다 (GDD §3.3).
    // 타일에 마우스를 올리면 그 자루·유물·캐릭터의 보기 카드(UpgradeCardPainter.DrawOwnedWeapon·DrawOwnedRelic·DrawOwnedCharacter — 보상 화면 카드와 같은 틀의 회색 카드)가 뜬다
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
        // 오른쪽 아래 버튼 — 캔버스 오른쪽·아래 끝에서 시작 화면 PLAY와 같은 8텍셀(32px).
        const string MainMenuLabel = "MAIN MENU";
        const int MainMenuMarginTexels = 8;

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
                throw new System.Exception("[PickShoot] PauseScreen.upgradeManager 미할당 (카드 선택 중 일시정지 차단과 상태 열 자리에 필요).");
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

        // 씬을 다시 불러 시작 화면으로. 멈춘 timeScale은 파괴될 때 OnDestroy가 되돌린다.
        void ReturnToMainMenu()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        // 버튼 자리는 캔버스 크기만으로 정해진다 — 클릭 판정(모든 이벤트)과 그리기(Repaint)가 같은 자리를 쓴다.
        Rect MainMenuButtonRect(PixelGui.CanvasScope canvas)
        {
            var size = PlateButtonPainter.Size(skin, MainMenuLabel);
            int margin = MainMenuMarginTexels * PixelGui.PanelScale;
            return new Rect(canvas.Width - margin - size.x, canvas.Height - margin - size.y, size.x, size.y);
        }

        void OnGUI()
        {
            if (!paused) return;
            // 클릭 판정(GUI.Button)은 Repaint 밖의 이벤트에서, 그리기는 Repaint에서만.
            if (Event.current.type != EventType.Repaint)
            {
                using var input = PixelGui.BeginCanvas();
                if (GUI.Button(MainMenuButtonRect(input), GUIContent.none, GUIStyle.none)) ReturnToMainMenu();
                return;
            }

            int scale = PixelGui.PanelScale;
            int textScale = PixelGui.TextScale;

            // 어둡게 덮는 막은 화면 픽셀 그대로 (캔버스를 열기 전).
            PixelGui.FillScreen(new Color(0f, 0f, 0f, dimAlpha));

            using var canvas = PixelGui.BeginCanvas();

            // 보상 화면 상태 열과 같은 크기·y의 열을 캔버스 가운데에 — 띠 왼쪽의 제목·부제와 STATS·WEAPONS·RELICS는 StatusColumnPainter가 그린다.
            var area = upgradeManager.CenteredStatusColumn(canvas.Width, canvas.Height);
            var mouse = Event.current.mousePosition;
            var hover = StatusColumnPainter.Draw(skin, area, Title, Hint, weapons, stats, null, mouse);

            // 띠 오른쪽 끝에 웨이브·레벨·XP. 왼쪽(제목·부제)과 맞닿으면 예외 — 열 폭(768px)에서 웨이브 두 자리·XP 여섯 자리도 122px 남는다.
            string wave = $"WAVE {waveManager.CurrentWave}";
            int leftW = Mathf.Max(PixelGui.MeasureText(skin.FontOutlined, Title, scale), PixelGui.MeasureText(skin.FontPlain, Hint, textScale));
            int rightW = Mathf.Max(PixelGui.MeasureText(skin.FontOutlined, wave, scale), StatLineWidth(textScale));
            if (area.x + leftW >= area.xMax - rightW)
                throw new System.Exception($"[PickShoot] PauseScreen: 띠의 왼쪽({leftW}px)과 오른쪽({rightW}px)이 열 폭 {area.width}px 안에서 맞닿음. 문구를 줄일 것.");
            PixelGui.DrawTextRight(skin.FontOutlined, wave, area.xMax, area.y, scale);
            DrawStatLine(area.xMax, area.y + skin.FontOutlined.lineHeight * scale, textScale);

            // 오른쪽 아래 MAIN MENU. 호버 테두리까지 열과 겹치면 예외.
            var menuButton = MainMenuButtonRect(canvas);
            int hoverOutline = UpgradeCardPainter.HoverOutlineTexels * scale;
            var menuOuter = new Rect(menuButton.x - hoverOutline, menuButton.y - hoverOutline, menuButton.width + 2 * hoverOutline, menuButton.height + 2 * hoverOutline);
            if (menuOuter.Overlaps(new Rect(area.x, area.y, area.width, area.height)))
                throw new System.Exception($"[PickShoot] PauseScreen: MAIN MENU 버튼({menuOuter})이 상태 열({area})과 겹침. 버튼 자리를 옮길 것.");
            PlateButtonPainter.Draw(skin, menuButton, MainMenuLabel, menuButton.Contains(mouse), Color.white);

            // 보기 카드는 다 그린 뒤 맨 마지막에 — 띠·이웃 타일·버튼 위로 뜬다.
            hover.DrawViewCard(skin, canvas, weapons, stats);
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
