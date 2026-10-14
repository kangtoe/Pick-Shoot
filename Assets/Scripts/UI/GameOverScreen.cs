using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace PickShoot
{
    // 플레이어 사망 뒤 화면을 어둡게 덮고 결과를 보여준다. PixelUiSkin(Kenney Interface 시트)으로 OnGUI에 그린다.
    // 구성은 업그레이드 카드 화면과 같은 결: 붉은 배너(해골 + "YOU DIED" + 해골) → 회색 패널(도달 웨이브) → 깜빡이는 재시작 안내.
    // 배너는 패널 배율(PixelGui.PanelScale), 그 아래는 절반인 글자 배율(PixelGui.TextScale)로 기준 캔버스에 그린다.
    // OnGUI가 PlayerHealth의 데미지 플래시(붉은 오버레이) 위에 그려지도록
    // 실행 순서를 뒤로 밀어 마지막에 렌더링되게 한다.
    [DefaultExecutionOrder(100)]
    public class GameOverScreen : MonoBehaviour
    {
        [SerializeField] PlayerHealth playerHealth;
        [SerializeField] WaveManager waveManager;
        [SerializeField] float gameOverDelay = 1f;

        [Header("Presentation")]
        [SerializeField] PixelUiSkin skin;
        [SerializeField, Range(0f, 1f)] float dimAlpha = 0.7f;
        // 재시작 안내가 켜졌다 꺼지는 주기 (초, 실시간).
        [SerializeField, Min(0.01f)] float hintBlinkInterval = 0.5f;

        // 글꼴에 있는 문자(A-Z 0-9 % + - 공백)만 써야 한다. 없는 문자는 PixelGui가 예외로 알린다.
        const string Title = "YOU DIED";
        const string Hint = "PRESS R TO RESTART";
        const int PaddingTexels = 8;     // 패널 테두리 → 내용

        bool gameOver;
        int reachedWave;
        float shownAt;

        void Awake()
        {
            if (playerHealth == null)
                throw new System.Exception("[PickShoot] GameOverScreen.playerHealth 미할당.");
            if (waveManager == null)
                throw new System.Exception("[PickShoot] GameOverScreen.waveManager 미할당 (도달 웨이브 표기에 필요).");
            if (skin == null)
                throw new System.Exception("[PickShoot] GameOverScreen.skin 미할당. PixelUiSkin 자산(Assets/Data/UI) 필요.");
            skin.Validate();
            playerHealth.Died += OnPlayerDied;
        }

        void OnDestroy()
        {
            if (playerHealth != null) playerHealth.Died -= OnPlayerDied;
        }

        void OnPlayerDied()
        {
            reachedWave = waveManager.CurrentWave;
            StartCoroutine(ShowAfterDelay());
        }

        IEnumerator ShowAfterDelay()
        {
            yield return new WaitForSeconds(gameOverDelay);
            gameOver = true;
            shownAt = Time.unscaledTime;
        }

        void Update()
        {
            if (!gameOver) return;
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.rKey.wasPressedThisFrame) Restart();
        }

        void Restart()
        {
            var scene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(scene.buildIndex);
        }

        void OnGUI()
        {
            if (!gameOver) return;
            if (Event.current.type != EventType.Repaint) return;

            int scale = PixelGui.PanelScale;
            int textScale = PixelGui.TextScale;
            int tile = skin.TileSize * scale;
            int textTile = skin.TileSize * textScale;
            int lineH = skin.FontPlain.lineHeight * textScale;

            // 어둡게 덮는 막은 화면 픽셀 그대로 (캔버스를 열기 전).
            PixelGui.FillScreen(new Color(0f, 0f, 0f, dimAlpha));

            using var canvas = PixelGui.BeginCanvas();

            // 배너 안 한 줄: 해골 + 제목 + 해골.
            int space = skin.FontOutlined.spaceWidth * scale;
            int titleW = PixelGui.MeasureText(skin.FontOutlined, Title, scale);
            int titleRowW = tile + space + titleW + space + tile;
            int bannerW = CeilToMultiple(titleRowW + 2 * tile, tile);
            int bannerH = 2 * tile;

            string result = $"REACHED WAVE {reachedWave}";
            int pad = PaddingTexels * textScale;
            int panelW = CeilToMultiple(PixelGui.MeasureText(skin.FontPlain, result, textScale) + 2 * pad, textTile);
            int panelH = CeilToMultiple(lineH + 2 * pad, textTile);
            int gap = textTile;

            // 배너·패널·안내 묶음을 화면 세로 중앙에 놓는다. 안내가 꺼진 동안에도 자리는 그대로 둬서 묶음이 움직이지 않는다.
            int blockH = bannerH + gap + panelH + gap + lineH;
            int y = (canvas.Height - blockH) / 2;
            int centerX = canvas.Width / 2;

            var banner = new Rect(centerX - bannerW / 2, y, bannerW, bannerH);
            PixelGui.DrawNineSlice(skin.PanelRed, banner, skin.TileSize, scale);
            int rowX = centerX - titleRowW / 2;
            int iconY = y + (bannerH - tile) / 2;
            PixelGui.DrawSprite(skin.IconSkull, new Rect(rowX, iconY, tile, tile));
            PixelGui.DrawText(skin.FontOutlined, Title, rowX + tile + space,
                y + (bannerH - skin.FontOutlined.lineHeight * scale) / 2, scale);
            PixelGui.DrawSprite(skin.IconSkull, new Rect(rowX + titleRowW - tile, iconY, tile, tile));
            y += bannerH + gap;

            var panel = new Rect(centerX - panelW / 2, y, panelW, panelH);
            PixelGui.DrawNineSlice(skin.PanelGrey, panel, skin.TileSize, textScale);
            PixelGui.DrawTextCentered(skin.FontPlain, result, centerX, y + (panelH - lineH) / 2, textScale);
            y += panelH + gap;

            // 화면이 뜬 순간을 기준으로 깜빡여 처음에는 항상 켜진 상태로 시작한다.
            bool hintOn = Mathf.FloorToInt((Time.unscaledTime - shownAt) / hintBlinkInterval) % 2 == 0;
            if (hintOn)
                PixelGui.DrawTextCentered(skin.FontPlain, Hint, centerX, y, textScale);
        }

        static int CeilToMultiple(int value, int multiple)
        {
            return ((value + multiple - 1) / multiple) * multiple;
        }
    }
}
