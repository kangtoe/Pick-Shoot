using System.Collections.Generic;
using UnityEngine;

namespace PickShoot
{
    // 업그레이드 카드 한 장의 IMGUI 그리기 (Kenney Interface 시트 기반 픽셀 UI).
    // 레이아웃은 텍셀 단위로 정의하고 scale(패널·아이콘)과 textScale(글자)을 곱해 픽셀로 바꾼다.
    // 카드 크기는 타일 단위로 고정이며 내용에 맞춰 늘리지 않는다 — 내용이 카드를 넘치면 예외로 알린다.
    // 카드는 위에서부터 한 줄씩 가운데 맞춤으로 쌓는다 (2026-10-09): 종류 제목(일반 글꼴 배율 3 — 획득 `NEW`·레벨업 `UPGRADE`, 유물도 `NEW`) → 이름(아웃라인 글꼴
    // 배율 2, 한 줄) → 전시 타일 → 첫 줄(무기는 레벨, 유물은 등급) → 스탯 줄 → 설명 줄. 이름을 배율 3으로 키운 안은 제목보다 커져 종류가 묻히고 타일에 붙었다
    // (Recordings/card_stacked_mockup_2026-10-09.png). 머리가 120 → 216px로 높아져 카드도 7타일에서 8타일로 키웠다.
    // 그전(2026-10-08 ~ 10-09)엔 브로타토식 가로 배치 — 왼쪽에 전시 타일, 그 오른쪽 글자 칸에 제목과 이름(200px에서 두 줄로 접기)을 두고 묶음을 가운데에 놓았다.
    // 스탯·설명 줄이 모두 가운데 맞춤이 되며 머리만 좌우로 갈린 꼴이 되어 세로로 돌렸다. 그 전에는 종류를 카드 위쪽 끝에 걸치는 배지(노란 NEW! / 카드와 같은 파란 LV UP)로
    // 적고 타일(배율 6) 밑에 이름(배율 3)을 세로로 쌓았다 — 그 배지 그리기는 일시정지 화면의 PAUSED 탭에만 남았다가 2026-10-12 그 화면도 글자 제목으로 바꾸며 없어졌다.
    // 종류 색은 제목 글자색으로 — `NEW`(새 무기·유물)는 XP 팝업과 같은 노랑(PixelUiSkin.HighlightText), `UPGRADE`는 초록(PixelUiSkin.UpgradeText).
    // 색 패널·외곽선 색 띠로 두는 안도 목업했으나 패널은 카드 안에서 버튼처럼 보였고 띠는 머리가 번잡했다 (Recordings/card_header_mockup_2026-10-08*.png).
    // 한 장이 레벨을 둘 이상 올리는 카드(아직 없다)는 제목 뒤에 글자 배율 꼬리 `(+n)`이 붙고, 제목과 꼬리를 한 덩이로 가운데에 둔다 (LevelTag, UpgradeOption.Levels).
    // 전시 칸은 일시정지·보상 화면의 빌드 격자와 같은 정사각 타일(ItemTilePainter)이고 배지는 고른 뒤의 상태(`LV1`·`LV3`·`X2`) — 레벨 줄의 "2 → 3"과 맞춘다.
    // 스탯 줄은 한 줄 문구 `DMG 240 → 270`(라벨·현재 값·화살표·다음 값, 값 하나면 `BOUNCE 3`)을 가운데 맞춤으로 적는다 (2026-10-09). 그전엔 라벨은 왼쪽 여백,
    // 값은 화살표를 한 열에 맞춘 오른쪽 열의 표였다 — 세 카드의 값이 같은 열에 서 가로로 비교하기 쉬웠지만 라벨과 값 사이 빈칸이 넓었고, 가운데 맞춘 머리 묶음·설명
    // 줄과 정렬이 갈렸다. 한 줄 문구를 왼쪽에 두는 안은 카드 오른쪽이 비었고, 줄만 왼쪽·설명은 가운데인 안은 한 카드에 정렬이 섞였다
    // (Recordings/card_inline_rows_mockup_2026-10-09.png).
    // 스탯 줄은 DMG·AMMO 뒤로 발사 속도(`FIRE RATE 30` — 분당 사격 수의 1/10, 2026-10-12)와 그 무기의 수치 특성(`BURST 3`·`BOUNCE 5`·`PIERCE 1`·`BLAST 2.5`,
    // ItemInfoText.WeaponTraitStats)이 같은 꼴로 이어지고,
    // 수치가 없는 특성(`AUTO RETRIEVE`·`PULLS IN`)은 표 아래 본문 줄 — 유물 카드의 효과 줄과 같이 가운데 맞춤 일반 글꼴로, 카드의 설명 줄이 모두 같은 꼴이다
    // (2026-10-07 표 아래 가운데 맞춤 아웃라인 → 같은 날 값 열 오른쪽 정렬 → 2026-10-08 라벨 열 왼쪽 정렬 → 2026-10-09 다시 가운데. 처음 가운데 둔 때는 글꼴이
    // 아웃라인이라 표와 결이 달라 튀었다 — 지금은 표와 같은 일반 글꼴이고 표와 본문 간격으로 떨어진다, Recordings/card_center_desc_mockup_2026-10-09.png).
    // 무기를 가르는 것이 카드에서 바로 읽히게 — 그전에는 호버 툴팁에만 있었다. 가장 긴 조합(수치 특성 셋 + 개조 줄 — 어썰트 라이플·스나이퍼 라이플·런처에 수치 개조가 붙은
    // 레벨업·개조 카드)에서 아래 여백이 48px (7×9타일, 2026-10-12 FIRE RATE 줄과 개조 줄. 7×8일 때 그 줄들 없이 32px, 7×7 가로 머리일 때 64px). 넘치면 예외.
    // 개조는 어느 카드든 레벨 줄 바로 아래 개조 이름 한 줄(개조 색, PixelUiSkin.ModText — 배지 머리글자와 같은 분홍)로 적는다 (2026-10-12): 개조된 자루의
    // 레벨업 카드와 보기 카드는 `HOMING`, 개조 카드는 붙는다는 뜻의 `+HOMING`. 개조가 더한 수치는 특성 줄에 합친 값(`PIERCE 3`·`BLAST 0.8`)으로 보인다 (Mods §4.2).
    // 그전엔 카드마다 달랐다 — 레벨업 카드는 수치로 못 적는 유도만 본문에 흰 `HOMING`(관통·도탄·폭발은 이름 없이 배지 머리글자뿐), 개조 카드는 레벨 줄 없이
    // 그 자리에 `+HOMING`이었다 (Recordings/mod_line_mockup_2026-10-12_*.png).
    // 개조 카드(Mods §4.1)도 같은 틀이다 — 제목 `MOD`(분홍), 이름·전시 타일은 대상 자루의 것(배지는 붙인 뒤의 `LV5H`), 첫 줄은 그 자루의 레벨(`LEVEL 5`, 흰 값
    // 하나 — 바뀌지 않는다)과 그 아래 `+개조 이름`. 스탯 줄은 레벨업 카드처럼 DMG·AMMO와 특성을 모두 적고, 개조가 바꾸는 줄만 화살표 — 오르는 줄
    // `PIERCE 1 → 3`, 없던 속성이 붙는 줄 `BOUNCE - → 3`·`BLAST - → 0.8`(튕김 0인 무기의 도탄, 폭발 탄환이 아닌 무기의 폭발. 0 → 3은 오르는 것이 아니라
    // 속성이 붙는 것이라 없음 `-`에서). 줄 순서는 다른 카드와 같은 특성 순서다 (2026-10-11). 레벨업·개조 카드는 같은 대상의 전후 비교라
    // "이 카드가 바꾸는 줄에만 화살표"가 같은 규칙이다.
    // 처음엔 바뀌는 줄만 적어 카드 아래가 비었다(Recordings/mod_card_rows_mockup_2026-10-10.png). 새 속성을 화살표 없는 초록 값으로 맨 끝에 두는 안은 획득 카드의
    // 값과 같은 꼴이라 원래 있던 값으로 읽힐 여지가 있었다 (card_new_trait_mockup_2026-10-10.png·card_new_trait_dash_mockup_2026-10-11.png).
    // 고르면 얻는 값은 `UPGRADE`와 같은 초록(PixelUiSkin.UpgradeText)으로 칠한다 (2026-10-11) — 레벨업·개조 카드의 화살표 뒤 다음 값, 획득 카드의 모든 값,
    // 유물 카드의 수정치 값. 라벨·현재 값·화살표·바뀌지 않는 값은 흰색이다. 개조 이름은 얻는 것이든 이미 가진 것이든 개조 색이다.
    // 획득 카드의 줄 전체(라벨까지)를 칠하는 안은 무거웠고, `NEW`의 노랑으로 칠하는 안은 얻는 것의 색이 둘로 갈렸다. 획득 카드에도 `-` → 값을 쓰는 안은 모든 줄에
    // 화살표가 붙어 아무것도 가리키지 않았다 — 2026-10-07에 `-` 열을 뺀 이유와 같다. 개조 이름은 노랑 → 초록 `+` → 초록 → 분홍 `+`를 거쳤다
    // (card_green_values·card_gain_green_final·card_kind_color_mockup_2026-10-10.png, card_new_weapon_dash_mockup_2026-10-11.png).
    // 일시정지·보상 화면 빌드 격자의 보유 자루·유물에 마우스를 올리면 같은 틀의 **보기 카드**가 뜬다 (DrawOwnedWeapon·DrawOwnedRelic, 2026-10-12) — 회색 패널,
    // 종류 제목 줄 없이 이름부터, 폭은 카드와 같고 높이는 내용에 맞춘다(한 번에 하나라 세 장을 맞출 일이 없다). 값은 모두 흰색이고 화살표가 없다 — 얻는 것이
    // 없다. 무기는 `LEVEL 3` 아래에 개조 이름 한 줄(개조 색), 유물은 1개분 수정치(보유 수는 배지 `X n`). 파란 패널 안은 선택 카드 위에 겹치면 네 번째
    // 선택지처럼 보였다 (Recordings/owned_card_*_mockup_2026-10-12*.png). 그전(2026-10-06 ~ 10-12)엔 사실 항목을 폭에 맞춰 채운 툴팁(TooltipPainter)이었고,
    // 카드 전시 칸에도 같은 툴팁이 떴다 — 카드가 특성까지 다 담게 되자 툴팁이 더 보이는 것은 탄속·넉백뿐이라 전시 칸 호버와 함께 걷었다.
    // 카드 호버는 테두리를 바깥으로 HoverOutlineTexels 넓히기 — 카드보다 그만큼 큰 흰 판 모양(PixelGui.FillPlateShape)을 패널 밑에 깔아 패널의 흰 바깥 선
    // 1텍셀이 2텍셀이 된다. 패널 모서리도 판처럼 1텍셀 깎여 있어 그 귀가 흰색으로 메워지고 바깥 귀만 깎인다. 리롤 버튼도 같은 값 (2026-10-08).
    // 그전엔 4텍셀 들림(띠→카드 간격 40px일 때 배지가 부제를 가림)·선택 테두리·비정수 배율 키우기(텍셀이 갈려 깨져 보이고 아래 빌드 줄 제목을 덮음)를 거쳤다.
    // 유물 카드(Relics §2)도 같은 패널 구성이다 — 제목은 `NEW`, 전시 칸에 유물 아이콘(배지 `X n`이 고른 뒤 보유 수), 무기 카드의 레벨 줄 자리에
    // 등급 이름 한 줄(`COMMON` — 라벨 없이 값만, 등급 색)뿐이고, 본문 줄에 그 유물이 더하는 수정치를 `DAMAGE +10%`처럼 한 줄로 적는다
    // (2026-10-07, 등급 줄은 2026-10-09 — 처음엔 `GRADE COMMON`이었다가 같은 날 라벨을 뺐다). 수정치 줄 맨 앞에는 그 스탯 아이콘(PixelUiSkin.StatIcon,
    // 스탯 표와 같은 것)이 붙는다 — 세로 카드는 줄 덩이 안, 가로 카드는 표 왼쪽 바깥 (2026-10-17, BodyIcons). 캐릭터 카드의 스탯 보너스 줄도 같다.
    // 등급은 2026-10-08까지 제목 자리였다. 그 사이 이름 밑 부제 줄(`COMMON`·무기는 `LEVEL +1`)로 옮기고 레벨 줄을 빼는 안, 부제의 외곽선·이름 음영을 끄는 안을
    // 목업했으나 레벨 줄로 돌아왔다 (Recordings/card_subtitle_mockup_2026-10-08*.png·card_text_*_mockup_2026-10-09*.png·card_grade_row_mockup_2026-10-09.png).
    // 캐릭터 카드(DrawCharacter, Characters §4, 2026-10-13)는 시작 화면의 캐릭터 카드 — 유물 카드와 같은 틀에서 종류 제목 없이 이름부터. 전시 칸은 32×32 그림이 드는
    // 초상 타일(ItemTilePainter.DrawPortrait, 배지 없음), 첫 줄은 `ABILITY DASH`(능력 이름은 얻는 것이라 초록), 그 아래 자산의 능력 설명 한 줄(흰색 — 개조 이름 줄과
    // 같은 자리라 SubLine이 색을 따로 든다), 본문에 스탯 보너스를 유물 카드의 수정치 줄 꼴로 한 줄씩(`MOVE SPEED +20%`, 값 초록). 시작 무기 후보는 같은 화면의 무기
    // 줄이 보이므로 카드에 적지 않는다 (같은 날 낮엔 맨 아래 작은 타일 줄이었다 — 캐릭터 화면과 무기 화면이 따로였을 때). 시작 화면의 카드는 8타일 높이
    // (UpgradeManager.StartCardHeightTiles)이고 가장 긴 조합(COLLECTOR·HUNTER — 보너스 두 줄)의 내용이 384px라 아래 여백 88px.
    // 러닝 중에는 빌드 격자 유물 줄 첫 칸의 캐릭터에 보기 카드(DrawOwnedCharacter, 2026-10-14)가 뜬다 — 유물 보기 카드와 같은 틀에 등급 줄 `CHARACTER`.
    public static class UpgradeCardPainter
    {
        const int CardWidthTiles = 7;
        // 10 → 9 (2026-10-08, 표가 짧은 카드의 아래가 비어 보임) → 7 (같은 날 머리 가로 배치 — 타일 밑에 두던 이름이 타일 오른쪽으로 가고 타일도 배율 6→4가 되어
        // 머리가 240px에서 120px로, 위 여백도 배지 깊이를 빼며 80에서 40px로) → 8 (2026-10-09 세로 쌓기 — 머리 216px) → 9 (2026-10-12 모든 무기 카드에
        // FIRE RATE 줄 — 8에서는 가장 긴 카드가 넘쳤다). 가장 긴 카드(수치 개조가 붙은 어썰트 라이플의 레벨업·개조 카드 등)의 아래 여백 48px.
        const int CardHeightTiles = 9;
        const int SidePaddingTexels = 10; // 좌우 여백 (패널 테두리 → 글자). 열 표 시절 산탄총 고레벨의 "256X3" 값까지 7타일 안에 들어가던 최대치.
        const int TopMarginTexels = 10;   // 위 여백 — 좌우와 같다. 배지가 카드 위 끝에 걸치던 때는 배지 깊이 16 + 4였다.
        const int BottomMinTexels = 8;    // 넘침 검사용 최소 아래 여백. 실제 아래 여백은 남는 만큼.
        const int RowGapTexels = 6;       // 머리(전시 타일)→첫 줄, 첫 줄→스탯 줄 (2026-10-08 카드를 9타일로 낮추며 8에서)
        const int BodyGapTexels = 4;      // 스탯 줄→본문 줄(특성·설명)
        // 머리 안 세로 간격 (패널 텍셀): 제목 줄 → 이름 줄은 한 쌍으로 붙이고, 이름 줄 → 전시 타일은 조금 띄운다.
        const int TitleNameGapTexels = 2;
        const int NameShowcaseGapTexels = 4;
        // 전시 타일의 배율. 28텍셀 타일 × 4 = 112px — 일시정지 격자의 타일과 같다. 6(168px)이면 머리가 56px 더 높아져 8타일 카드에 들지 않는다.
        const int ShowcaseTileScale = 4;
        // 호버 때 테두리를 바깥으로 넓히는 두께 (패널 텍셀). 리롤 버튼(UpgradeManager)도 같은 값.
        public const int HoverOutlineTexels = 1;
        // 보기 카드 ↔ 가리키는 타일 (패널 텍셀). 툴팁 때와 같은 값.
        const int AnchorGapTexels = 2;
        // 보상 화면의 가로 카드 (DrawLandscape, 2026-10-14) — 14×4타일(896×256px). 왼쪽부터 전시 타일(세로 가운데) · 가운데 열(종류 제목·이름·첫 줄·개조 줄,
        // 위 여백부터) · 오른쪽 표(스탯 줄·본문 줄·설명, 세로 가운데). 표 폭 85텍셀(340px)은 가장 긴 줄(`FIRE RATE`·`34 -> 40`)에 여유를 둔 값.
        const int LandscapeWidthTiles = 14;
        const int LandscapeHeightTiles = 4;
        const int LandscapeTableWidthTexels = 85;
        const int LandscapeShowcaseGapTexels = 8;   // 전시 타일 → 가운데 열 (32px)
        const int LandscapeColumnGapTexels = 4;     // 가운데 열 → 표 (16px, 넘침 검사용)
        // 종류 제목의 일반 글꼴 배율 — 이름(아웃라인 배율 2)보다 한 단계 커 종류가 먼저 읽힌다 (2026-10-08, 글자 배율 2에서 키움). 보너스 꼬리 `(+n)`은 글자 배율 그대로
        // 제목 뒤에 글리프 상자 아래 변을 맞춰 붙인다.
        const int TitleScale = 3;
        // 이름의 아웃라인 글꼴 배율. 제목·스탯 줄과 글꼴로 가른다 — 배율 3이면 제목보다 커져 종류가 묻혔다 (2026-10-09 목업).
        // 이름은 한 줄 — 카드 안쪽 폭(368px)에 가장 긴 `ARMOR PLATE`·`MAGNET CORE`(194px)도 넉넉히 들고, 넘치면 예외. 가로 머리 때는 200px 칸에서 두 줄로 접었다.
        const int NameScale = 2;
        // 종류 제목 — 획득 `NEW`(노랑), 레벨업 `UPGRADE`(초록, PixelUiSkin.UpgradeText). 느낌표는 뺐다 (배지 때는 `NEW!`) — 글자 제목에서는 색이 종류를 가르고,
        // `(+n)` 앞에서 어색하다. 유물도 `NEW` — 이미 가진 유물도 하나를 새로 얻는 카드이고, 겹쳐 쌓이는 것은 배지가 말한다.
        // 레벨업은 2026-10-09까지 흰 `LV UP`이었다.
        const string NewTitle = "NEW";
        const string LevelUpTitle = "UPGRADE";
        const string ModTitle = "MOD";
        // 첫 줄(무기 카드)의 라벨 — 값은 "n → n+1". 슬롯 번호(`SLOT 3`)는 2026-10-07에 뺐다 — 자루를 가리키는 번호였지만 전시 그림·레벨로 충분하고,
        // 레벨 값의 `LV` 접두도 함께 걷어 값 열이 좁아졌다. 유물 카드는 이 자리가 라벨 없는 등급 이름이다 (2026-10-09. 그전엔 줄이 없었고,
        // 2026-10-07엔 보유 수 `OWNED n → n+1` 줄을 뺐다 — 고른 뒤 보유 수는 전시 배지 `X n`이 말한다).
        const string LevelLabel = "LEVEL";
        // 캐릭터 카드 첫 줄의 라벨 — 값은 능력 이름 (`ABILITY DASH`).
        const string AbilityLabel = "ABILITY";
        // 캐릭터 보기 카드의 등급 줄 — 유물 보기 카드의 `COMMON` 자리 (2026-10-14). 캐릭터는 유물 등급이 아니라 RelicGrade에 두지 않는다.
        const string CharacterGrade = "CHARACTER";
        // 줄이 없는 칸용 빈 묶음 (프레임마다 새로 만들지 않는다).
        static readonly string[] NoLines = new string[0];
        static readonly Color[] NoTints = new Color[0];
        // 개조 카드에서 개조 전에 없던 특성의 "현재" 값 — 0이 아니라 없음 (`BOUNCE - → 3`, 2026-10-11).
        const string NoneValue = "-";
        static readonly List<string> EmptyLines = new List<string>();

