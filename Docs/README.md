# 문서 색인

하려는 일에서 **문서 하나**를 찾는 표. 각 문서는 제목 바로 아래에 범위를 한 줄로 밝히므로 그 줄까지 보고 맞는지 확인한다. 문서를 새로 만들면 이 표에 한 줄을 더한다.

## 전체

| 하려는 일 | 문서 |
|---|---|
| 이 게임이 무엇인지, 무엇이 재미인지 | [README](../README.md) |
| 기획 전체 — 규칙·수치 기준·레퍼런스 | [GDD](GDD.md) |
| 지금 어느 단계인지, 다음에 무엇을 하는지, 지난 결정의 날짜와 이유 | [Roadmap](Roadmap.md) |
| 개발 방침과 커밋 메시지 규칙 | [DEVELOPMENT](../DEVELOPMENT.md) |

## 시스템·규칙 상세 — `Docs/Systems/`

| 하려는 일 | 문서 |
|---|---|
| 무기 — 종류·스탯·탄약·카드 ([GDD §7](GDD.md#7-무기-시스템)) | [Weapons](Systems/Weapons.md) |
| 적 — 타입·웨이브·보스 ([GDD §8](GDD.md#8-적-시스템)) | [Enemies](Systems/Enemies.md) |
| 유물 — 플레이어 스탯·효과 ([GDD §9](GDD.md#9-유물-시스템)) | [Relics](Systems/Relics.md) |
| 무기 개조 — 4종·획득·표시·회수와의 관계 ([GDD §10](GDD.md#10-무기-개조-시스템)) | [Mods](Systems/Mods.md) |
| 아트·픽셀 UI — 기준 캔버스·배율·타일·글꼴·월드 아트·외곽선 규칙 | [PixelArtUI](Systems/PixelArtUI.md) |

## 다시 실행할 절차 — `Docs/Guides/`

"비의존" 문서는 프로젝트 의존 내용이 없어 다른 Unity 프로젝트에 **그대로 복사해** 쓴다. 개선점은 이 저장소의 원본에 반영한다.

| 하려는 일 | 문서 | |
|---|---|---|
| Unity 프로젝트라면 어디서나 같은 원칙 (자동화·데이터·네임스페이스·솔루션 이름·임포트·IMGUI) | [UnityPrinciples](Guides/UnityPrinciples.md) | 비의존 |
| 에디터를 열지 않고 검증 — 컴파일 검사·로직 하니스·시트 디코드 | [OfflineVerification](Guides/OfflineVerification.md) | 비의존 |
| 플레이 영상 자동 녹화 | [AutoPlayRecording](Guides/AutoPlayRecording.md) | 비의존 |
| 픽셀 UI 배치·배율·문구안을 실제 시트로 합성해 비교 | [PixelUiMockup](Guides/PixelUiMockup.md) | 전용 |

## 새로 적을 자리

| 적을 내용 | 자리 |
|---|---|
| 개발 방침·원칙이 바뀌었다 | [DEVELOPMENT](../DEVELOPMENT.md) — Unity 공통이면 [UnityPrinciples](Guides/UnityPrinciples.md) |
| 기획 수치·규칙 | [GDD](GDD.md), 자세한 것은 `Systems/`의 해당 문서 |
| 무엇을 언제 왜 했는지, 고른 안과 버린 안 | [Roadmap](Roadmap.md) |
| 다시 실행할 수 있는 절차·함정 | `Guides/`의 해당 문서 |
