using UnityEngine;
using UnityEngine.SceneManagement;

namespace PickShoot
{
    // 화면비를 16:9로 고정한다 (2026-10-14, PixelArtUI §1). 화면이 더 넓으면 양옆, 더 높으면 위아래에 검은 띠를 두고 그 안의 16:9 영역에만 그린다.
    // 이유 둘: 월드 시야가 화면비에 따라 달라지지 않아(21:9에서 아레나를 가로로 더 보고 스폰을 먼저 보던 것) 모두 같은 시야로 싸우고, 픽셀 UI의 기준 캔버스가
    // 늘 1920×1080이라(PixelGui) 화면 가장자리까지 쓰는 배치를 화면비마다 따로 맞출 일이 없다. 그전엔 캔버스 가로가 화면비를 따라 1440(4:3)~2520(21:9)로 바뀌어
    // 묶음을 가운데 두고 16:10(1728)에도 드는 폭으로 짰다.
    // 하는 일: 메인 카메라의 화면 영역(pixelRect)을 16:9 영역으로 좁히고 — 카메라의 aspect·뷰포트 좌표 변환이 따라온다 — 그 바깥 띠를 OnGUI에서 검게 칠한다
    // (카메라가 그리지 않는 화면 부분은 지워지지 않아 이전 프레임이 남는다). 띠는 다른 UI보다 위에 그린다(GUI.depth) — 띠로 나간 크로스헤어 등은 가려진다.
    // 씬에 두지 않고 실행 시 스스로 붙는다(RuntimeInitializeOnLoadMethod) — 씬과 무관한 표시 규칙이고, 씬 재시작(재로드) 뒤의 새 카메라도 다시 맞춘다.
    // 화면 전체를 덮던 막(일시정지·보상·데스 화면의 어두운 막, 피격 붉은 플래시)은 GuiRect 안만 덮는다.
    [DefaultExecutionOrder(-10000)]
    public class ScreenLetterbox : MonoBehaviour
    {
        public const int AspectWidth = 16;
        public const int AspectHeight = 9;
        // 띠를 다른 OnGUI보다 위에 (값이 작을수록 위).
        const int BarGuiDepth = -1000;

        Camera cam;

        // 화면 픽셀(원점 좌상단 — GUI 좌표)로 본 16:9 영역. 화면비가 정확히 16:9면 화면 전체. 위치·크기는 정수 픽셀이라 PixelGui의 화면 픽셀 맞춤이 그대로 맞는다.
        public static RectInt GuiRect
        {
            get
            {
                int sw = Screen.width, sh = Screen.height;
                int w, h;
                if ((long)sw * AspectHeight >= (long)sh * AspectWidth)
                {
                    h = sh;
                    w = Mathf.Min(sw, Mathf.RoundToInt(sh * (float)AspectWidth / AspectHeight));
                }
                else
                {
                    w = sw;
                    h = Mathf.Min(sh, Mathf.RoundToInt(sw * (float)AspectHeight / AspectWidth));
                }
                return new RectInt((sw - w) / 2, (sh - h) / 2, w, h);
            }
        }

        // 같은 영역을 카메라 좌표(원점 좌하단)로.
        static Rect CameraPixelRect
        {
            get
            {
                var r = GuiRect;
                return new Rect(r.x, Screen.height - r.y - r.height, r.width, r.height);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            var go = new GameObject(nameof(ScreenLetterbox));
            DontDestroyOnLoad(go);
            go.AddComponent<ScreenLetterbox>();
        }

        void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        // 씬의 Awake 뒤·Start 전에 불린다 — 카메라의 aspect를 Start에서 읽는 쪽(PlayerFollowCamera)이 처음부터 16:9를 본다.
        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            cam = null;
            Apply();
        }

        // 창 크기가 바뀌어도 따라가도록 매 프레임, 다른 스크립트의 Update보다 먼저.
        void Update()
        {
            Apply();
        }

        void Apply()
        {
            if (cam == null) cam = Camera.main;
            if (cam == null)
                throw new System.Exception("[PickShoot] ScreenLetterbox: 메인 카메라(태그 MainCamera)가 없음. 16:9 영역을 걸 카메라가 필요.");
            var r = CameraPixelRect;
            if (cam.pixelRect != r) cam.pixelRect = r;
        }

        void OnGUI()
        {
            // 순서는 모든 이벤트에서 같은 값이어야 한다.
            GUI.depth = BarGuiDepth;
            if (Event.current.type != EventType.Repaint) return;
            var r = GuiRect;
            if (r.width == Screen.width && r.height == Screen.height) return;
            var prev = GUI.color;
            GUI.color = Color.black;
            // 16:9 영역 바깥 네 띠 (둘은 늘 폭이나 높이가 0).
            GUI.DrawTexture(new Rect(0, 0, r.x, Screen.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax, 0, Screen.width - r.xMax, Screen.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, 0, r.width, r.y), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.yMax, r.width, Screen.height - r.yMax), Texture2D.whiteTexture);
            GUI.color = prev;
        }
    }
}
