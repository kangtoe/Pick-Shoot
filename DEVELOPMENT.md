# 개발 방침

프로젝트 공통 적용사항을 한곳에 모은다. 코드/구현 디테일이 아닌 **공통 원칙, Unity 규칙, 문서 구조, 커밋 가이드**를 다룬다.

---

## 1. 공통 원칙

1. **문서화 범위** — 코드 세부 사항은 문서화하지 않는다. 대원칙, 또는 개선의 여지가 있는 결정만 문서로 남긴다. 단, 기획 문서(GDD 등)는 세부사항을 적어도 된다.
2. **단일 출처(SSOT)** — 모든 방침·개요는 프로젝트 루트 문서가 단일 출처. 메모리·보조 노트는 본문을 복제하지 않고 해당 문서로 이어지는 포인터만 둔다.
3. **기획 우선** — 구현 전 기획. 중요한 의사결정은 문서화.
4. **명확한 실패(fail loud)** — 문제를 조용히 흡수하는 방어 코드(무음 fallback, 예외 삼키기, 의심스러운 입력 자동 보정) 대신 콘솔 에러로 원인을 명확히 진단한다. 시스템 경계(데이터 로드 등)의 실패는 어떤 데이터가 왜 잘못됐는지까지 드러낸다.

---

## 2. Unity 프로젝트 규칙

Unity 프로젝트라면 어디서나 같은 방침(에디터 작업 자동화, 에디터 없는 사전 검증, 데이터·네임스페이스·사운드 구조, 폴더·솔루션 이름, 픽셀아트 임포트, IMGUI 픽셀 UI의 함정)은 [Docs/Guides/UnityPrinciples.md](Docs/Guides/UnityPrinciples.md)에 두고 다른 Unity 프로젝트에 복사해 쓴다. 이 절에는 **이 프로젝트에서만 정해지는 값과 결정**만 둔다.

1. **네임스페이스** — 프로젝트 전체 단일 `PickShoot`.
2. **데이터 위치** — ScriptableObject 자산이 SSOT. `Assets/Data/Weapons`(`WeaponData`) · `Enemies`(`EnemyData`와 타입별 파생 `ChargerData` 등) · `Relics`(`RelicData`) · `UI`(`PixelUiSkin`) · `Arena`(`ArenaTheme`).
3. **효과음** — 클립은 그것을 쓰는 시스템의 직렬화 필드(`WeaponData`·프리팹·씬 컴포넌트)가 보관하고, 재생은 `SoundPlayer` 한 곳을 통해서만 한다.
4. **에디터 도구** — 장기 보존하는 플레이 모드 디버그 도구는 `Assets/Editor`의 `DebugToolsWindow` 한 창(메뉴 `PickShoot/Debug Tools`)에 모은다. 플레이 영상 자동 녹화 도구(`Assets/Editor/PlayRecorder.cs`·`PickShootPlayDriver.cs`, 메뉴 `PickShoot/Record Play`)는 예외로 보존한다 — 2026-09-29 아트 전면 교체 전후를 같은 봇·해상도·길이로 찍어 비교하기 위해서이며, 비교가 끝나면 삭제 여부를 정한다. 산출물(영상·목업 PNG)은 `Recordings/`(git 무시)에 날짜와 HEAD 짧은 해시를 파일명에 넣어 남긴다. 절차는 [AutoPlayRecording](Docs/Guides/AutoPlayRecording.md), 픽셀 UI 목업은 [PixelUiMockup](Docs/Guides/PixelUiMockup.md).
5. **아트·픽셀 UI** — 기준 캔버스(세로 270텍셀)와 배율, `PixelUiSkin`·`PixelGui` 조각, 아이템 타일과 배지, 글꼴, 월드 아트, 스프라이트 팩 외곽선 규칙은 모두 [Docs/Systems/PixelArtUI.md](Docs/Systems/PixelArtUI.md).
6. **프로젝트 폴더·솔루션 이름** — 모든 PC에서 프로젝트 폴더 이름은 저장소 이름과 같은 `Pick-Shoot`으로 두고, `.vscode/settings.json`의 `dotnet.defaultSolution`은 `Pick-Shoot.slnx`로 고정한다 (이유와 폴더 이름이 다른 PC의 복구 절차는 [UnityPrinciples §5](Docs/Guides/UnityPrinciples.md#5-프로젝트-폴더와-솔루션-이름)).

---

## 3. 문서 구조

- **README.md** — 간략한 프로젝트 개요
- **DEVELOPMENT.md** (본 문서) — 개발 과정 중 적용하는 원칙
- **[Docs/README.md](Docs/README.md)** — 문서 색인. 어떤 일을 할 때 어느 문서를 읽는지 한 줄씩. 문서를 새로 만들면 여기에 한 줄을 더한다.
- **Docs/** — 그 외 모든 문서. 카테고리별 하위 폴더로 분류한다 — 시스템·규칙 상세는 `Docs/Systems/`, 다시 실행할 수 있는 절차는 `Docs/Guides/`(프로젝트 비의존 문서는 다른 프로젝트에 그대로 복사해 쓴다).

각 문서는 제목 바로 아래에 **범위를 한 줄로 밝힌다** — 무엇을 다루고, 인접한 내용은 어느 문서에 있는지. 색인과 그 한 줄만 보고 필요한 문서 하나를 고를 수 있어야 한다.

---

## 4. 커밋 가이드

### 메시지 형식

```
<타입>: <제목> (한 줄 요약, 50자 이내)

<본문> (선택사항)
- 변경 사항 설명
- 왜 변경했는지 설명
```

### 타입

| 타입 | 설명 | 예시 |
|------|------|------|
| `feat` | 새로운 기능 추가 | `feat: 플레이어 자동 이동 시스템 구현` |
| `fix` | 버그 수정 | `fix: 경계 충돌 시 즉사 처리 버그 수정` |
| `refactor` | 코드 리팩토링 | `refactor: EnemySpawner 로직 개선` |
| `chore` | 빌드, 설정, Unity 에셋 변경 | `chore: FloatingText 속도 파라미터 조정` |
| `docs` | 문서 작업 | `docs: 게임 기획 문서 작성` |
| `perf` | 성능 개선 | `perf: Object Pooling 적용` |

### 작성 원칙

1. **제목은 50자 이내**, 명령형으로 작성
2. **한글 사용** (프로젝트 통일)
3. **무엇을, 왜** 변경했는지 명확히
4. 본문은 72자마다 줄바꿈 (선택사항)
5. **Co-Authored-By 사용 금지** (불필요한 메타정보 제거)
6. **한글 메시지 인코딩 문제 시** 임시 텍스트 파일에 메시지 작성 후 복사하여 사용
7. **논리적으로 분리 가능한 변경사항은 별도 커밋**으로 분리 시도
