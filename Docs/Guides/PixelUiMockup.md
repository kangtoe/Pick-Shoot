# 픽셀 UI 목업 — PixelUiSkin 조각으로 에디터 없이 배치 비교

`PixelGui`로 그리는 픽셀 UI(HUD·일시정지·보상 화면·카드·타일·툴팁)의 배치·배율·문구를 바꿀 때, 에디터에 넣기 전에 **실제 시트 조각을 실제 규칙으로 합성한 그림**으로 안을 고르는 절차. Pick&Shoot 전용이다 — 공통 재료(시트 `.meta` 디코드, 컴파일 검사, 목업 원칙)는 `OfflineVerification.md`에 있고, 이 문서의 코드 조각은 그 문서 §3 조각 **뒤에 이어 붙여** 실행한다.

- 검증: 2026-10-07 아래 조각으로 두 글꼴 44글리프 전부 디코드, 9-slice 패널과 글자 합성 확인.
- 적용 예: 2026-10-02 일시정지 화면 세 안, 2026-10-06 아이템 타일 배지 네 안과 HUD 네 안(`Recordings/*_mockup_2026-10-06.png`), 2026-10-07 보상 화면 배치 여섯 안 — 카드·빌드 줄·리롤 버튼·제목을 전부 합성한 전체 화면(`Recordings/reward_layout_mockup_2026-10-07.png`). 결정은 `Docs/Systems/PixelArtUI.md`와 `Docs/Roadmap.md`에 적혀 있다.

## 1. 재현 규칙

픽셀 UI는 세로 1080px(270텍셀 × 4) 기준 캔버스에 정수 배율로 배치하고 캔버스 전체를 화면 높이에 비례해 축소·확대한다(`Docs/Systems/PixelArtUI.md` §1). 목업도 같은 좌표계를 쓴다.

- 캔버스 1920×1080. 배율은 패널 4·글자와 HUD 2·보상 화면 빌드 줄 타일과 카드 종류 제목 3. 여백·간격·타일 수 같은 배치 상수는 해당 Painter(`GameHud`·`BuildGridPainter`·`ItemTilePainter`·`UpgradeCardPainter`·`TooltipPainter`)에서 **읽어서** 그대로 쓴다.
- 720p는 1080p 캔버스를 **2/3로 NEAREST 축소**한 것이 실제에 가장 가깝다. 배율 1 글자·1텍셀 테두리가 빠지는 문제는 여기서만 보이므로, 확인할 부분을 크롭해 실제 크기와 2배 확대 둘 다 둔다.
- 바탕은 `ArenaTheme`의 바닥 타일(배율 4)을 깔고, 일시정지·보상 화면이면 검정 70%로 덮는다.
- 안 이름·설명도 같은 비트맵 글꼴(일반 글꼴 배율 2)로 적는다.

## 2. PixelUiSkin 구조

스킨 `Assets/Data/UI/PixelUiSkin.asset` 한 파일이 시트 조각 참조와 글꼴 지표를 전부 보관한다. 최상위 필드는 2칸 들여쓰기.

- 글꼴 `fontPlain`·`fontOutlined`: `characters[i]` ↔ `glyphs[i]`(스프라이트 참조). `glyphLeft`·`glyphWidth`는 타일 안에서 실제 획이 있는 가로 구간(텍셀)이며 int 배열 hex로 저장. `lineHeight 16`·`letterSpacing 1`·`spaceWidth 5`, 대문자·숫자 상자의 세로 구간 `capTop 2`·`capHeight 12`(배지 바탕 높이가 쓴다).
- 글자는 `PixelGui.DrawText`와 같이: 글리프 타일에서 `(glyphLeft, 0, glyphWidth, lineHeight)`만 잘라 배율로 찍고 펜을 `glyphWidth × 배율`만큼 옮긴다. 글자 사이 `letterSpacing × 배율`, 공백은 `spaceWidth × 배율`.
- 9-slice `panelBlue`·`panelYellow`·`panelRed`·`panelGrey`·`selectionFrame`: `tiles[0..8]` = 좌상·상·우상·좌·중·우·좌하·하·우하. 모서리는 한 장, 변·중앙은 16텍셀 타일을 **반복**한다(늘리지 않는다).
- 단색 판(HUD 무기 판·아이템 타일)은 시트 조각이 아니라 색 필드 `weaponPlateOutline`(외곽선 2텍셀)·`weaponPlateFill`로 그린다.
- 다른 자산: 무기 그림은 `Assets/Data/Weapons/*.asset`의 `weaponSprite`, 바닥은 `Assets/Data/Arena/*.asset`의 `floor`, 유물 임시 아이콘은 `Assets/Art/UI/relic_keys.png` 16×16 격자. 전부 같은 `{fileID, guid}` 규칙으로 푼다.