        // 글꼴에 있는 문자(A-Z 0-9 % + - ! ( ) / . 공백)만 써야 한다. 없는 문자는 PixelGui가 예외로 알린다.
        struct CardContent
        {
            // 머리 제목(종류)과 그 글자색 — `NEW`는 노랑, `UPGRADE`는 초록. TitleTag는 제목 뒤에 작게 붙는 보너스 꼬리(`(+2)`), 없으면 null.
            // 보기 카드는 Title이 null — 제목 줄과 그 아래 간격 없이 이름부터.
            public string Title;
            public string TitleTag;
            public Color TitleTint;
            public Sprite ShowcaseSprite;
            // 전시 칸이 아이템 타일(28텍셀, 배지 가능)이 아니라 초상 타일(36텍셀, 배지 없음)인가 — 캐릭터 카드.
            public bool ShowcasePortrait;
            public string ShowcaseBadge;
            // 배지 뒤에 개조 색으로 붙는 개조 머리글자 (`LV5H`의 `H`). 개조가 없으면 null.
            public string ShowcaseModMark;
            public string Name;
            // 전시 타일 밑 첫 줄 — 모든 카드에 있다. 무기는 레벨(`LEVEL 2 -> 3`, 개조 카드는 `LEVEL 5`), 유물은 등급(`COMMON` — RowLabel이 null이면 라벨 없이 값만).
            // RowNow는 스탯 줄과 같은 규칙 — null이면 비교 없이 값 하나 (획득·개조·유물·보기 카드). RowTint는 다음 값(RowNext)의 글자색 — 얻는 값은 초록, 유물은 등급 색
            // (Relics §5), 바뀌지 않는 값은 흰색.
            public string RowLabel;
            public string RowNow;
            public string RowNext;
            public Color RowTint;
            // 첫 줄 바로 아래 한 줄 — 개조된 자루(레벨업·보기 카드)는 개조 이름 `HOMING`, 개조 카드는 `+HOMING`(둘 다 개조 색), 캐릭터 카드는 능력 설명(흰색). 없으면 null.
            public string SubLine;
            public Color SubLineTint;
            public string[] StatLabels;
            // StatNow[i]가 null이면 그 줄은 값 하나 — "현재 → 다음"이 뜻 없는 줄(레벨로 바뀌지 않는 특성)이라 화살표 없이 고른 뒤의 값만 적는다.
            public string[] StatNow;
            public string[] StatNext;
            // StatNext[i]의 글자색 — 얻는 값은 초록, 그 밖은 흰색. 라벨·현재 값·화살표는 늘 흰색.
            public Color[] StatTints;
            // 표 아래 본문. BodyLines는 한 항목이 한 줄(가운데 맞춤 일반 글꼴 — 무기의 수치 없는 특성·유물의 수정치), Note는 단어 단위로 접는 문단.
            // BodyValues[i]는 그 줄 뒤에 공백 하나를 두고 붙는 값(유물 수정치 `+10%`) — null이면 줄이 흰 문구 하나다. BodyValuesGain이면 얻는 값이라 초록
            // (선택 카드), 아니면 흰색(보기 카드 — 이미 가진 것).
            public string[] BodyLines;
            public string[] BodyValues;
            // BodyLines[i] 앞의 아이콘(글자 배율 타일) — 유물·캐릭터의 수정치 줄은 그 스탯 아이콘 (2026-10-17). 배열이 null이면 아이콘 없는 본문(무기의 특성).
            // 세로 카드는 아이콘·문구·값을 한 덩이로 가운데에, 가로 카드는 표 왼쪽 바깥에 매단다 (DrawLandscape).
            public Sprite[] BodyIcons;
            public bool BodyValuesGain;
            public string Note;
        }

