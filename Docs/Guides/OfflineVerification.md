# 에디터 없이 검증하기 — 컴파일 검사 · 로직 하니스 · 스프라이트 시트 디코드

코드나 UI를 바꿀 때마다 에디터 왕복(포커스 → 재컴파일 → 플레이 → 눈으로 확인)을 하면 느리고, 사람이 창을 보고 있어야 한다. 에디터에 넣기 전에 셸에서 끝낼 수 있는 검증 세 가지를 적는다. 목표는 **에디터에서는 "맞는지 확인"만 하고, "될지 안 될지"는 미리 거르는 것**이다.

> 원본: Pick&Shoot 저장소 `Docs/Guides/OfflineVerification.md`. 프로젝트 의존 내용이 없으므로 다른 Unity 프로젝트에는 이 파일을 그대로 복사해 쓴다. 개선점이 생기면 원본에 반영한다. 이 문서 위에 프로젝트의 UI 규칙을 얹어 목업을 만드는 예는 Pick&Shoot의 `Docs/Guides/PixelUiMockup.md`.

- 검증: Windows 10, Unity Hub 설치본. §1이 쓰는 폴더 구조(Roslyn·dotnet 런타임·netstandard 2.1 참조)는 이 PC에 설치된 2021.3·2022.3·2023.2·6000.3에서 같고, **2020.3에는 셋 다 없어 쓸 수 없다**. 2026-10-07 Pick&Shoot(6000.3.10f1) 58파일 오류 0. §2는 dotnet SDK 9, §3은 Python 3 + Pillow 12.
- 스크립트는 세션 스크래치에만 두고 저장소에 넣지 않는다(일회성 자동화). 다음에 필요하면 이 문서의 조각에서 다시 시작한다. 목업 PNG만 `Recordings/`(Unity 표준 `.gitignore`의 `/[Rr]ecordings/`)에 남긴다.
- 셋 다 에디터와 무관하게 돈다. 에디터가 열려 있어도 되지만, 에디터가 임포트 중이면 §1이 `Library/`의 패키지 dll을 못 읽을 수 있다.

## 1. 컴파일 검사 (Unity 번들 Roslyn)

에디터는 포커스를 받아야 재컴파일하고, 오류는 Console과 `Editor.log`에만 남는다. 같은 Roslyn이 에디터 설치 폴더에 있어 명령 하나로 전체 스크립트를 컴파일할 수 있다. 경고는 보지 않고 **오류 0 · exit 0**만 본다.

- 컴파일러: `$U/NetCoreRuntime/dotnet.exe $U/DotNetSdkRoslyn/csc.dll @check.rsp`. `$U`는 `C:/Program Files/Unity/Hub/Editor/<버전>/Editor/Data`, 버전은 `ProjectSettings/ProjectVersion.txt`에서 읽는다.
- **응답 파일(rsp)을 쓴다.** `Program Files`의 공백 때문에 셸 루프로 `-r:`를 이어 붙이면 인자가 깨진다. 한 줄에 옵션 하나, 경로는 큰따옴표.
- 옵션: `-nologo -target:library -nostdlib -langversion:9.0 -nowarn:CS0649,CS0169,CS0414`(미할당 `[SerializeField]` 경고 등은 의미 없음) `-out:<스크래치>/check.dll`. 언어 버전은 에디터가 지원하는 것에 맞춘다(2021.2 이상 9.0).
- 참조: `NetStandard/ref/2.1.0/netstandard.dll`, `Managed/UnityEngine/UnityEngine.*Module.dll` 전부, `Managed/UnityEditor.dll`, **모듈 폴더 안의** `Managed/UnityEngine/UnityEngine.dll`(type-forward 퍼사드). 패키지 어셈블리는 프로젝트의 `Library/ScriptAssemblies/*.dll` 전부에서 **`Assembly-CSharp*.dll`만 뺀 것** — 프로젝트 자신의 옛 빌드 결과를 참조하면 같은 형식이 양쪽에 있어 CS0436 경고가 쏟아지고, 지운 형식이 옛 dll에서 잡혀 오류를 놓친다. `Library/ScriptAssemblies`는 에디터가 한 번 임포트한 뒤에만 있다.
- 소스: `Assets/**/*.cs` 전부(Editor 폴더 포함). asmdef로 나눈 프로젝트도 보통 한 번에 넘기면 된다.