## 3. 코드 조각

`OfflineVerification.md` §3의 공통 조각(`sprite`·`blit`·`hexints`) 뒤에 붙인다:

```python
# PixelUiSkin.asset: 최상위 필드(2칸 들여쓰기)별로 스프라이트 참조와 글꼴 지표를 모은다.
skin, sec = {}, None
for l in open('Assets/Data/UI/PixelUiSkin.asset', encoding='utf-8'):
    m = re.match(r'^  (\w+):', l)
    if m: sec = m.group(1); skin[sec] = {'refs': []}
    if sec is None: continue
    m = re.search(r'fileID: (-?\d+), guid: ([0-9a-f]{32})', l)
    if m: skin[sec]['refs'].append((m.group(2), int(m.group(1))))
    m = re.match(r"^\s+characters: '(.*)'", l)
    if m: skin[sec]['characters'] = m.group(1)
    m = re.match(r'^\s+(glyphLeft|glyphWidth): ([0-9a-f]+)', l)
    if m: skin[sec][m.group(1)] = hexints(m.group(2))
    m = re.match(r'^\s+(lineHeight|letterSpacing|spaceWidth|capTop|capHeight): (\d+)', l)
    if m: skin[sec][m.group(1)] = int(m.group(2))

def measure(f, s, scale):                                        # PixelGui.MeasureText 재현
    w = (len(s) - 1) * f['letterSpacing'] if s else 0
    w += sum(f['spaceWidth'] if c == ' ' else f['glyphWidth'][f['characters'].index(c)] for c in s)
    return w * scale
def text(cv, f, s, x, y, scale):                                 # PixelGui.DrawText 재현
    pen = x
    for i, c in enumerate(s):
        if i: pen += f['letterSpacing'] * scale
        if c == ' ': pen += f['spaceWidth'] * scale; continue
        g = f['characters'].index(c)
        blit(cv, sprite(f['refs'][g], (f['glyphLeft'][g], 0, f['glyphWidth'][g], f['lineHeight'])), pen, y, scale)
        pen += f['glyphWidth'][g] * scale
def nine(cv, name, x, y, w, h, scale, t=16):                     # 9-slice: 모서리 4장 + 변·중앙은 타일 반복
    tl = skin[name]['refs']; T = t * scale
    def fill(ref, x0, y0, x1, y1):
        yy = y0
        while yy < y1:
            hh = min(T, y1 - yy); xx = x0
            while xx < x1:
                ww = min(T, x1 - xx); blit(cv, sprite(ref, (0, 0, ww // scale, hh // scale)), xx, yy, scale); xx += T
            yy += T
    blit(cv, sprite(tl[0]), x, y, scale); fill(tl[1], x + T, y, x + w - T, y + T); blit(cv, sprite(tl[2]), x + w - T, y, scale)
    fill(tl[3], x, y + T, x + T, y + h - T); fill(tl[4], x + T, y + T, x + w - T, y + h - T); fill(tl[5], x + w - T, y + T, x + w, y + h - T)
    blit(cv, sprite(tl[6]), x, y + h - T, scale); fill(tl[7], x + T, y + h - T, x + w - T, y + h); blit(cv, sprite(tl[8]), x + w - T, y + h - T, scale)
```

쓰는 예 — 일시정지 배너 하나를 1080p·720p로:

```python
cv = Image.new('RGBA', (1920, 1080), (214, 186, 124, 255))
FO, FP = skin['fontOutlined'], skin['fontPlain']
nine(cv, 'panelBlue', 960 - 224, 400, 448, 128, 4)                    # 7타일 × 2타일, 패널 배율 4
text(cv, FO, 'PAUSED', 960 - measure(FO, 'PAUSED', 4) // 2, 432, 4)
text(cv, FP, 'PRESS ESC TO RESUME', 960 - measure(FP, 'PRESS ESC TO RESUME', 2) // 2, 560, 2)
cv.convert('RGB').save('Recordings/pause_mockup_2026-10-07.png')
cv.resize((1280, 720), Image.NEAREST).convert('RGB').save('Recordings/pause_mockup_2026-10-07_720.png')
```