        // 보상 화면의 선택 카드 크기 (7×9타일).
        public static Vector2Int CardSize(PixelUiSkin skin, int scale)
        {
            return CardSize(skin, scale, CardHeightTiles);
        }

        // 높이를 달리하는 화면(시작 화면, 8타일)용. 폭은 늘 7타일 — 문구 폭 검사의 기준.
        public static Vector2Int CardSize(PixelUiSkin skin, int scale, int heightTiles)
        {
            if (heightTiles < 2)
                throw new System.Exception($"[PickShoot] UpgradeCardPainter.CardSize: 높이 {heightTiles}타일은 9-slice 모서리 두 장보다 작음.");
            int tile = skin.TileSize * scale;
            return new Vector2Int(CardWidthTiles * tile, heightTiles * tile);
        }

        // 선택 카드 한 장 (보상 화면·시작 화면 무기 단계). 크기는 고정(CardSize)이라 내용이 넘치면 예외.
        public static void Draw(PixelUiSkin skin, UpgradeOption option, PlayerWeapons weapons, PlayerStats stats, Rect rect, int scale, int textScale, bool hovered)
        {
            DrawSelectable(skin, Build(skin, option, weapons, stats), rect, scale, textScale, hovered);
        }

        // 캐릭터 카드 (시작 화면, Characters §4). 헤더 참조.
        public static void DrawCharacter(PixelUiSkin skin, CharacterData character, Rect rect, int scale, int textScale, bool hovered)
        {
            var bonuses = new List<string>();
            var bonusValues = new List<string>();
            var bonusIcons = new List<Sprite>();
            ModifierLines(skin, character.Modifiers, $"캐릭터 '{character.DisplayName}'", bonuses, bonusValues, bonusIcons);
            var c = new CardContent
            {
                Title = null,
                ShowcaseSprite = character.IdleSprite,
                ShowcasePortrait = true,
                ShowcaseBadge = null,
                Name = character.DisplayName.ToUpperInvariant(),
                RowLabel = AbilityLabel,
                RowNow = null,
                RowNext = CharacterData.AbilityUiName(character.AbilityKind),
                RowTint = skin.UpgradeText,
                SubLine = character.AbilityDescription,
                SubLineTint = Color.white,
                StatLabels = NoLines,
                StatNow = NoLines,
                StatNext = NoLines,
                StatTints = NoTints,
                BodyLines = bonuses.ToArray(),
                BodyValues = bonusValues.ToArray(),
                BodyIcons = bonusIcons.ToArray(),
                BodyValuesGain = true,
                Note = null,
            };
            DrawSelectable(skin, c, rect, scale, textScale, hovered);
        }

        // 보상 화면의 가로 카드 크기 (14×4타일).
        public static Vector2Int LandscapeSize(PixelUiSkin skin, int scale)
        {
            int tile = skin.TileSize * scale;
            return new Vector2Int(LandscapeWidthTiles * tile, LandscapeHeightTiles * tile);
        }

