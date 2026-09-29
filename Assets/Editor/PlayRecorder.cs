using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Media;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PickShoot
{
    // 플레이 영상 자동 녹화 도구 (메뉴 PickShoot/Record Play, 또는 Recordings/record.request 파일). 장기 보존 도구 (DEVELOPMENT §2.1) —
    // 아트 교체 전후를 같은 봇·해상도·길이로 찍어 비교하는 동안 둔다. 구조·절차·함정은 Docs/Guides/AutoPlayRecording.md.
    // 흐름: 요청 파일 → 플레이 모드 진입 → 세션 MonoBehaviour가 봇(IPlayDriver)을 돌리며 매 프레임을 MediaEncoder에 넣는다 → 플레이 종료.
    // 상태는 전부 Recordings/record.* 파일로 주고받는다 — 플레이 모드 진입·종료의 도메인 리로드가 static 상태를 지우기 때문.
    public interface IPlayDriver
    {
        string Status { get; }
        void Begin();
        // 세션이 매 Update마다 부른다. Time.timeScale이 0이어도 불리므로 카드 선택·사망 화면은 봇이 처리한다.
        void Tick(int frame);
        void End();
    }

    [InitializeOnLoad]
    public static class PlayRecorderTrigger
    {
        public const string CustomSizeName = "Recorder";
        const int DefaultSeconds = 90;
        const int DefaultFps = 30;
        const int DefaultWidth = 1920;
        const int DefaultHeight = 1080;
        const double PollInterval = 0.5;
        static double nextPoll;

        public static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);
        public static string RecordingsDir => Path.Combine(ProjectRoot, "Recordings");
        public static string RequestPath => Path.Combine(RecordingsDir, "record.request");
        public static string ActivePath => Path.Combine(RecordingsDir, "record.active");
        public static string DonePath => Path.Combine(RecordingsDir, "record.done");
        public static string LogPath => Path.Combine(RecordingsDir, "record.log");

        static PlayRecorderTrigger()
        {
            // AssetImportWorker 프로세스도 이 어셈블리를 로드한다. 거기서 폴링·정리를 하면 진행 중인 세션의 active 파일을 지우므로 빠진다.
            if (AssetDatabase.IsAssetImportWorkerProcess()) return;
            Directory.CreateDirectory(RecordingsDir);
            EditorApplication.update += Poll;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            // 중단된 세션이 남긴 active 파일은 다음 수동 플레이를 가로채므로 치운다. 플레이 진입 리로드 중에는 살아 있어야 한다.
            if (!EditorApplication.isPlayingOrWillChangePlaymode && File.Exists(ActivePath))
            {
                File.Delete(ActivePath);
                Log("stale record.active removed.");
            }
            Log($"loaded. isPlaying={EditorApplication.isPlaying}");
        }

        // 기본 설정(1080p·30fps·90초·봇)으로 요청 파일을 쓴다. 파일명에 날짜와 HEAD 커밋 짧은 해시를 넣어 어느 상태를 찍었는지 남긴다.
        [MenuItem("PickShoot/Record Play")]
        static void RecordPlayMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new Exception("[PickShoot] Record Play: 플레이 모드에서는 시작할 수 없습니다. 멈춘 뒤 다시 실행하세요.");
            string output = Path.Combine(RecordingsDir, $"play_{DateTime.Now:yyyy-MM-dd}_{GitShortHash()}.mp4");
            File.WriteAllText(RequestPath,
                $"output={output}\nseconds={DefaultSeconds}\nfps={DefaultFps}\nflip=1\nwidth={DefaultWidth}\nheight={DefaultHeight}\ndriver=1\nrestore_index=-1\n");
            Log($"request written by menu: {output}");
        }

        static string GitShortHash()
        {
            string git = Path.Combine(ProjectRoot, ".git");
            string head = File.ReadAllText(Path.Combine(git, "HEAD")).Trim();
            string hash = head;
            if (head.StartsWith("ref: "))
            {
                string refName = head.Substring(5).Trim();
                string refPath = Path.Combine(git, refName.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(refPath))
                {
                    hash = File.ReadAllText(refPath).Trim();
                }
                else
                {
                    hash = null;
                    foreach (var line in File.ReadAllLines(Path.Combine(git, "packed-refs")))
                        if (line.EndsWith(" " + refName)) { hash = line.Substring(0, line.IndexOf(' ')); break; }
                    if (hash == null)
                        throw new Exception($"[PickShoot] Record Play: git ref를 찾지 못함 ({refName}).");
                }
            }
            if (hash.Length < 7)
                throw new Exception($"[PickShoot] Record Play: git HEAD 해시가 이상함 ({hash}).");
            return hash.Substring(0, 7);
        }

        static void Poll()
        {
            if (EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + PollInterval;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!File.Exists(RequestPath)) return;

            string text = File.ReadAllText(RequestPath);
            File.Delete(RequestPath);
            if (File.Exists(DonePath)) File.Delete(DonePath);
            File.WriteAllText(ActivePath, text);
            Log("request accepted: " + text.Trim().Replace("\r", "").Replace("\n", " | "));
            EditorApplication.EnterPlaymode();
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            if (!File.Exists(ActivePath)) return;
            Log("EnteredPlayMode: starting session.");
            new GameObject("PlayRecorderSession").AddComponent<PlayRecorderSession>();
        }

        public static void Log(string message)
        {
            File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss} {message}\n");
        }
    }

    // 플레이 모드 안에서 녹화 한 회를 맡는다. 진입 직후 PlayRecorderTrigger가 붙이며 씬이 다시 로드돼도(재시작) 살아남는다.
    // 종료 훅(ExitingPlayMode·beforeAssemblyReload·OnDestroy)에서 Finish를 불러 어떤 경로로 끝나도 encoder를 Dispose한다 —
    // 빠지면 moov atom 없는 깨진 mp4가 남는다. 각 복원 단계는 따로 격리해 하나가 실패해도 나머지는 진행한다.
    public class PlayRecorderSession : MonoBehaviour
    {
        string output;
        int seconds, fps, width, height, restoreIndex;
        bool flip, useDriver;

        MediaEncoder encoder;
        RenderTexture captureRT, flipRT;
        Texture2D frameTex;
        IPlayDriver driver;
        int frame, totalFrames;
        bool capturing, finished;
        int savedSizeIndex = -1;
        bool sizeChanged, inputChanged;
        InputSettings.BackgroundBehavior savedBackground;
        InputSettings.EditorInputBehaviorInPlayMode savedEditorInput;

        void Awake()
        {
            DontDestroyOnLoad(gameObject);
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeReload;
        }

        void Start()
        {
            try
            {
                ReadConfig();
                StartCoroutine(Run());
            }
            catch (Exception e) { Fail(e); }
        }

        void Update()
        {
            if (!capturing || driver == null) return;
            try { driver.Tick(frame); }
            catch (Exception e) { Fail(e); }
        }

        void OnDestroy() => Finish("OnDestroy");
        void OnPlayModeChanged(PlayModeStateChange state) { if (state == PlayModeStateChange.ExitingPlayMode) Finish("ExitingPlayMode"); }
        void OnBeforeReload() => Finish("beforeAssemblyReload");

        void ReadConfig()
        {
            var values = new Dictionary<string, string>();
            foreach (var raw in File.ReadAllLines(PlayRecorderTrigger.ActivePath))
            {
                string line = raw.Trim();
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                values[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
            }
            if (!values.TryGetValue("output", out output) || output.Length == 0)
                throw new Exception("[PickShoot] PlayRecorder: record.request에 output(mp4 경로)이 없음.");
            if (!Path.IsPathRooted(output)) output = Path.Combine(PlayRecorderTrigger.ProjectRoot, output);
            seconds = ReadInt(values, "seconds", 90);
            fps = ReadInt(values, "fps", 30);
            flip = ReadInt(values, "flip", 1) != 0;
            width = ReadInt(values, "width", 0);
            height = ReadInt(values, "height", 0);
            useDriver = ReadInt(values, "driver", 1) != 0;
            restoreIndex = ReadInt(values, "restore_index", -1);
            if (seconds <= 0 || fps <= 0)
                throw new Exception($"[PickShoot] PlayRecorder: seconds({seconds})·fps({fps})는 양수여야 함.");
        }

        static int ReadInt(Dictionary<string, string> values, string key, int fallback)
        {
            if (!values.TryGetValue(key, out var text)) return fallback;
            if (!int.TryParse(text, out int result))
                throw new Exception($"[PickShoot] PlayRecorder: {key}={text} 를 정수로 읽을 수 없음.");
            return result;
        }

        IEnumerator Run()
        {
            yield return null;
            if (useDriver) driver = CreateDriver();

            if (width > 0 && height > 0)
            {
                savedSizeIndex = GameViewSizeUtil.ReadSelectedIndex();
                PlayModeWindow.SetCustomRenderingResolution((uint)width, (uint)height, PlayRecorderTrigger.CustomSizeName);
                sizeChanged = true;
                PlayRecorderTrigger.Log($"resolution requested {width}x{height} (saved index {savedSizeIndex})");
                for (int waited = 0; waited < 120 && (Screen.width != width || Screen.height != height); waited++)
                    yield return null;
                if (Screen.width != width || Screen.height != height)
                {
                    Fail(new Exception($"[PickShoot] PlayRecorder: Game view가 {width}x{height}로 바뀌지 않음 (현재 {Screen.width}x{Screen.height})."));
                    yield break;
                }
            }
            else
            {
                width = Screen.width;
                height = Screen.height;
            }
            if (width % 2 != 0 || height % 2 != 0)
            {
                Fail(new Exception($"[PickShoot] PlayRecorder: 해상도는 짝수여야 함 (H.264) — {width}x{height}."));
                yield break;
            }
            PlayRecorderTrigger.Log($"screen {Screen.width}x{Screen.height} -> video {width}x{height} {fps}fps {seconds}s flip={flip} driver={(driver != null ? driver.GetType().Name : "none")}");

            try
            {
                ApplyInputSettings();
                driver?.Begin();
                Time.captureFramerate = fps;
                if (File.Exists(output)) File.Delete(output);
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                encoder = new MediaEncoder(output, new VideoTrackAttributes
                {
                    frameRate = new MediaRational(fps),
                    width = (uint)width,
                    height = (uint)height,
                    includeAlpha = false,
                    bitRateMode = VideoBitrateMode.High,
                });
                captureRT = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
                flipRT = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
                frameTex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                totalFrames = seconds * fps;
                capturing = true;
                PlayRecorderTrigger.Log($"capture start: {output}");
            }
            catch (Exception e)
            {
                Fail(e);
                yield break;
            }

            while (frame < totalFrames && !finished)
            {
                yield return new WaitForEndOfFrame();
                if (finished) break;
                try
                {
                    CaptureFrame();
                    frame++;
                    if (frame == 1 || frame == totalFrames / 2) SaveSnapshot();
                    if (frame % (fps * 10) == 0)
                        PlayRecorderTrigger.Log($"progress {frame}/{totalFrames} {(driver != null ? driver.Status : "")}");
                }
                catch (Exception e)
                {
                    Fail(e);
                    yield break;
                }
            }
            PlayRecorderTrigger.Log("finishing");
            Finish("complete");
            EditorApplication.ExitPlaymode();
        }

        static IPlayDriver CreateDriver()
        {
            foreach (var type in TypeCache.GetTypesDerivedFrom<IPlayDriver>())
                if (!type.IsAbstract && !type.IsInterface)
                    return (IPlayDriver)Activator.CreateInstance(type);
            throw new Exception("[PickShoot] PlayRecorder: IPlayDriver 구현이 없음 (driver=0으로 봇 없이 녹화하거나 봇을 추가).");
        }

        // 에디터는 Game view에 포커스가 없으면 가상 장치의 이벤트도 버리므로 녹화 중에만 우회한다. 프로젝트에 InputSettings 자산이
        // 없어 값은 저장되지 않지만, 있는 경우를 위해 종료 시 되돌린다.
        void ApplyInputSettings()
        {
            var settings = InputSystem.settings;
            savedBackground = settings.backgroundBehavior;
            savedEditorInput = settings.editorInputBehaviorInPlayMode;
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            inputChanged = true;
        }

        void RestoreInputSettings()
        {
            if (!inputChanged) return;
            inputChanged = false;
            InputSystem.settings.backgroundBehavior = savedBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = savedEditorInput;
        }

        // DX11/DX12 캡처는 상하 반전이라 flip=1이면 Blit으로 뒤집는다.
        void CaptureFrame()
        {
            ScreenCapture.CaptureScreenshotIntoRenderTexture(captureRT);
            RenderTexture source = captureRT;
            if (flip)
            {
                Graphics.Blit(captureRT, flipRT, new Vector2(1f, -1f), new Vector2(0f, 1f));
                source = flipRT;
            }
            var previous = RenderTexture.active;
            RenderTexture.active = source;
            frameTex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            frameTex.Apply(false);
            RenderTexture.active = previous;
            encoder.AddFrame(frameTex);
        }

        void SaveSnapshot()
        {
            File.WriteAllBytes(Path.Combine(PlayRecorderTrigger.RecordingsDir, $"snapshot_{frame}.png"), frameTex.EncodeToPNG());
        }

        void Fail(Exception e)
        {
            PlayRecorderTrigger.Log("ERROR " + e);
            Debug.LogException(e);
            Finish("error");
            EditorApplication.ExitPlaymode();
        }

        void Finish(string reason)
        {
            if (finished) return;
            finished = true;
            capturing = false;
            PlayRecorderTrigger.Log($"finish ({reason}) frames={frame}");
            Try("encoder dispose", () => { encoder?.Dispose(); encoder = null; });
            Try("captureFramerate", () => Time.captureFramerate = 0);
            Try("driver end", () => driver?.End());
            Try("input restore", RestoreInputSettings);
            Try("game view restore", () =>
            {
                if (sizeChanged) GameViewSizeUtil.Restore(restoreIndex >= 0 ? restoreIndex : savedSizeIndex, PlayRecorderTrigger.CustomSizeName);
            });
            Try("textures", () =>
            {
                if (captureRT != null) captureRT.Release();
                if (flipRT != null) flipRT.Release();
            });
            File.WriteAllText(PlayRecorderTrigger.DonePath, $"reason={reason}\nframes={frame}\noutput={output}\n");
            if (File.Exists(PlayRecorderTrigger.ActivePath)) File.Delete(PlayRecorderTrigger.ActivePath);
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeReload;
        }

        static void Try(string what, Action action)
        {
            try { action(); }
            catch (Exception e) { PlayRecorderTrigger.Log($"{what} failed: {e}"); }
        }
    }

    // Game view 크기는 공개 API로 되돌릴 수 없어 reflection. selectedSizeIndex를 되돌리고 SetCustomRenderingResolution이 추가한
    // 커스텀 항목을 지운다. GameViewSizes.instance는 기반 ScriptableSingleton<T>의 static이라 FlattenHierarchy가 필요하다.
    static class GameViewSizeUtil
    {
        const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy;
        static readonly Assembly EditorAssembly = typeof(EditorWindow).Assembly;
        static readonly Type GameViewType = EditorAssembly.GetType("UnityEditor.GameView");

        static EditorWindow FindGameView()
        {
            var views = Resources.FindObjectsOfTypeAll(GameViewType);
            return views.Length > 0 ? (EditorWindow)views[0] : null;
        }

        static PropertyInfo Property(Type type, string name)
        {
            var property = type.GetProperty(name, Any);
            if (property == null)
                throw new Exception($"[PickShoot] PlayRecorder: {type.Name}.{name} 프로퍼티를 찾지 못함 (Unity 버전 차이).");
            return property;
        }

        static MethodInfo Method(Type type, string name)
        {
            var method = type.GetMethod(name, Any);
            if (method == null)
                throw new Exception($"[PickShoot] PlayRecorder: {type.Name}.{name} 메서드를 찾지 못함 (Unity 버전 차이).");
            return method;
        }

        public static int ReadSelectedIndex()
        {
            var view = FindGameView();
            if (view == null) return -1;
            return (int)Property(GameViewType, "selectedSizeIndex").GetValue(view);
        }

        public static void Restore(int index, string customName)
        {
            var view = FindGameView();
            if (view != null && index >= 0)
                Property(GameViewType, "selectedSizeIndex").SetValue(view, index);

            var sizesType = EditorAssembly.GetType("UnityEditor.GameViewSizes");
            object sizes = Property(sizesType, "instance").GetValue(null);
            object group = Property(sizes.GetType(), "currentGroup").GetValue(sizes);
            var groupType = group.GetType();
            int total = (int)Method(groupType, "GetTotalCount").Invoke(group, null);
            var getSize = Method(groupType, "GetGameViewSize");
            var remove = Method(groupType, "RemoveCustomSize");
            int removed = 0;
            for (int i = total - 1; i >= 0; i--)
            {
                object size = getSize.Invoke(group, new object[] { i });
                string text = (string)Property(size.GetType(), "baseText").GetValue(size);
                if (text != customName) continue;
                remove.Invoke(group, new object[] { i });
                removed++;
            }
            PlayRecorderTrigger.Log($"game view restored: index={index} removed custom sizes={removed}");
        }
    }
}