rsp를 만들고 실행까지 하는 스크립트. 프로젝트 루트에서 `python compile_check.py <스크래치 폴더>`:

```python
import glob, os, re, subprocess, sys
S = sys.argv[1]; rsp = S + '/check.rsp'
ver = re.search(r'm_EditorVersion: (\S+)', open('ProjectSettings/ProjectVersion.txt').read()).group(1)
U = f'C:/Program Files/Unity/Hub/Editor/{ver}/Editor/Data'            # Windows Unity Hub 기본 설치 경로
refs = [U + '/NetStandard/ref/2.1.0/netstandard.dll']
refs += sorted(glob.glob(U + '/Managed/UnityEngine/UnityEngine.*Module.dll'))
refs += [U + '/Managed/UnityEditor.dll', U + '/Managed/UnityEngine/UnityEngine.dll']   # Editor 스크립트용. 퍼사드는 모듈 폴더 안의 것
refs += [d for d in sorted(glob.glob('Library/ScriptAssemblies/*.dll'))             # 패키지 어셈블리(에디터가 한 번 임포트한 뒤에만 있다).
         if not os.path.basename(d).startswith('Assembly-CSharp')]                   # 프로젝트 자신의 빌드 결과는 빼야 지금 소스가 검사된다
assert any('ScriptAssemblies' in r for r in refs), 'Library/ScriptAssemblies가 비어 있음 — 에디터로 한 번 임포트할 것'
for r in refs: assert os.path.exists(r), r
src = sorted(glob.glob('Assets/**/*.cs', recursive=True))
lines = ['-nologo', '-target:library', '-nostdlib', '-langversion:9.0', '-nowarn:CS0649,CS0169,CS0414', f'-out:"{S}/check.dll"']
lines += [f'-r:"{r}"' for r in refs] + [f'"{s}"' for s in src]
open(rsp, 'w', encoding='utf-8').write('\n'.join(lines) + '\n')
p = subprocess.run([U + '/NetCoreRuntime/dotnet.exe', U + '/DotNetSdkRoslyn/csc.dll', '@' + rsp], capture_output=True, text=True)
print(f'sources={len(src)} refs={len(refs)} exit={p.returncode}'); print((p.stdout + p.stderr).strip() or '(no diagnostics)'); sys.exit(p.returncode)
```

- 통과 기준: `exit=0`. CS0618(deprecated API) 같은 경고는 에디터와 같은 것이라 그대로 둔다.
- 한계: 에디터의 define 심볼을 재현하지 않으므로 `#if UNITY_EDITOR` 안은 컴파일되지 않는다. 그 안까지 보려면 `-define:UNITY_EDITOR`를 더한다. 런타임·Editor 스크립트를 한 어셈블리로 묶어 컴파일하므로 런타임 코드가 `UnityEditor`를 직접 쓰는 실수는 잡히지 않는다(플레이어 빌드에서 걸린다). 런타임 동작(null 참조·직렬화 누락)도 못 잡는다 — 그건 §2 또는 에디터.

## 2. 로직 실행 (스텁 하니스)

입력이 정해지면 출력이 정해지는 코드(배치·계산·규칙)를 에디터 없이 실제로 돌려 본다. 적용 예: Pick&Shoot에서 2026-09-17 `ArenaDecor`의 울타리·잔무늬 타일 배치를 하니스로 돌리고 Python 목업과 칸 단위로 대조.

1. 스크래치에 콘솔 프로젝트(`dotnet new console`, SDK 9). csproj에 검사할 프로젝트 .cs를 `<Compile Include="…/Assets/Scripts/…/Foo.cs" />`로 **끌어온다**(복사하면 원본과 어긋난다).
2. 그 파일이 쓰는 UnityEngine 형식을 **같은 이름의 네임스페이스에 최소 스텁**으로 만든다 — `MonoBehaviour`(빈 클래스), `Vector2Int`·`RectInt`(쓰는 연산만), `GameObject`·`Transform`·`SpriteRenderer`(호출을 목록에 기록만), `Sprite`, `Debug.Log*`, `SerializeField`·`Min` 어트리뷰트. 컴파일러가 없다고 하는 멤버만 하나씩 채우면 된다.
3. `Program.cs`에서 인스턴스를 만들고 `[SerializeField]` private 필드는 리플렉션으로 주입(`GetField(name, NonPublic | Instance).SetValue`), `Awake`·`Start`도 `GetMethod(..., NonPublic | Instance).Invoke`로 부른다. 결과는 스텁이 기록한 "어느 칸에 어떤 스프라이트" 목록을 텍스트나 JSON으로 출력.
4. 같은 규칙을 Python으로 짠 목업과 칸 단위로 diff한다. 둘이 같아야 "코드가 기획대로"다 — 목업만 맞고 코드가 다르면 코드를, 둘 다 같은데 이상하면 기획을 고친다.