        // 보상 화면의 선택 카드 — 세로 카드(Draw)와 같은 내용(Build)을 가로로 편다 (2026-10-14). 세 장을 세로로 쌓아 오른쪽 표가 같은 x에 서므로 카드끼리
        // 줄을 위아래로 비교한다. 표의 줄은 라벨 바로 뒤에 값을 잇는 한 줄 문구다 (2026-10-17) — 그전엔 라벨은 표 왼쪽, 값은 오른쪽 끝으로 갈라 값이 한 열에
        // 섰지만 라벨과 값 사이 빈칸이 넓었다 (세로 카드가 2026-10-09에 한 줄 문구로 바꾼 이유와 같다).
        // 시작 화면·보기 카드는 세로 카드 그대로다. 크기는 고정(LandscapeSize)이라 내용이 넘치면 예외.
        public static void DrawLandscape(PixelUiSkin skin, UpgradeOption option, PlayerWeapons weapons, PlayerStats stats, Rect rect, int scale, int textScale, bool hovered)
        {
            var c = Build(skin, option, weapons, stats);
            if (c.Title == null || c.ShowcasePortrait)
                throw new System.Exception($"[PickShoot] UpgradeCardPainter.DrawLandscape: '{c.Name}' — 가로 카드는 종류 제목이 있고 아이템 타일인 선택 카드만 그린다.");
            int lineH = skin.FontPlain.lineHeight * textScale;
            int space = skin.FontPlain.spaceWidth * textScale;
            int iconSize = skin.TileSize * textScale;
            int pad = SidePaddingTexels * scale;
            int tableW = LandscapeTableWidthTexels * scale;
            int textX = LandscapeTextX(rect, scale);
            int tableX = (int)rect.xMax - pad - tableW;
            // 본문 줄의 아이콘은 표 왼쪽 바깥에 매달아(표 안의 글자 자리는 그대로 — 아이콘까지 표에 넣으면 RECHARGE RATE +30%가 340px를 넘는다) 열 사이 틈을
            // 아이콘이 대신한다 (2026-10-17, Recordings/stat_icons_mockup_2026-10-17_B.png).
            int bodyIconW = 0;
            for (int i = 0; i < c.BodyLines.Length; i++)
                bodyIconW = Mathf.Max(bodyIconW, BodyIconWidth(c, i, iconSize));
            int middleW = tableX - Mathf.Max(LandscapeColumnGapTexels * scale, bodyIconW) - textX;

            // 가운데 열의 폭·높이 검사.
            RequireFits(TitleWidth(skin, c, textScale, out int titleTextW), middleW, $"{c.Title} {c.TitleTag}", "가로 카드 가운데 열(제목)");
            RequireFits(PixelGui.MeasureText(skin.FontOutlined, c.Name, NameScale), middleW, c.Name, "가로 카드 가운데 열(이름)");
            RequireFits(RowWidth(skin, c.RowLabel, c.RowNow, c.RowNext, iconSize, space, textScale), middleW, $"{c.RowLabel} {c.RowNow} > {c.RowNext}", "가로 카드 가운데 열");
            if (c.SubLine != null)
                RequireFits(PixelGui.MeasureText(skin.FontPlain, c.SubLine, textScale), middleW, c.SubLine, "가로 카드 가운데 열");
            int middleH = skin.FontPlain.lineHeight * TitleScale + TitleNameGapTexels * scale + skin.FontOutlined.lineHeight * NameScale
                + NameShowcaseGapTexels * scale + (c.SubLine != null ? 2 : 1) * lineH;
            int innerTop = (int)rect.y + TopMarginTexels * scale;
            int innerBottom = (int)rect.yMax - BottomMinTexels * scale;
            if (innerTop + middleH > innerBottom)
                throw new System.Exception($"[PickShoot] UpgradeCardPainter.DrawLandscape: '{c.Name}' 가운데 열 높이 {middleH}px가 카드 안쪽을 넘음.");

            // 표: 스탯 줄 → (본문 간격) → 본문 줄 → 설명. 줄마다 라벨 바로 뒤에 값을 잇는 한 줄 문구를 표 왼쪽 끝부터 — 문구 전체의 폭으로 잰다.
            for (int i = 0; i < c.StatLabels.Length; i++)
                RequireFits(RowWidth(skin, c.StatLabels[i], c.StatNow[i], c.StatNext[i], iconSize, space, textScale),
                    tableW, $"{c.StatLabels[i]} {c.StatNow[i]} > {c.StatNext[i]}", "가로 카드 표");
            for (int i = 0; i < c.BodyLines.Length; i++)
                RequireFits(PixelGui.MeasureText(skin.FontPlain, BodyText(c.BodyLines[i], c.BodyValues[i]), textScale), tableW, BodyText(c.BodyLines[i], c.BodyValues[i]), "가로 카드 표");
            var noteLines = string.IsNullOrEmpty(c.Note) ? EmptyLines : PixelGui.WrapText(skin.FontPlain, c.Note, textScale, tableW);
            int bodyCount = c.BodyLines.Length + noteLines.Count;
            int tableH = c.StatLabels.Length * lineH + bodyCount * lineH;
            if (c.StatLabels.Length > 0 && bodyCount > 0) tableH += BodyGapTexels * scale;
            int tableTop = (int)rect.y + BottomMinTexels * scale;
            int tableRoom = (int)rect.height - 2 * BottomMinTexels * scale;
            if (tableH > tableRoom)
                throw new System.Exception($"[PickShoot] UpgradeCardPainter.DrawLandscape: '{c.Name}' 표 높이 {tableH}px({c.StatLabels.Length + bodyCount}줄)가 카드 안쪽 {tableRoom}px를 넘음.");

            if (hovered)
            {
                int o = HoverOutlineTexels * scale;
                PixelGui.FillPlateShape(new Rect(rect.x - o, rect.y - o, rect.width + 2 * o, rect.height + 2 * o), scale, Color.white);
            }
            PixelGui.DrawNineSlice(skin.PanelBlue, rect, skin.TileSize, scale);

            // 전시 타일 — 세로 가운데.
            int showcase = ItemTilePainter.Size(ShowcaseTileScale);
            ItemTilePainter.Draw(skin, c.ShowcaseSprite, c.ShowcaseBadge, c.ShowcaseModMark, (int)rect.x + pad, (int)rect.center.y - showcase / 2, ShowcaseTileScale);

            // 가운데 열 — 위 여백부터 왼쪽 맞춤. 제목 글자색은 GUI.color로 곱한다 (세로 카드와 같다).
            int y = innerTop;
            var prevColor = GUI.color;
            GUI.color = prevColor * c.TitleTint;
            PixelGui.DrawText(skin.FontPlain, c.Title, textX, y, TitleScale);
            if (c.TitleTag != null)
                PixelGui.DrawText(skin.FontPlain, c.TitleTag, textX + titleTextW + space, y + (skin.FontPlain.capTop + skin.FontPlain.capHeight) * (TitleScale - textScale), textScale);
            GUI.color = prevColor;
            y += skin.FontPlain.lineHeight * TitleScale + TitleNameGapTexels * scale;
            PixelGui.DrawText(skin.FontOutlined, c.Name, textX, y, NameScale);
            y += skin.FontOutlined.lineHeight * NameScale + NameShowcaseGapTexels * scale;
            DrawRowAt(skin, c.RowLabel, c.RowNow, c.RowNext, c.RowTint, textX, y, iconSize, space, textScale);
            y += lineH;
            if (c.SubLine != null)
                DrawRowAt(skin, null, null, c.SubLine, c.SubLineTint, textX, y, iconSize, space, textScale);

            // 오른쪽 표 — 세로 가운데.
            y = tableTop + (tableRoom - tableH) / 2;
            for (int i = 0; i < c.StatLabels.Length; i++)
            {
                DrawRowAt(skin, c.StatLabels[i], c.StatNow[i], c.StatNext[i], c.StatTints[i], tableX, y, iconSize, space, textScale);
                y += lineH;
            }
            if (c.StatLabels.Length > 0 && bodyCount > 0) y += BodyGapTexels * scale;
            var valueTint = c.BodyValuesGain ? skin.UpgradeText : Color.white;
            for (int i = 0; i < c.BodyLines.Length; i++)
            {
                if (BodyIconWidth(c, i, iconSize) > 0)
                    PixelGui.DrawSprite(c.BodyIcons[i], new Rect(tableX - iconSize, y, iconSize, iconSize));
                PixelGui.DrawText(skin.FontPlain, c.BodyLines[i], tableX, y, textScale);
                if (c.BodyValues[i] != null)
                {
                    GUI.color = prevColor * valueTint;
                    PixelGui.DrawText(skin.FontPlain, c.BodyValues[i], BodyValueX(skin, c.BodyLines[i], tableX, textScale), y, textScale);
                    GUI.color = prevColor;
                }
                y += lineH;
            }
            for (int i = 0; i < noteLines.Count; i++)
            {
                PixelGui.DrawText(skin.FontPlain, noteLines[i], tableX, y, textScale);
                y += lineH;
            }
        }

        // 가로 카드의 가운데 열 왼쪽 끝 — 전시 타일 오른쪽. 리롤 칸(DrawLandscapeAction)의 제목도 여기에 맞춘다.
        static int LandscapeTextX(Rect rect, int scale)
        {
            return (int)rect.x + SidePaddingTexels * scale + ItemTilePainter.Size(ShowcaseTileScale) + LandscapeShowcaseGapTexels * scale;
        }

        // 가로 카드 열 아래의 행동 칸 (보상 화면의 리롤, 2026-10-14) — 선택지가 아니라는 것이 읽히게 회색 패널에 높이는 카드의 절반(2타일, 9-slice 최소)이다.
        // 아이콘은 판 없이 전시 타일 열의 가운데에(아이템이 아니라 행동), 제목(일반 글꼴 배율 3)과 설명 한 줄은 가운데 열 x에, rightText는 표의 오른쪽 끝에.
        // tint는 칸 전체(패널·아이콘·글자)에 곱한다 — 누를 수 없을 때 어둡게. rightTint는 rightText에만 더 곱한다.
        // R 타일·아이콘 없음·수량을 제목에 넣는 안과 비교해 골랐다 (Recordings/reward_reroll_icon_mockup_2026-10-14.png).
        public static void DrawLandscapeAction(PixelUiSkin skin, Rect rect, Sprite icon, string title, string description, string rightText, Color rightTint, Color tint, bool hovered, int scale, int textScale)
        {
            int minH = 2 * skin.TileSize * scale;
            if ((int)rect.height < minH)
                throw new System.Exception($"[PickShoot] UpgradeCardPainter.DrawLandscapeAction: 높이 {rect.height}px가 9-slice 최소 {minH}px보다 작음.");
            int lineH = skin.FontPlain.lineHeight * textScale;
            int titleH = skin.FontPlain.lineHeight * TitleScale;
            int textX = LandscapeTextX(rect, scale);
            int right = (int)rect.xMax - SidePaddingTexels * scale;
            int textW = PixelGui.MeasureText(skin.FontPlain, title, TitleScale);
            int descW = PixelGui.MeasureText(skin.FontPlain, description, textScale);
            int rightW = PixelGui.MeasureText(skin.FontPlain, rightText, textScale);
            if (textX + Mathf.Max(textW, descW) + LandscapeColumnGapTexels * scale > right - rightW)
                throw new System.Exception($"[PickShoot] UpgradeCardPainter.DrawLandscapeAction: '{title}'·'{description}'이 오른쪽 '{rightText}'과 맞닿음. 문구를 줄일 것.");
            int iconW = (int)icon.rect.width * scale;
            int iconH = (int)icon.rect.height * scale;
            if (iconH > (int)rect.height - 2 * BottomMinTexels * scale)
                throw new System.Exception($"[PickShoot] UpgradeCardPainter.DrawLandscapeAction: 아이콘 높이 {iconH}px가 칸 안쪽을 넘음.");

            var prev = GUI.color;
            if (hovered)
            {
                int o = HoverOutlineTexels * scale;
                PixelGui.FillPlateShape(new Rect(rect.x - o, rect.y - o, rect.width + 2 * o, rect.height + 2 * o), scale, Color.white);
            }
            GUI.color = prev * tint;
            PixelGui.DrawNineSlice(skin.PanelGrey, rect, skin.TileSize, scale);
            int iconCenterX = (int)rect.x + SidePaddingTexels * scale + ItemTilePainter.Size(ShowcaseTileScale) / 2;
            PixelGui.DrawSprite(icon, new Rect(iconCenterX - iconW / 2, (int)rect.center.y - iconH / 2, iconW, iconH));
            int textTop = (int)rect.center.y - (titleH + TitleNameGapTexels * scale + lineH) / 2;
            PixelGui.DrawText(skin.FontPlain, title, textX, textTop, TitleScale);
            PixelGui.DrawText(skin.FontPlain, description, textX, textTop + titleH + TitleNameGapTexels * scale, textScale);
            GUI.color = prev * tint * rightTint;
            PixelGui.DrawTextRight(skin.FontPlain, rightText, right, (int)rect.center.y - lineH / 2, textScale);
            GUI.color = prev;
        }

