# Unity 플레이 영상 자동 녹화 가이드

사람이 조작하지 않아도 **플레이 모드 진입 → (선택) 자동 플레이 → mp4 저장 → 플레이 모드 종료**를 한 번에 끝내는 방법. 일회성 에디터 스크립트 하나(+ 게임별 봇 하나)로 구현하며, 이 문서는 그 스크립트를 다시 만들 수 있을 만큼의 구조와 함정만 적는다.

> 원본: Pick&Shoot 저장소 `Docs/Guides/AutoPlayRecording.md`. 프로젝트 의존 내용이 없으므로 다른 Unity 프로젝트에는 이 파일을 그대로 복사해 쓴다. 개선점이 생기면 원본에 반영한다.
> 구현 예: Pick&Shoot의 `Assets/Editor/PlayRecorder.cs`(트리거·세션·Game view 복원, 프로젝트 비의존)와 `PickShootPlayDriver.cs`(게임별 봇). 아트 교체 전후 비교를 위해 보존 중이며, 메뉴 `PickShoot/Record Play`가 기본 설정(1080p·30fps·90초·봇)으로 요청 파일을 쓴다 — 파일명은 `play_{날짜}_{HEAD 짧은 해시}.mp4`.

- 검증: Unity 6000.3.10f1, Windows 10, DX12, Input System 1.18. Recorder 패키지 불필요.
- 산출물: H.264 mp4(게임 소리 포함, `audio=0`이면 영상만), 확인용 스냅샷 PNG, 진행 로그. 적용 예: Pick&Shoot에서 1080p 30fps 81초(자동 플레이 봇으로 웨이브 3까지).
- `Recordings/`는 Unity 표준 `.gitignore`에 포함(`/[Rr]ecordings/`). 없으면 추가.

## 1. 동작 원리

스크립트는 `Assets/Editor/` 아래 파일 하나. 상태는 전부 파일(`Recordings/record.*`)로 주고받는다. 플레이 모드 진입·종료 때 도메인 리로드가 두 번 일어나 static 상태가 날아가기 때문이다.

1. **트리거** — `[InitializeOnLoad]` 정적 클래스가 `EditorApplication.update`에서 0.5초마다 `record.request`를 확인. 있으면 내용을 `record.active`로 옮기고 `EditorApplication.EnterPlaymode()`.
2. **세션 시작** — `playModeStateChanged == EnteredPlayMode`에서 `record.active`가 있으면 숨은 GameObject를 만들어 세션 MonoBehaviour를 붙이고 `DontDestroyOnLoad`. 에디터 어셈블리의 MonoBehaviour라도 에디터 플레이 모드에서는 정상 동작한다(Unity Recorder도 같은 방식).
3. **캡처·인코딩** — 코루틴에서 1프레임 대기 후:
   - 해상도: `PlayModeWindow.SetCustomRenderingResolution(w, h, name)` → `Screen.width/height`가 바뀔 때까지 몇 프레임 대기.
   - `Time.captureFramerate = fps`로 게임 시간을 프레임당 1/fps로 고정(영상 길이가 실제 속도와 무관하게 정확해짐).
   - `new MediaEncoder(path, new VideoTrackAttributes { frameRate = new MediaRational(fps), width, height, bitRateMode = VideoBitrateMode.High })` (`UnityEditor.Media`).
   - 매 프레임 `WaitForEndOfFrame` → `ScreenCapture.CaptureScreenshotIntoRenderTexture(rt)` → **DX12는 상하 반전이므로** `Graphics.Blit(rt, flipRT, new Vector2(1,-1), new Vector2(0,1))` → `Texture2D.ReadPixels` → `encoder.AddFrame(tex)`. 해상도는 짝수로 맞춘다(H.264 제약). 첫 프레임과 중간 프레임은 PNG로도 저장해 방향 확인에 쓴다.
   - 소리(2026-10-14): 인코더를 `new MediaEncoder(path, video, new AudioTrackAttributes { sampleRate = new MediaRational(AudioSettings.outputSampleRate), channelCount = 2 })`로 만들고 `AudioRenderer.Start()`. 매 프레임 `AddFrame` 뒤 `AudioRenderer.GetSampleCountForCaptureFrame() × 채널 수` 크기의 `NativeArray<float>`에 `AudioRenderer.Render` → `encoder.AddSamples`. `captureFramerate`에 맞춰 오프라인으로 계산하므로 인코딩이 느려도 영상과 맞는다(Unity Recorder와 같은 방식). 녹화 중에는 스피커로 소리가 나지 않는다. 종료 시 `AudioRenderer.Stop()`을 Dispose보다 먼저.
