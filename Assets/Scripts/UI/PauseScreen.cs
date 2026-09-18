using UnityEngine;
using UnityEngine.InputSystem;

namespace PickShoot
{
    // ESC로 게임을 멈추고 화면을 어둡게 덮는 일시정지 화면. PixelUiSkin(Kenney Interface 시트)으로 OnGUI에 그린다.
    // 구성은 데스 화면과 같은 결: 파란 배너("PAUSED") → 회색 패널(재개 안내). 배너는 패널 배율, 그 아래는 글자 배율.
    // 멈춤은 업그레이드 카드와 같은 방식(Time.timeScale = 0)이라 물리·코루틴·사격이 함께 멎는다.
    // 카드 선택 중(이미 멈춰 있음)과 플레이어 사망 뒤(데스 화면이 이어받음)에는 ESC를 받지 않는다.
    // 멈춘 동안 HUD는 GameHud가 걷고 하드웨어 커서를 되살린다 — 볼륨 조절 등 클릭 조작이 들어올 자리.
    // OnGUI가 PlayerHealth의 데미지 플래시 위에 그려지도록 실행 순서를 뒤로 민다.
    [DefaultExecutionOrder(100)]
    public class PauseScreen : MonoBehaviour
    {
        [SerializeField] PlayerHealth playerHealth;
        [SerializeField] UpgradeManager upgradeManager;

        [Header("Presentation")]
        [SerializeField] PixelUiSkin skin;
        [SerializeField, Range(0f, 1f)] float dimAlpha = 0.7f;

        // 글꼴에 있는 문자(A-Z 0-9 % + - 공백)만 써야 한다. 없는 문자는 PixelGui가 예외로 알린다.
        const string Title = "PAUSED";
        const string Hint = "PRESS ESC TO RESUME";
        const int PaddingTexels = 8;     // 패널 테두리 → 내용

        bool paused;
        float prevTimeScale;

        public bool IsPaused => paused;

        void Awake()
        {
            if (playerHealth == null)
                throw new System.Exception("[PickShoot] PauseScreen.playerHealth 미할당 (사망 뒤 일시정지 차단에 필요).");
            if (upgradeManager == null)
                throw new System.Exception("[PickShoot] PauseScreen.upgradeManager 미할당 (카드 선택 중 일시정지 차단에 필요).");
            if (skin == null)
                throw new System.Exception("[PickShoot] PauseScreen.skin 미할당. PixelUiSkin 자산(Assets/Data/UI) 필요.");
            skin.Validate();
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
            int tile = skin.TileSize * scale;
            int textTile = skin.TileSize * textScale;
            int lineH = skin.FontPlain.lineHeight * textScale;

            // 어둡게 덮는 막은 화면 픽셀 그대로 (캔버스를 열기 전).
            GUI.color = new Color(0f, 0f, 0f, dimAlpha);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            using var canvas = PixelGui.BeginCanvas();

            int titleW = PixelGui.MeasureText(skin.FontOutlined, Title, scale);
            int bannerW = CeilToMultiple(titleW + 2 * tile, tile);
            int bannerH = 2 * tile;

            int pad = PaddingTexels * textScale;
            int panelW = CeilToMultiple(PixelGui.MeasureText(skin.FontPlain, Hint, textScale) + 2 * pad, textTile);
            int panelH = CeilToMultiple(lineH + 2 * pad, textTile);
            int gap = textTile;

            // 배너·패널 묶음을 화면 세로 중앙에 놓는다.
            int blockH = bannerH + gap + panelH;
            int y = (canvas.Height - blockH) / 2;
            int centerX = canvas.Width / 2;

            var banner = new Rect(centerX - bannerW / 2, y, bannerW, bannerH);
            PixelGui.DrawNineSlice(skin.PanelBlue, banner, skin.TileSize, scale);
            PixelGui.DrawTextCentered(skin.FontOutlined, Title, centerX,
                y + (bannerH - skin.FontOutlined.lineHeight * scale) / 2, scale);
            y += bannerH + gap;

            var panel = new Rect(centerX - panelW / 2, y, panelW, panelH);
            PixelGui.DrawNineSlice(skin.PanelGrey, panel, skin.TileSize, textScale);
            PixelGui.DrawTextCentered(skin.FontPlain, Hint, centerX, y + (panelH - lineH) / 2, textScale);
        }

        static int CeilToMultiple(int value, int multiple)
        {
            return ((value + multiple - 1) / multiple) * multiple;
        }
    }
}