        // 선택 카드의 공통 그리기 — 파란 패널, 호버면 테두리 넓히기. 크기는 고정이라 내용이 넘치면 예외.
        static void DrawSelectable(PixelUiSkin skin, CardContent c, Rect rect, int scale, int textScale, bool hovered)
        {
            var noteLines = Measure(skin, c, (int)rect.width, scale, textScale, out int contentH);
            int topMargin = TopMarginTexels * scale;
            int bottomMin = BottomMinTexels * scale;
            int innerH = (int)rect.height - topMargin - bottomMin;
            if (contentH > innerH)
                throw new System.Exception($"[PickShoot] UpgradeCardPainter: 카드 내용 높이 {contentH}px가 카드 안쪽 높이 {innerH}px(위 여백 {topMargin}·아래 최소 여백 {bottomMin} 제외)를 넘음 (본문 {c.BodyLines.Length + noteLines.Count}줄). 본문 문구를 줄이거나 간격을 줄일 것 — 카드를 키우려면 보상 화면 블록(UpgradeManager)이 캔버스 세로에 드는지 볼 것.");

            // 호버면 카드보다 한 텍셀 큰 흰 판 모양을 먼저 깔아 테두리를 바깥으로 넓힌다.
            if (hovered)
            {
                int o = HoverOutlineTexels * scale;
                PixelGui.FillPlateShape(new Rect(rect.x - o, rect.y - o, rect.width + 2 * o, rect.height + 2 * o), scale, Color.white);
            }
            // 획득·레벨업·개조·유물 카드는 같은 파란 패널.
            PixelGui.DrawNineSlice(skin.PanelBlue, rect, skin.TileSize, scale);
            DrawContent(skin, c, noteLines, rect, scale, textScale);
        }

        // 보유 자루의 보기 카드 — 지금 상태(레벨·개조)의 값. anchor는 그 자루의 빌드 격자 타일(캔버스 픽셀).
        public static void DrawOwnedWeapon(PixelUiSkin skin, PixelGui.CanvasScope canvas, Weapon weapon, PlayerStats stats, Rect anchor)
        {
            // 값은 모두 흰색 값 하나 — 비교할 "현재"가 없는 획득 카드와 같은 표를, 얻는 값의 색 없이 만든다.
            var rows = BuildWeaponStats(weapon.Data, weapon.Mod, 0, weapon.Level, stats.DamageMultiplier, stats.MaxAmmoBonus, Color.white);
            var flags = WeaponFlags(weapon.Data);
            var c = new CardContent
            {
                Title = null,
                ShowcaseSprite = weapon.Data.WeaponSprite,
                ShowcaseBadge = ItemInfoText.WeaponBadge(weapon.Level),
                ShowcaseModMark = ItemInfoText.ModMark(weapon.Mod),
                Name = weapon.Data.DisplayName.ToUpperInvariant(),
                RowLabel = LevelLabel,
                RowNow = null,
                RowNext = weapon.Level.ToString(),
                RowTint = Color.white,
                SubLine = weapon.Mod != null ? weapon.Mod.UiName : null,
                SubLineTint = skin.ModText,
                StatLabels = rows.Labels,
                StatNow = rows.Now,
                StatNext = rows.Next,
                StatTints = rows.Tints,
                BodyLines = flags,
                BodyValues = new string[flags.Length],
                Note = JoinNotes(weapon.Data.Description, weapon.Mod != null ? weapon.Mod.Description : null),
            };
            DrawOwned(skin, canvas, c, anchor);
        }

        // 보유 유물 한 종류의 보기 카드 — 수정치는 1개분(선택 카드와 같은 값을 흰색으로), 보유 수는 배지 `X n`이 말한다 (2026-10-12).
        // 보유분 합(`+30%`)은 툴팁 때 적었지만 고르지 않았다 — 같은 유물의 선택 카드와 값이 갈리고, 합은 배지와 곱해 읽힌다.
        public static void DrawOwnedRelic(PixelUiSkin skin, PixelGui.CanvasScope canvas, RelicData relic, int count, Rect anchor)
        {
            var effects = new List<string>();
            var effectValues = new List<string>();
            var effectIcons = new List<Sprite>();
            ModifierLines(skin, relic.Modifiers, $"유물 '{relic.DisplayName}'", effects, effectValues, effectIcons);
            var c = new CardContent
            {
                Title = null,
                ShowcaseSprite = relic.Icon,
                ShowcaseBadge = ItemInfoText.RelicBadge(count),
                Name = relic.DisplayName.ToUpperInvariant(),
                RowLabel = null,
                RowNow = null,
                RowNext = relic.Grade.ToString().ToUpperInvariant(),
                RowTint = GradeTint(relic.Grade),
                StatLabels = NoLines,
                StatNow = NoLines,
                StatNext = NoLines,
                StatTints = NoTints,
                BodyLines = effects.ToArray(),
                BodyValues = effectValues.ToArray(),
                BodyIcons = effectIcons.ToArray(),
                BodyValuesGain = false,
                Note = relic.Description,
            };
            DrawOwned(skin, canvas, c, anchor);
        }

        // 고른 캐릭터의 보기 카드 — 빌드 격자 유물 줄 첫 칸 (Characters §4, 2026-10-14). 유물 보기 카드와 같은 틀: 전시 칸은 격자와 같은 아이콘 타일 + `X1`,
        // 첫 줄은 등급 자리의 `CHARACTER`. 그 아래 덩이(무기 카드의 스탯 줄 자리)에 능력 이름과 설명, 본문에 스탯 보너스 — 능력이 정체성이라 보너스보다 먼저 읽힌다.
        // 보너스 뒤 설명 문단으로 능력을 두는 안(유물 카드와 줄 구성까지 같은 꼴)은 능력이 보너스 밑에 묻혀 뺐다 (Recordings/character_view_card_mockup_2026-10-14_v2.png).
        // 이미 가진 것이라 값은 모두 흰색이다. 시작 화면의 캐릭터 카드(DrawCharacter)는 전신 초상 타일이고 이 등급 줄이 없다 — 거기서는 모두가 캐릭터다.
        public static void DrawOwnedCharacter(PixelUiSkin skin, PixelGui.CanvasScope canvas, CharacterData character, Rect anchor)
        {
            var bonuses = new List<string>();
            var bonusValues = new List<string>();
            var bonusIcons = new List<Sprite>();
            ModifierLines(skin, character.Modifiers, $"캐릭터 '{character.DisplayName}'", bonuses, bonusValues, bonusIcons);
            var c = new CardContent
            {
                Title = null,
                ShowcaseSprite = character.Icon,
                ShowcaseBadge = ItemInfoText.RelicBadge(1),
                Name = character.DisplayName.ToUpperInvariant(),
                RowLabel = null,
                RowNow = null,
                RowNext = CharacterGrade,
                RowTint = Color.white,
                StatLabels = new[] { AbilityLabel, null },
                StatNow = new string[2],
                StatNext = new[] { CharacterData.AbilityUiName(character.AbilityKind), character.AbilityDescription },
                StatTints = new[] { Color.white, Color.white },
                BodyLines = bonuses.ToArray(),
                BodyValues = bonusValues.ToArray(),
                BodyIcons = bonusIcons.ToArray(),
                BodyValuesGain = false,
                Note = null,
            };
            DrawOwned(skin, canvas, c, anchor);
        }

        // 보기 카드 — 폭은 선택 카드와 같고 높이는 내용 + 위아래 여백(둘 다 위 여백 값). 자리는 타일 위 가운데, 위에 자리가 없으면 아래, 둘 다 없으면
        // 넓은 쪽 캔버스 끝에 붙인다 — 타일을 조금 가린다. 지금 배치에서는 생기지 않는다 — 두 화면의 상태 열(StatusColumnPainter)은 위 끝이 56px라 가장 긴
        // 보기 카드(512px, 기본 수치 특성이 있는 자루에 다른 수치 개조)가 무기 첫 줄에선 아래에, 둘째 줄부터는 위에 든다 (2026-10-17. 그전 일시정지 화면은
        // 무기 줄 위에 들도록 띠 → 격자 간격을 160px로 벌렸다).
        // 좌우는 캔버스 안으로 민다.
        // 이웃 타일·카드 위로 뜨므로 화면이 다른 요소를 다 그린 뒤 맨 마지막에 부른다.
        static void DrawOwned(PixelUiSkin skin, PixelGui.CanvasScope canvas, CardContent c, Rect anchor)
        {
            int scale = PixelGui.PanelScale;
            int textScale = PixelGui.TextScale;
            int w = CardWidthTiles * skin.TileSize * scale;
            var noteLines = Measure(skin, c, w, scale, textScale, out int contentH);
            int h = contentH + 2 * TopMarginTexels * scale;
            if (h > canvas.Height)
                throw new System.Exception($"[PickShoot] UpgradeCardPainter: 보기 카드 '{c.Name}'의 높이 {h}px가 캔버스 {canvas.Height}px를 넘음. 본문 문구를 줄일 것.");

            int gap = AnchorGapTexels * scale;
            int x = Mathf.Clamp((int)anchor.center.x - w / 2, 0, canvas.Width - w);
            int above = (int)anchor.y - gap;
            int below = canvas.Height - (int)anchor.yMax - gap;
            int y;
            if (h <= above) y = above - h;
            else if (h <= below) y = (int)anchor.yMax + gap;
            else y = above >= below ? 0 : canvas.Height - h;

            var rect = new Rect(x, y, w, h);
            PixelGui.DrawNineSlice(skin.PanelGrey, rect, skin.TileSize, scale);
            DrawContent(skin, c, noteLines, rect, scale, textScale);
        }