4. **종료** — 목표 프레임 도달 시, 또는 `ExitingPlayMode`·`AssemblyReloadEvents.beforeAssemblyReload`에서 강제로: `encoder.Dispose()` → `captureFramerate = 0` → 입력 설정·Game view 크기 복원 → `record.done` 기록 → `record.active` 삭제 → `ExitPlaymode()`. 각 단계는 try/catch로 격리한다. **Dispose가 빠지면 moov atom 없는 깨진 mp4가 남는다.**

Game view 크기 복원은 공개 API가 없어 reflection: `UnityEditor.GameView.selectedSizeIndex`를 저장·복원하고, `GameViewSizes.instance.currentGroup`에서 `RemoveCustomSize`로 추가된 항목을 지운다(실패해도 로그만 남기고 진행). `GameViewSizes.instance`는 기반 클래스 `ScriptableSingleton<T>`의 static이므로 `GetProperty`에 `BindingFlags.FlattenHierarchy`를 넣어야 찾힌다.

## 2. 자동 플레이 (선택)

세션이 매 `Update`마다 호출하는 게임별 봇을 둔다. `Time.timeScale = 0`이어도 호출되게 해서 일시정지·선택 UI를 봇이 처리한다. 인터페이스(`Begin / Tick(frame) / End / Status`)를 두고 `TypeCache.GetTypesDerivedFrom<>`로 구현체를 자동 발견하면 프로젝트마다 봇 파일만 새로 쓰면 된다.

**Input System 프로젝트** — 가상 장치로 입력을 주입한다.
- `InputSystem.AddDevice<Keyboard>()`, `AddDevice<Mouse>()`. 추가된 장치가 `Keyboard.current`/`Mouse.current`가 되고, 매 프레임 이벤트를 보내면 유지된다.
- `InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.D))` — 눌린 키 **집합 전체**를 매 프레임 전송(빈 상태 = 모두 뗌). `wasPressedThisFrame`류는 한 프레임만 누른다.
- `QueueStateEvent(mouse, new MouseState { position = p }.WithButton(MouseButton.Left, true))` — `position`은 Game view 픽셀(좌하단 원점), `Camera.WorldToScreenPoint` 결과를 그대로 쓴다.
- **에디터는 Game view 포커스가 없으면 포인터/키보드 이벤트를 버린다.** 녹화 중에만 `InputSystem.settings.backgroundBehavior = IgnoreFocus`, `editorInputBehaviorInPlayMode = AllDeviceInputAlwaysGoesToGameView`로 바꾸고 종료 시 복원.
- 이 우회는 **사용자의 실제 마우스·키보드도 게임에 들여보낸다** — 뒤에서 찍는 동안 다른 앱에서 마우스를 움직이면 `Mouse.current`가 실제 마우스로 바뀌어 조준이 끌려가고, 타이핑은 이동·일시정지가 된다. 그래서 설정을 바꾼 직후, 봇이 가상 장치를 더하기 전에 지금 붙어 있는 장치를 모두 `InputSystem.DisableDevice`로 끄고 종료 때 `EnableDevice`로 되돌린다 (2026-10-14).
- IMGUI(OnGUI) 버튼은 에디터 이벤트, uGUI는 EventSystem을 타므로 가상 마우스로 눌리지 않는다. 선택 메서드를 직접 호출하거나 private 필드를 reflection으로 바꾼다. uGUI는 `Button.onClick.Invoke()`.

**레거시 Input Manager 프로젝트** — 가상 장치가 없다. 게임의 자동 플레이/디버그 모드를 켜거나, 입력 컴포넌트를 끄고 봇이 `Rigidbody`·공격 메서드를 직접 구동하거나, 입력 없이 진행되는 씬만 녹화한다.