- 산출물은 `Recordings/<주제>_mockup_<YYYY-MM-DD>[_vN].png`(git 무시). 고른 안과 버린 안의 이유, 파일명은 `Docs/Systems/PixelArtUI.md`(규칙이 바뀐 경우)나 `Docs/Roadmap.md`(진행 기록)에 적는다.
- 언제 목업하나 (2026-10-08): **여러 안을 고를 때, 배치·크기 관계가 새로 생길 때, 상태(호버·비활성)가 있는 새 요소를 넣을 때**. 수치 하나를 바꾸는 일(배율·간격·글꼴 교체)은 컴파일 검사만 하고 에디터에서 본다. 비용은 세션 첫 목업(재현 조각을 다시 짜는 것)이 크고 그 뒤 안 하나는 싸다.
- 스크립트는 저장소에 넣지 않는다. 다음에는 두 문서의 조각에서 다시 시작한다.

## 4. 문구 폭 사전 검증

고정 폭 영역(업그레이드 카드 `UpgradeCardPainter`, 툴팁의 `PixelGui.WrapText`)은 한 단어가 폭을 넘으면 **런타임 예외**다(fail loud). 문구를 바꾸면 §3의 `measure()`로 먼저 잰다.

- 공식(`PixelGui.MeasureText`): `(Σ advance + (글자 수 − 1) × letterSpacing) × 배율`, advance는 공백이면 `spaceWidth`, 아니면 `glyphWidth`. 코드가 바뀌면 `PixelGui.cs`를 다시 읽고 Python 쪽을 맞춘다.
- 폭 상수는 그때그때 Painter에서 읽는다. 예(2026-10-03): 카드 안쪽 폭 = 7타일 × 64 − 2 × 10 × 4 = 368px(1080p), 본문 글자 배율 2, 무기 이름은 아웃라인 글꼴 배율 3. 유물 카드 라벨을 5글자로 줄이고 총계 부호를 뺀 근거.
- 참고 측정(2026-10-07, 아웃라인 글꼴): `PAUSED` ×4 = 260px, `LV5` ×2 = 64px, `WEAPONS (7/12)` ×2 = 292px, `WAVE 4 CLEAR` ×4 = 492px. 리롤 버튼 `REROLL (2)`(일반 글꼴 ×2 + 여백, 32 배수 올림) = 256×64px.

## 5. 함정

| 증상 | 원인 → 해결 |
|---|---|
| 글자 간격이 코드보다 넓음 | `glyphLeft`·`glyphWidth` 크롭 생략(16텍셀 타일째 찍음) → §2 글자 규칙대로 |
| 스킨 참조가 `.meta`에 없음(KeyError) | 시트를 다시 슬라이스해 `internalID`가 바뀜 → 에디터가 스킨을 다시 저장했는지 `git status` 확인 |
| 시트에 없는 글자로 `ValueError` | 글꼴은 영문 대문자·숫자·`%+-!()/.`뿐 → 문구를 바꾸거나 `font_extra.png`에 글리프를 추가(`Docs/Systems/PixelArtUI.md` §5) |
| 목업은 깨끗한데 에디터 720p에서 타일 사이 1px 틈 | 목업은 IMGUI의 쿼드별 반올림을 재현하지 않는다 → 틈은 코드에서 타일 변을 화면 픽셀에 맞춰 해결(`UnityPrinciples.md` §7). 목업으로는 못 본다 |
| 1080p에서는 좋은데 720p에서 획이 빠짐 | 배율 1 글자·1텍셀 선 → 720p 크롭을 반드시 같이 보고, 배율 2 이상이나 단색 사각형으로 바꾼다 |
| 호버 순간 화면의 다른 UI가 통째로 사라짐 | 9-slice 영역이 모서리 타일 두 장(2 × 16텍셀 × 배율)보다 작아 `DrawNineSlice` 예외로 `OnGUI`가 끊김 → 영역을 두 장 크기로 넓혀 가운데 맞추거나 배율을 낮춘다. 목업은 이 예외를 안 낸다 (2026-10-08 리롤 버튼 112px에 배율 4 테두리) |
| 목업은 깨끗한데 에디터의 비활성·호버 상태에서 모양이 다름 | 상태를 목업에 안 그림 — 덮는 막·테두리가 판의 깎인 귀를 따르는지는 상태별로 그려야 보인다 (2026-10-08 리롤 버튼 0회의 직사각형 막) → 비활성·호버도 한 칸씩 둔다 |