        // 내용의 폭을 검사하고(넘치면 예외 — 카드 폭은 늘리지 않는다) 접은 설명 줄과 내용 높이(위아래 여백 제외)를 낸다. width는 카드 폭.
        static List<string> Measure(PixelUiSkin skin, CardContent c, int width, int scale, int textScale, out int contentH)
        {
            int innerW = width - 2 * SidePaddingTexels * scale;
            int lineH = skin.FontPlain.lineHeight * textScale;
            int space = skin.FontPlain.spaceWidth * textScale;
            // 화살표는 글자 배율의 아이콘 한 칸.
            int iconSize = skin.TileSize * textScale;

            // 제목·이름과 첫 줄·스탯·본문 줄은 카드 안쪽 폭에 — 첫 줄·스탯 줄은 한 줄 문구 전체의 폭으로 잰다.
            if (c.Title != null)
                RequireFits(TitleWidth(skin, c, textScale, out _), innerW, $"{c.Title} {c.TitleTag}", "카드 안쪽(제목)");
            RequireFits(PixelGui.MeasureText(skin.FontOutlined, c.Name, NameScale), innerW, c.Name, "카드 안쪽(이름)");
            RequireFits(RowWidth(skin, c.RowLabel, c.RowNow, c.RowNext, iconSize, space, textScale), innerW, $"{c.RowLabel} {c.RowNow} > {c.RowNext}", "카드 안쪽");
            if (c.SubLine != null)
                RequireFits(PixelGui.MeasureText(skin.FontPlain, c.SubLine, textScale), innerW, c.SubLine, "카드 안쪽");
            for (int i = 0; i < c.StatLabels.Length; i++)
                RequireFits(RowWidth(skin, c.StatLabels[i], c.StatNow[i], c.StatNext[i], iconSize, space, textScale), innerW, $"{c.StatLabels[i]} {c.StatNow[i]} > {c.StatNext[i]}", "카드 안쪽");

            // 본문 줄: 한 항목이 한 줄(줄 안에서 나누지 않는다 — 항목 하나가 폭을 넘으면 문구를 줄여야 한다), 설명은 단어 단위로 접는다.
            for (int i = 0; i < c.BodyLines.Length; i++)
            {
                string line = BodyText(c.BodyLines[i], c.BodyValues[i]);
                RequireFits(BodyIconWidth(c, i, iconSize) + PixelGui.MeasureText(skin.FontPlain, line, textScale), innerW, line, "카드 안쪽");
            }
            var noteLines = string.IsNullOrEmpty(c.Note)
                ? EmptyLines
                : PixelGui.WrapText(skin.FontPlain, c.Note, textScale, innerW);
            int bodyLines = c.BodyLines.Length + noteLines.Count;

            int gap = RowGapTexels * scale;
            int firstLines = c.SubLine != null ? 2 : 1;
            contentH = HeaderHeight(skin, c, scale) + gap + firstLines * lineH + gap + c.StatLabels.Length * lineH;
            if (bodyLines > 0) contentH += BodyGapTexels * scale + bodyLines * lineH;
            return noteLines;
        }

        // 전시 칸의 한 변 — 아이템 타일 또는 초상 타일.
        static int ShowcaseHeight(CardContent c)
        {
            return c.ShowcasePortrait ? ItemTilePainter.PortraitSize(ShowcaseTileScale) : ItemTilePainter.Size(ShowcaseTileScale);
        }

        // 머리: 제목(배율 3, 꼬리와 한 덩이 — 보기 카드는 없음) → 이름(아웃라인 배율 2, 한 줄) → 전시 타일. 선택 카드는 높이가 모두 같아 첫 줄 자리도 같다
        // (캐릭터 카드만 전시 칸이 초상 타일이라 32px 높다 — 다른 선택 카드와 한 화면에 서지 않는다).
        static int HeaderHeight(PixelUiSkin skin, CardContent c, int scale)
        {
            int h = skin.FontOutlined.lineHeight * NameScale + NameShowcaseGapTexels * scale + ShowcaseHeight(c);
            if (c.Title != null) h += skin.FontPlain.lineHeight * TitleScale + TitleNameGapTexels * scale;
            return h;
        }

        // 제목과 꼬리를 한 덩이로 잰 폭. titleTextW는 제목 글자만의 폭(꼬리를 놓을 자리).
        static int TitleWidth(PixelUiSkin skin, CardContent c, int textScale, out int titleTextW)
        {
            titleTextW = PixelGui.MeasureText(skin.FontPlain, c.Title, TitleScale);
            int w = titleTextW;
            if (c.TitleTag != null) w += skin.FontPlain.spaceWidth * textScale + PixelGui.MeasureText(skin.FontPlain, c.TitleTag, textScale);
            return w;
        }

        // 내용을 rect 안에 위 여백부터 가운데 맞춤으로 쌓는다 — 위 여백은 고정, 남는 공간은 아래 여백이라 어떤 종류든 내용 위치가 같다. 폭·높이는 Measure가 검사했다.
        static void DrawContent(PixelUiSkin skin, CardContent c, List<string> noteLines, Rect rect, int scale, int textScale)
        {
            int gap = RowGapTexels * scale;
            int lineH = skin.FontPlain.lineHeight * textScale;
            int space = skin.FontPlain.spaceWidth * textScale;
            int iconSize = skin.TileSize * textScale;
            int showcaseH = ShowcaseHeight(c);
            int centerX = (int)rect.center.x;
            int y = (int)rect.y + TopMarginTexels * scale;

            // 머리 — 제목, 이름, 전시 타일(그림과 상태 배지)을 차례로 가운데에.
            // 제목 글자색은 GUI.color로 곱한다 — 글리프의 흰 획이 그 색이 되고 어두운 상자도 함께 어두워진다 (XP 팝업과 같은 방식). 꼬리도 같은 색.
            if (c.Title != null)
            {
                int titleX = centerX - TitleWidth(skin, c, textScale, out int titleTextW) / 2;
                var prevColor = GUI.color;
                GUI.color = prevColor * c.TitleTint;
                PixelGui.DrawText(skin.FontPlain, c.Title, titleX, y, TitleScale);
                if (c.TitleTag != null)
                {
                    // 꼬리는 글자 배율 — 글리프 상자의 아래 변을 제목에 맞춘다 (상자 아래 변 = 줄 위 y + (capTop + capHeight) × 배율).
                    int tagX = titleX + titleTextW + space;
                    int tagY = y + (skin.FontPlain.capTop + skin.FontPlain.capHeight) * (TitleScale - textScale);
                    PixelGui.DrawText(skin.FontPlain, c.TitleTag, tagX, tagY, textScale);
                }
                GUI.color = prevColor;
                y += skin.FontPlain.lineHeight * TitleScale + TitleNameGapTexels * scale;
            }
            PixelGui.DrawTextCentered(skin.FontOutlined, c.Name, centerX, y, NameScale);
            y += skin.FontOutlined.lineHeight * NameScale + NameShowcaseGapTexels * scale;
            int showcaseX = centerX - showcaseH / 2;
            if (c.ShowcasePortrait)
            {
                if (c.ShowcaseBadge != null || c.ShowcaseModMark != null)
                    throw new System.Exception($"[PickShoot] UpgradeCardPainter: 초상 타일에는 배지가 없음 ('{c.Name}'의 배지 '{c.ShowcaseBadge}').");
                ItemTilePainter.DrawPortrait(skin, c.ShowcaseSprite, showcaseX, y, ShowcaseTileScale);
            }
            else
                ItemTilePainter.Draw(skin, c.ShowcaseSprite, c.ShowcaseBadge, c.ShowcaseModMark, showcaseX, y, ShowcaseTileScale);
            y += showcaseH + gap;

            DrawRow(skin, c.RowLabel, c.RowNow, c.RowNext, c.RowTint, centerX, y, iconSize, space, textScale);
            y += lineH;
            if (c.SubLine != null)
            {
                DrawRow(skin, null, null, c.SubLine, c.SubLineTint, centerX, y, iconSize, space, textScale);
                y += lineH;
            }
            y += gap;

            for (int i = 0; i < c.StatLabels.Length; i++)
            {
                DrawRow(skin, c.StatLabels[i], c.StatNow[i], c.StatNext[i], c.StatTints[i], centerX, y, iconSize, space, textScale);
                y += lineH;
            }

            // 본문도 가운데 맞춤 — 스탯 줄과는 본문 간격으로 덩어리가 갈린다. 무기의 수치 없는 특성과 유물 카드의 수정치가 여기 온다.
            // 글꼴은 표의 값과 같은 일반 글꼴 (2026-10-08, 그전엔 아웃라인 — 카드 안에서 이름만 아웃라인이게).
            if (c.BodyLines.Length + noteLines.Count > 0) y += BodyGapTexels * scale;
            var valueTint = c.BodyValuesGain ? skin.UpgradeText : Color.white;
            for (int i = 0; i < c.BodyLines.Length; i++)
            {
                // 값이 있으면 문구(흰색)와 값을, 아이콘이 있으면 그것까지 한 덩이로 가운데에.
                int iconW = BodyIconWidth(c, i, iconSize);
                int bx = centerX - (iconW + PixelGui.MeasureText(skin.FontPlain, BodyText(c.BodyLines[i], c.BodyValues[i]), textScale)) / 2;
                if (iconW > 0)
                {
                    PixelGui.DrawSprite(c.BodyIcons[i], new Rect(bx, y, iconSize, iconSize));
                    bx += iconW;
                }
                PixelGui.DrawText(skin.FontPlain, c.BodyLines[i], bx, y, textScale);
                if (c.BodyValues[i] != null)
                {
                    var prevBody = GUI.color;
                    GUI.color = prevBody * valueTint;
                    PixelGui.DrawText(skin.FontPlain, c.BodyValues[i], BodyValueX(skin, c.BodyLines[i], bx, textScale), y, textScale);
                    GUI.color = prevBody;
                }
                y += lineH;
            }
            for (int i = 0; i < noteLines.Count; i++)
            {
                PixelGui.DrawTextCentered(skin.FontPlain, noteLines[i], centerX, y, textScale);
                y += lineH;
            }
        }

        // area는 그 문구가 들어가야 하는 칸의 이름 (예외 문구용).
        static void RequireFits(int width, int maxWidth, string what, string area)
        {
            if (width > maxWidth)
                throw new System.Exception($"[PickShoot] UpgradeCardPainter: 카드 내용 '{what}'의 폭 {width}px가 {area} 폭 {maxWidth}px를 넘음. 카드 크기는 고정이라 내용에 맞춰 늘지 않는다 — CardWidthTiles를 늘리거나 문구를 줄일 것.");
        }