봇 설계 요령: 매 프레임 `FindObjectsByType<T>`로 상태를 읽어도 프로토타입 규모면 충분하다. 회피(가까운 적 반발) + 목표(자원 끌림) + 경계 회피(카메라 범위)의 벡터 합을 8방향 키로 양자화하면 웨이브 서바이벌류는 수십 초를 사망 없이 버틴다. 사망 화면이 있으면 몇 초 뒤 재시작 키를 한 프레임 눌러 영상을 이어간다.

## 3. 사용 절차

1. 스크립트를 `Assets/Editor/`에 넣고(Pick&Shoot에는 이미 있음) **Unity 창을 한 번 클릭**한다. 에디터는 포커스를 받을 때만 재컴파일한다. `record.log`에 `loaded.` 줄이 찍히면 준비 완료. 컴파일 에러는 `%LOCALAPPDATA%\Unity\Editor\Editor.log`에서 `error CS`로 찾는다.
2. `Recordings/record.request`를 쓴다 (key=value, 한 줄에 하나. Pick&Shoot에서는 메뉴 `PickShoot/Record Play`가 대신 써 준다): `output`(mp4 절대 경로, 필수), `seconds`, `fps`, `flip`(DX12는 1), `width`/`height`(0이면 현재 크기), `restore_index`(되돌릴 Game view 크기 index, -1이면 시작 시점 값), `driver`(0이면 봇 없이 녹화), `audio`(0이면 소리 없이, 기본 1).
3. `record.done`이 생길 때까지 5초 간격으로 `record.log`를 꼬리 출력하며 기다린다. 정상 흐름: `request accepted → EnteredPlayMode → session started → 해상도 요청 → screen … -> video … → progress N/M(10초마다) → finishing → finish`. 1080p는 프레임당 약 35ms라 90초 영상에 100초 안팎 걸린다.
4. 검증: `ffmpeg -i play.mp4`로 길이·해상도 확인(`moov atom not found`면 깨진 파일), `ffmpeg -ss 30 -i play.mp4 -frames:v 1 f.png`로 프레임을 뽑아 HUD가 위에 있는지 본다. ffmpeg 없이는 `snapshot_*.png`로 방향만 확인. PATH에 없어도 다른 프로그램에 딸린 ffmpeg가 있을 수 있다(예: 이 PC는 `C:\Program Files\File Converter\ffmpeg.exe`).
5. 정리: 일회성이면 스크립트 삭제(프로젝트 규칙에 따름. Pick&Shoot은 아트 비교가 끝날 때까지 보존), `record.active`·`record.done`·`snapshot_*.png` 삭제, Game view 크기 확인, `git status` 확인.

## 4. 함정