- 난수: 코드가 자체 난수(xorshift 등)나 시드 고정 `System.Random`을 쓰면 하니스와 Python이 같은 수열을 낼 수 있다. `UnityEngine.Random`은 스텁으로 같은 수열을 재현할 수 없으므로 그런 코드는 규칙만 대조한다.
- 물리·코루틴·프레임 순서·씬 참조에 기대는 코드는 하니스 대상이 아니다. 그런 건 에디터에서 본다.
- 스텁은 검사 대상 하나에 맞춘 일회성이다. 다른 파일에 재활용하려 들지 않는다 — 스텁이 커지면 Unity 동작을 흉내 내기 시작해 믿을 수 없어진다.

## 3. 스프라이트 시트·SO 자산 디코드 (Python + Pillow)

ScriptableObject·프리팹·`.meta`는 전부 YAML 텍스트라 에디터 없이 읽을 수 있다. UI 안을 실제 시트 조각으로 합성해 비교하는 목업, 문구가 고정 폭에 들어가는지 재는 검사 등의 재료다.

**자산 푸는 법**
- 스프라이트 참조는 `{fileID, guid}`. guid → 시트의 `.meta`(헤더 `guid:`; `Assets/**/*.meta`를 한 번 훑어 사전을 만든다), fileID → 그 `.meta` 안 스프라이트의 `internalID`. 스프라이트 `rect`(x·y·width·height)의 `y`는 **아래 원점**이라 PIL에서는 `top = 시트 높이 − y − height`.
- SO의 int 배열은 **리틀엔디언 int32의 hex 문자열**로 저장된다(8자 = 값 1개). 색은 `{r, g, b, a}` 0~1 float, 문자열은 YAML 따옴표.
- 시트는 Point 필터·정수 PPU가 전제. 합성은 **정수 배율 · NEAREST**로만 한다.

**목업 원칙** — 코드와 같은 조건이 아니면 비교할 가치가 없다.
- 프로젝트의 기준 캔버스 크기·배율·배치 상수(여백·간격·타일 수)는 그리는 코드에서 **읽어서** 그대로 쓴다. 비슷하게 잡지 않는다.
- 다른 해상도도 프로젝트가 실제로 축소하는 방식대로 함께 본다. 얇은 획·1텍셀 테두리가 빠지는 문제는 작은 해상도에서만 보이므로, 확인할 부분을 크롭해 실제 크기와 2배 확대 둘 다 둔다.
- 바탕은 실제 조건으로(게임 바닥을 깔고 필요하면 어둡게 덮는다). 단색 바탕에서 좋아 보이던 안이 실제 바탕에서 묻히는 걸 거른다.
- 여러 안을 한 장에 나란히 두고, 안 이름·설명도 게임의 비트맵 글꼴로 적는다. 한 화면처럼 읽혀야 눈이 안끼리 비교한다.
- 산출물은 `Recordings/<주제>_mockup_<YYYY-MM-DD>[_vN].png`. PNG는 로컬에만 있으므로 고른 안과 버린 안의 이유, 파일명을 문서에 적는다.

공통 조각(프로젝트 루트에서 실행). 프로젝트의 스킨·글꼴 구조를 읽는 부분은 이 뒤에 이어 붙인다(예: `PixelUiMockup.md`):