        static CardContent Build(PixelUiSkin skin, UpgradeOption option, PlayerWeapons weapons, PlayerStats stats)
        {
            switch (option.Kind)
            {
                case UpgradeKind.AcquireWeapon:
                {
                    var d = option.AcquireData;
                    // 시작 레벨(UpgradeOption.Levels)은 보통 1. 둘 이상이면 제목이 `NEW (+n)`이고 표·배지도 그 레벨의 값.
                    int start = option.Levels;
                    var acquired = BuildWeaponStats(d, null, 0, start, stats.DamageMultiplier, stats.MaxAmmoBonus, skin.UpgradeText);
                    var acquiredFlags = WeaponFlags(d);
                    return new CardContent
                    {
                        Title = NewTitle,
                        TitleTag = LevelTag(start),
                        TitleTint = skin.HighlightText,
                        ShowcaseSprite = d.WeaponSprite,
                        ShowcaseBadge = ItemInfoText.WeaponBadge(start),
                        Name = d.DisplayName.ToUpperInvariant(),
                        RowLabel = LevelLabel,
                        RowNow = null,
                        RowNext = start.ToString(),
                        RowTint = skin.UpgradeText,
                        StatLabels = acquired.Labels,
                        StatNow = acquired.Now,
                        StatNext = acquired.Next,
                        StatTints = acquired.Tints,
                        BodyLines = acquiredFlags,
                        BodyValues = new string[acquiredFlags.Length],
                        Note = d.Description,
                    };
                }

                case UpgradeKind.LevelUp:
                {
                    if (option.TargetSlotIndex >= weapons.Slots.Count)
                        throw new System.Exception($"[PickShoot] UpgradeCardPainter: 레벨업 카드의 slotIndex {option.TargetSlotIndex}가 범위 밖 (현재 슬롯 {weapons.Slots.Count}개).");
                    var w = weapons.Slots[option.TargetSlotIndex];
                    // 오르는 단계 수(UpgradeOption.Levels)는 보통 1. 둘 이상이면 제목이 `UPGRADE (+n)`이고 "다음" 값·배지가 그 레벨의 값.
                    int next = w.Level + option.Levels;
                    if (next > Weapon.MaxLevel)
                        throw new System.Exception($"[PickShoot] UpgradeCardPainter: 레벨업 카드가 '{w.Data.DisplayName}' 레벨 {w.Level}에 {option.Levels}를 더하면 최대 {Weapon.MaxLevel}을 넘음. 후보를 만들 때 걸러야 함 (UpgradeManager).");
                    var levelled = BuildWeaponStats(w.Data, w.Mod, w.Level, next, stats.DamageMultiplier, stats.MaxAmmoBonus, skin.UpgradeText);
                    var levelledFlags = WeaponFlags(w.Data);
                    return new CardContent
                    {
                        Title = LevelUpTitle,
                        TitleTag = LevelTag(option.Levels),
                        TitleTint = skin.UpgradeText,
                        ShowcaseSprite = w.Data.WeaponSprite,
                        ShowcaseBadge = ItemInfoText.WeaponBadge(next),
                        ShowcaseModMark = ItemInfoText.ModMark(w.Mod),
                        Name = w.Data.DisplayName.ToUpperInvariant(),
                        RowLabel = LevelLabel,
                        RowNow = w.Level.ToString(),
                        RowNext = next.ToString(),
                        RowTint = skin.UpgradeText,
                        SubLine = w.Mod != null ? w.Mod.UiName : null,
                        SubLineTint = skin.ModText,
                        StatLabels = levelled.Labels,
                        StatNow = levelled.Now,
                        StatNext = levelled.Next,
                        StatTints = levelled.Tints,
                        BodyLines = levelledFlags,
                        BodyValues = new string[levelledFlags.Length],
                        Note = JoinNotes(w.Data.Description, w.Mod != null ? w.Mod.Description : null),
                    };
                }

                case UpgradeKind.Mod:
                {
                    if (option.TargetSlotIndex >= weapons.Slots.Count)
                        throw new System.Exception($"[PickShoot] UpgradeCardPainter: 개조 카드의 slotIndex {option.TargetSlotIndex}가 범위 밖 (현재 슬롯 {weapons.Slots.Count}개).");
                    var w = weapons.Slots[option.TargetSlotIndex];
                    var m = option.ModData;
                    if (w.Mod != null)
                        throw new System.Exception($"[PickShoot] UpgradeCardPainter: 개조 카드가 이미 '{w.Mod.DisplayName}'가 붙은 '{w.Data.DisplayName}'를 가리킴. 후보를 만들 때 걸러야 함 (UpgradeManager).");
                    var modded = BuildModStats(w.Data, m, w.Level, stats.DamageMultiplier, stats.MaxAmmoBonus, skin.UpgradeText);
                    var moddedFlags = WeaponFlags(w.Data);
                    return new CardContent
                    {
                        Title = ModTitle,
                        TitleTag = null,
                        TitleTint = skin.ModText,
                        ShowcaseSprite = w.Data.WeaponSprite,
                        // 개조 카드가 가리키는 것은 자루다 — 그 자루의 그림과 붙인 뒤의 배지 (Mods §4.1).
                        ShowcaseBadge = ItemInfoText.WeaponBadge(w.Level),
                        ShowcaseModMark = m.BadgeMark,
                        Name = w.Data.DisplayName.ToUpperInvariant(),
                        RowLabel = LevelLabel,
                        RowNow = null,
                        RowNext = w.Level.ToString(),
                        RowTint = Color.white,
                        SubLine = "+" + m.UiName,
                        SubLineTint = skin.ModText,
                        StatLabels = modded.Labels,
                        StatNow = modded.Now,
                        StatNext = modded.Next,
                        StatTints = modded.Tints,
                        BodyLines = moddedFlags,
                        BodyValues = new string[moddedFlags.Length],
                        Note = JoinNotes(w.Data.Description, m.Description),
                    };
                }

                case UpgradeKind.Relic:
                {
                    var d = option.RelicData;
                    // 유물 카드의 표는 등급 한 줄뿐이다 — 그 유물이 더하는 수정치를 `DAMAGE +10%` 한 줄로 본문(가운데 맞춤, 일반 글꼴)에 둔다 (2026-10-07).
                    // 라벨과 값을 좌우 끝으로 갈라 놓으면(표) 한 줄짜리 효과를 읽는 데 눈이 빈칸을 건너야 한다. 값은 "지금 총계 → 획득 뒤 총계"였다가
                    // 수정치 하나로 바꿨다 — 카드가 답할 것은 "이걸 고르면 무엇이 붙나"이고, 총계는 일시정지 화면이 보일 자리다 (Relics §7).
                    // 무기 카드의 수치 없는 특성 줄도 2026-10-09부터 같은 본문 줄이다 (그 사이 표의 라벨 열에 붙어 있었다).
                    // 수정치 값(`+10%`)은 얻는 값이라 초록 (2026-10-11).
                    var effects = new List<string>();
                    var effectValues = new List<string>();
                    var effectIcons = new List<Sprite>();
                    ModifierLines(skin, d.Modifiers, $"유물 '{d.DisplayName}'", effects, effectValues, effectIcons);
                    int owned = stats.CountOf(d);
                    return new CardContent
                    {
                        Title = NewTitle,
                        TitleTag = null,
                        TitleTint = skin.HighlightText,
                        ShowcaseSprite = d.Icon,
                        ShowcaseBadge = ItemInfoText.RelicBadge(owned + 1),
                        Name = d.DisplayName.ToUpperInvariant(),
                        // 무기 카드의 레벨 줄 자리에 라벨 없는 등급 이름 (2026-10-09). 보유 수 줄(`OWNED 1 -> 2`)은 2026-10-07에 뺐다 — 전시 배지 `X n`이 말한다.
                        RowLabel = null,
                        RowNow = null,
                        RowNext = d.Grade.ToString().ToUpperInvariant(),
                        RowTint = GradeTint(d.Grade),
                        StatLabels = NoLines,
                        StatNow = NoLines,
                        StatNext = NoLines,
                        StatTints = NoTints,
                        BodyLines = effects.ToArray(),
                        BodyValues = effectValues.ToArray(),
                        BodyIcons = effectIcons.ToArray(),
                        BodyValuesGain = true,
                        // 설명 문구는 스탯 줄이 못 하는 말이 있을 때만 — 커먼 7종은 비어 있다 (Relics §2).
                        Note = d.Description,
                    };
                }

                default:
                    throw new System.Exception($"[PickShoot] UpgradeCardPainter: 알 수 없는 Kind {option.Kind}");
            }
        }

        struct StatRows
        {
            public string[] Labels;
            public string[] Now;
            public string[] Next;
            public Color[] Tints;
        }

        // 무기 카드의 표 — DMG·AMMO 뒤에 그 무기의 수치 특성(점사·튕김·관통·폭발, 개조를 합친 값)이 같은 줄로 이어진다 (ItemInfoText.WeaponTraitStats).
        // nowLevel이 0이면 획득 카드다 — 아직 없는 자루라 비교할 "현재"가 없고, 모든 줄이 값 하나다 (2026-10-07. 그전에는 "현재" 열에 `-`를 두어
        // 획득·레벨업 카드의 줄 구성을 맞췄는데, 읽는 사람에게는 뜻 없는 열이었다).
        // 레벨업 카드에서도 레벨로 바뀌지 않는 특성은 값 하나다 — 같은 수를 화살표 양쪽에 두면 그것도 오르는 값으로 읽힌다.
        // 얻는 값은 gain 색 — 획득 카드는 모든 값, 레벨업 카드는 바뀐 줄의 다음 값.
        static StatRows BuildWeaponStats(WeaponData data, ModData mod, int nowLevel, int nextLevel, float damageMultiplier, int maxAmmoBonus, Color gain)
        {
            var labels = new List<string>(ItemInfoText.WeaponStatLabels);
            var next = new List<string>(ItemInfoText.WeaponStatValues(data, nextLevel, damageMultiplier, maxAmmoBonus));
            var now = new List<string>();
            if (nowLevel > 0) now.AddRange(ItemInfoText.WeaponStatValues(data, nowLevel, damageMultiplier, maxAmmoBonus));
            else for (int i = 0; i < labels.Count; i++) now.Add(null);

            var traitLabels = new List<string>();
            var traitValues = new List<string>();
            ItemInfoText.WeaponTraitStats(data, mod, nextLevel, maxAmmoBonus, traitLabels, traitValues);
            var nowLabels = new List<string>();
            var nowValues = new List<string>();
            if (nowLevel > 0) ItemInfoText.WeaponTraitStats(data, mod, nowLevel, maxAmmoBonus, nowLabels, nowValues);
            for (int i = 0; i < traitLabels.Count; i++)
            {
                labels.Add(traitLabels[i]);
                next.Add(traitValues[i]);
                int j = nowLabels.IndexOf(traitLabels[i]);
                now.Add(j >= 0 && nowValues[j] != traitValues[i] ? nowValues[j] : null);
            }
            var tints = new Color[labels.Count];
            for (int i = 0; i < tints.Length; i++)
                tints[i] = nowLevel == 0 || (now[i] != null && now[i] != next[i]) ? gain : Color.white;
            return new StatRows { Labels = labels.ToArray(), Now = now.ToArray(), Next = next.ToArray(), Tints = tints };
        }