| 증상 | 원인 → 해결 |
|---|---|
| `session started` 뒤 진행 없음, 예외도 없음 | **Unity 창이 완전히 가려지면**(전체화면 게임, 다른 창 뒤) 에디터 루프가 멈춘다. 포커스는 없어도 되지만 보여야 한다 → 창을 보이게 한다. 게임 포커스를 뺏지 않으려면 `SetWindowPos(HWND_TOPMOST, SWP_NOACTIVATE)`로 다른 모니터에 잠시 띄웠다 되돌린다. 백그라운드 프로세스의 `SetForegroundWindow`는 Windows가 막는다. 단, 2026-10-14에 "포커스를 잃으면 멈춤"(`runInBackground`, 아래 표)이 따로 있음을 확인했다 — 이전의 "가려지면 멈춤" 관찰에 그 원인이 섞여 있었을 수 있고, `runInBackground`를 켠 뒤 완전히 가려진 경우는 아직 다시 시험하지 않았다 |
| 영상 상하 반전 | DX11/DX12 캡처 특성 → `flip=1`. 다른 그래픽 API에서 반대로 뒤집히면 0 |
| ffmpeg로 뽑은 프레임이 게임보다 밝고 초록이 튐 | `MediaEncoder`의 H.264는 BT.709로 인코딩하지만 색 태그를 남기지 않아 ffmpeg(3.2에서 확인)가 BT.601로 푼다 → RGB로 뽑을 때(PNG·GIF·접촉 시트) `scale=in_color_matrix=bt709`를 앞에 두고, 다시 인코딩한 mp4에는 `-colorspace bt709 -color_primaries bt709 -color_trc bt709`로 태그를 단다. 확인은 외곽선·고정색 픽셀이 시트 값과 같은지로 (2026-10-14) |
| 봇이 안 움직임 | Game view 포커스 없음 → §2의 두 설정 우회 확인 |
| mp4 재생 불가 | Dispose 전 중단(디스크 부족이 실제 사례) → 디스크 여유 확인, 종료 훅 확인 |
| 사람이 Play를 눌렀는데 봇이 돌고 해상도가 바뀜 | 이전 `record.active`가 남아 다음 플레이 진입을 가로챔 → 시작 전 삭제 |
| Game view 크기가 커스텀 항목으로 남음 | 복원 reflection 실패 → 드롭다운에서 되돌리고 커스텀 항목 제거 |
| 로그에 `loaded. isPlaying=False`가 세션 도중 섞임 | AssetImportWorker 프로세스가 같은 로그에 쓰는 것 → 무시 |
| 씬 파일을 외부(스크립트)에서 고친 뒤 리프레시가 멈춤 | 열린 씬이 바뀌면 "The open scene(s) have been modified externally" 모달이 뜬다 → 다이얼로그의 `Reload` 버튼에 `PostMessage(WM_COMMAND)`를 보내 누른다(포커스 불필요). 처음부터 포커스 없이 리프레시하려면 폴링 스크립트에 요청 파일 → `AssetDatabase.Refresh()` 명령을 넣어 둔다 |
| 폴링은 도는데 게임 시간이 안 흐름(`FixedUpdate`·`Update` 멈춤) | Player 설정 `runInBackground: 0`이면 에디터가 포커스를 잃을 때 플레이 루프가 멈춘다 → 세션 중에만 `Application.runInBackground = true`(런타임 값, 프로젝트 설정은 그대로)로 켜고 종료 때 복원 — 세션 MonoBehaviour의 `Awake`(진입 콜백 안, 첫 프레임 전)에서 켠다. Pick&Shoot은 이 설정이 꺼져 있는데 녹화기에 빠져 있다가 2026-10-14에 넣었다: 그전까지는 플레이 진입이 에디터에 포커스를 줘서(아래 줄) 우연히 돌았고, 사용자가 다른 창으로 돌아가면 멈췄다 |
| 녹화가 시작될 때마다 Unity 창이 앞으로 튀어나와 사용자가 쓰던 창을 가로챔 | Game view의 기본값(Play Focused)이 플레이 진입 때 포커스를 가져간다 → 진입 직전 `PlayModeWindow.SetPlayModeFocused(false)`, 원래 값은 `record.active`에 적어 두고 세션 종료(또는 중단된 세션 정리)에서 되돌린다. 위 줄과 함께 넣으면 사용자가 다른 앱을 쓰는 동안에도 정상 속도로 찍힌다 (2026-10-14 확인) |
| `ScreenCapture`가 파일을 안 남김 | Game view가 그려지지 않는 상태(에디터 백그라운드) → 카메라를 직접 렌더: `RenderPipeline.SubmitRenderRequest(cam, new RenderPipeline.StandardRequest { destination = rt })` → `ReadPixels`. IMGUI HUD는 빠진다 |
| 셋업 스크립트의 `SerializedObject.FindProperty`가 null (6000.3) | 직렬화 이름이 YAML 키와 다르다: Physics2D 반발 임계값은 `m_BounceThreshold`(옛 키 `m_VelocityThreshold`), TimeManager `Fixed Timestep`은 유리수 구조체(`.m_Count`, 초당 틱 `.m_Rate.m_Numerator`/`m_Denominator`) → `GetIterator()`로 속성 경로를 덤프해 확인 |
| 셋업이 만든 SO 참조가 씬에 null로 저장됨 | `CreateInstance`+`CreateAsset` 뒤 `EditorSceneManager.OpenScene(…, Single)`을 하면 그 전의 C# 참조가 끊긴다 → 씬을 연 뒤 `LoadAssetAtPath`로 다시 읽어 연결 |

Claude Code로 운용할 때: "예외 없이 멈춤"이면 코드보다 먼저 창 가려짐을 의심하고, `System.Drawing.Graphics.CopyFromScreen`으로 실제 화면을 캡처해 확인하는 것이 가장 빠르다.