```python
import re, struct, glob
from PIL import Image

# guid -> .meta 경로. {fileID, guid} 참조에서 파일을 찾는 열쇠.
guid2meta = {}
for m in glob.glob('Assets/**/*.meta', recursive=True):
    g = re.search(r'^guid: ([0-9a-f]{32})', open(m, encoding='utf-8', errors='ignore').read(400), re.M)
    if g: guid2meta[g.group(1)] = m.replace(chr(92), '/')     # Windows glob은 역슬래시

sheets, rects = {}, {}
def sheet(guid):                                                 # 시트를 열고 (guid, internalID) -> PIL 좌표 rect를 모은다
    if guid not in sheets:
        meta = guid2meta[guid]; im = Image.open(meta[:-5]).convert('RGBA'); sheets[guid] = im
        for b in re.finditer(r'name: \S+\n\s+rect:\n\s+serializedVersion: 2\n\s+x: (\d+)\n\s+y: (\d+)\n\s+width: (\d+)\n\s+height: (\d+).*?internalID: (-?\d+)',
                             open(meta, encoding='utf-8').read(), re.S):
            x, y, w, h, iid = map(int, b.groups())
            rects[(guid, iid)] = (x, im.height - y - h, w, h)     # Unity rect의 y는 아래 원점 -> PIL 위 원점
    return sheets[guid]
def sprite(ref, sub=None):                                       # ref = (guid, fileID), sub = 조각 안의 (x, y, w, h)
    im = sheet(ref[0]); x, y, w, h = rects[ref]
    if sub: x, y, w, h = x + sub[0], y + sub[1], sub[2], sub[3]
    return im.crop((x, y, x + w, y + h))
def blit(cv, im, x, y, scale):                                   # 정수 배율 NEAREST
    cv.alpha_composite(im.resize((im.width * scale, im.height * scale), Image.NEAREST), (int(x), int(y)))
def hexints(h):                                                  # Unity YAML의 int[] (리틀엔디언 int32 hex 문자열)
    return list(struct.unpack('<%di' % (len(h) // 8), bytes.fromhex(h)))
```

## 4. 함정

| 증상 | 원인 → 해결 |
|---|---|
| csc가 참조를 못 찾고 경로가 `C:/Program`에서 끊김 | 공백 경로를 셸 루프로 조립 → rsp 파일에 한 줄씩 큰따옴표로 |
| CS0433 "형식이 두 어셈블리에 있음" 수백 개 | `Managed/UnityEngine.dll`(바깥의 모놀리식 사본)을 모듈과 함께 참조 → 모듈 폴더 안의 `Managed/UnityEngine/UnityEngine.dll`만 |
| CS0436 "형식이 가져온 형식과 충돌" 다수 | `Assembly-CSharp*.dll`을 참조에 넣음 → 뺀다 |
| 패키지 형식(`Unity.InputSystem` 등)을 못 찾음 | `Library/ScriptAssemblies`가 비어 있음(새 클론·Library 삭제 직후) → 에디터를 한 번 열어 임포트한 뒤 재시도 |
| `#if UNITY_EDITOR` 안의 오류가 안 잡힘 | define 없음 → `-define:UNITY_EDITOR` 추가 |
| `DotNetSdkRoslyn`·`NetCoreRuntime` 폴더가 없음 | Unity 2020.3 이하 → 이 방법은 쓸 수 없다. 에디터에서 컴파일한다 |
| 목업의 스프라이트가 엉뚱한 조각 | `rect.y`를 위 원점으로 씀 → `top = 시트 높이 − y − height` |
| int 배열 값이 0이거나 거대한 값 | hex를 빅엔디언이나 int64로 읽음 → `struct.unpack('<%di')`, 8자당 값 1개 |
| 참조가 `.meta`에 없음(KeyError) | 시트를 다시 슬라이스해 `internalID`가 바뀜, 또는 다른 시트의 guid → 에디터가 자산을 다시 저장했는지 `git status` 확인 |
| 하니스 결과가 에디터와 다름 | `UnityEngine.Random`·물리·프레임 순서에 기댄 코드 → 하니스 대상이 아니다(§2) |

Claude Code로 운용할 때: Bash 도구는 명령 문자열의 `\\`를 `\` 하나로 줄인다(heredoc 안에서도). Python 소스에 역슬래시가 필요하면 `chr(92)`로 조립하거나 Write 도구로 파일을 쓴다. 약 8K자를 넘는 명령은 통째로 실패하므로 긴 스크립트는 파일로 쓴 뒤 실행한다.