        // 개조 카드의 표 (Mods §4.1) — 레벨업 카드처럼 DMG·AMMO와 특성을 모두 적되 레벨은 그대로라 바뀌지 않는 줄은 흰 값 하나, 개조가 바꾸는 특성만
        // "개조 전 → 개조 뒤"(다음 값 초록). 개조 전에 없던 특성(튕김 0인 무기의 BOUNCE)은 0이 아니라 없음(NoneValue)에서 붙는다 — `BOUNCE - → 3`.
        // 줄 순서는 다른 카드와 같은 특성 순서다. 수치를 바꾸지 않는 개조(유도)는 바뀌는 줄이 없다 — 레벨 줄 아래 개조 줄이 말한다.
        static StatRows BuildModStats(WeaponData data, ModData mod, int level, float damageMultiplier, int maxAmmoBonus, Color gain)
        {
            var labels = new List<string>(ItemInfoText.WeaponStatLabels);
            var next = new List<string>(ItemInfoText.WeaponStatValues(data, level, damageMultiplier, maxAmmoBonus));
            var now = new List<string>();
            var tints = new List<Color>();
            for (int i = 0; i < labels.Count; i++)
            {
                now.Add(null);
                tints.Add(Color.white);
            }

            var beforeLabels = new List<string>();
            var beforeValues = new List<string>();
            ItemInfoText.WeaponTraitStats(data, null, level, maxAmmoBonus, beforeLabels, beforeValues);
            var afterLabels = new List<string>();
            var afterValues = new List<string>();
            ItemInfoText.WeaponTraitStats(data, mod, level, maxAmmoBonus, afterLabels, afterValues);
            for (int i = 0; i < afterLabels.Count; i++)
            {
                int j = beforeLabels.IndexOf(afterLabels[i]);
                string before = j >= 0 ? beforeValues[j] : NoneValue;
                bool changed = before != afterValues[i];
                labels.Add(afterLabels[i]);
                now.Add(changed ? before : null);
                next.Add(afterValues[i]);
                tints.Add(changed ? gain : Color.white);
            }
            return new StatRows { Labels = labels.ToArray(), Now = now.ToArray(), Next = next.ToArray(), Tints = tints.ToArray() };
        }

        // 수치가 없는 특성 — 표에 둘 값이 없어 표 아래 본문 줄(가운데 맞춤)이 된다 (자동 회수·당기는 폭발). 개조 이름은 여기 두지 않는다 — 레벨 줄 아래
        // 개조 줄(SubLine)이 말한다 (2026-10-12, 그전엔 레벨업 카드의 유도만 여기 흰 줄이었다).
        static string[] WeaponFlags(WeaponData data)
        {
            var flags = new List<string>();
            ItemInfoText.WeaponTraitFlags(data, flags);
            return flags.ToArray();
        }

        // 수정치 묶음을 한 줄씩 — 아이콘(PixelUiSkin.StatIcon)·라벨(`DAMAGE`)·값(`+10%`, 양수에도 부호). 유물의 선택 카드·보기 카드(1개분)와 캐릭터 카드(스탯 보너스)가
        // 같이 쓴다. owner는 예외 문구용.
        static void ModifierLines(PixelUiSkin skin, PlayerStatModifiers m, string owner, List<string> labels, List<string> values, List<Sprite> icons)
        {
            if (m.IsZero)
                throw new System.Exception($"[PickShoot] UpgradeCardPainter: {owner}의 수정치가 전부 0 — 효과 없음. 자산 확인.");
            foreach (var s in PlayerStatModifiers.Stats)
            {
                int v = m.Get(s);
                if (v == 0) continue;
                icons.Add(skin.StatIcon(s));
                labels.Add(PlayerStatModifiers.Label(s));
                values.Add(PlayerStatModifiers.FormatSigned(s, v));
            }
        }

        // 본문 줄 i 앞 아이콘의 폭 — 아이콘이 없으면 0.
        static int BodyIconWidth(CardContent c, int i, int iconSize)
        {
            if (c.BodyIcons == null) return 0;
            if (c.BodyIcons.Length != c.BodyLines.Length)
                throw new System.Exception($"[PickShoot] UpgradeCardPainter: '{c.Name}'의 BodyIcons({c.BodyIcons.Length})와 BodyLines({c.BodyLines.Length}) 길이가 다름.");
            return c.BodyIcons[i] != null ? iconSize : 0;
        }

        // 무기 카드의 설명 문단 — 무기 설명 뒤에 개조 설명(둘 다 선택, 지금 자산은 모두 비어 있다). 한 문단으로 이어 단어 단위로 접는다.
        static string JoinNotes(string first, string second)
        {
            if (string.IsNullOrEmpty(first)) return second;
            if (string.IsNullOrEmpty(second)) return first;
            return first + " " + second;
        }

        // 제목 뒤의 보너스 꼬리. 한 장이 레벨을 둘 이상 올리는 카드(아직 없다 — 앞으로의 보너스 카드)는 `NEW (+2)`·`UPGRADE (+3)`처럼 올리는 수를 괄호에 붙인다.
        // 획득 카드의 수는 시작 레벨이라 1이면 레벨업의 +1과 같은 보통 카드 — 붙이지 않는다 (2026-10-08).
        static string LevelTag(int levels)
        {
            return levels >= 2 ? $"(+{levels})" : null;
        }

        // 등급 줄 값의 글자색 (Relics §1). 커먼은 흰색 — 배지 시절(2026-10-08까지)엔 회색 패널로 무기 카드의 노란 NEW!·파란 LV UP과 갈랐다. 언커먼·레어는 그 단계에서 정한다 (Relics §6).
        static Color GradeTint(RelicGrade grade)
        {
            switch (grade)
            {
                case RelicGrade.Common: return Color.white;
                default:
                    throw new System.Exception($"[PickShoot] UpgradeCardPainter: 등급 {grade}의 글자색이 아직 정해지지 않음 (Relics §5 3·4단계).");
            }
        }

        // 스탯 줄 한 줄 문구의 폭: (라벨 + 공백) + (현재 값 + 공백 + 화살표 + 공백) + 다음 값. label이 null이면 앞 괄호가(유물 등급 줄),
        // now가 null이면 뒤 괄호가 없다(값 하나).
        static int RowWidth(PixelUiSkin skin, string label, string now, string next, int iconSize, int space, int textScale)
        {
            int w = PixelGui.MeasureText(skin.FontPlain, next, textScale);
            if (label != null) w += PixelGui.MeasureText(skin.FontPlain, label, textScale) + space;
            if (now != null) w += PixelGui.MeasureText(skin.FontPlain, now, textScale) + space + iconSize + space;
            return w;
        }

        // 본문 한 줄의 문구 — 값이 있으면 공백 하나 뒤에 붙는다 (폭 재기와 그리기가 같은 문구를 쓴다).
        static string BodyText(string line, string value)
        {
            return value != null ? line + " " + value : line;
        }

        // 본문 줄을 x부터 그릴 때 값을 놓을 자리 — BodyText를 한 문구로 그릴 때와 같은 자리(공백 뒤 글자 간격까지). 값만 색이 달라 따로 그린다.
        static int BodyValueX(PixelUiSkin skin, string line, int x, int textScale)
        {
            return x + PixelGui.MeasureText(skin.FontPlain, line + " ", textScale) + skin.FontPlain.letterSpacing * textScale;
        }

        // 스탯 줄 한 줄을 centerX 가운데에. 다음 값만 nextTint로 곱한다 — 얻는 값의 초록·유물 등급 색·개조 이름의 개조 색. 라벨·현재 값·화살표는 흰색.
        static void DrawRow(PixelUiSkin skin, string label, string now, string next, Color nextTint, int centerX, int y, int iconSize, int space, int textScale)
        {
            DrawRowAt(skin, label, now, next, nextTint, centerX - RowWidth(skin, label, now, next, iconSize, space, textScale) / 2, y, iconSize, space, textScale);
        }

        // 같은 줄을 왼쪽 끝 x부터 (가로 카드의 가운데 열·표).
        static void DrawRowAt(PixelUiSkin skin, string label, string now, string next, Color nextTint, int x, int y, int iconSize, int space, int textScale)
        {
            if (label != null)
            {
                PixelGui.DrawText(skin.FontPlain, label, x, y, textScale);
                x += PixelGui.MeasureText(skin.FontPlain, label, textScale) + space;
            }
            if (now != null)
            {
                PixelGui.DrawText(skin.FontPlain, now, x, y, textScale);
                x += PixelGui.MeasureText(skin.FontPlain, now, textScale) + space;
                // 위쪽 화살표 아이콘을 시계방향 90° 돌려 오른쪽 화살표로 쓴다.
                PixelGui.DrawSpriteRotated(skin.IconArrowUp, new Rect(x, y, iconSize, iconSize), 90f);
                x += iconSize + space;
            }
            var prevColor = GUI.color;
            GUI.color = prevColor * nextTint;
            PixelGui.DrawText(skin.FontPlain, next, x, y, textScale);
            GUI.color = prevColor;
        }
    }
}
