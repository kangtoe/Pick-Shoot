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
    // 적고 타일(배율 6) 밑에 이름(배율 3)을 세로로 쌓았다 — 그 배지 그리기는 일시정지 화면의 PAUSED 탭(PauseScreen.DrawBadge)에만 남았다.
    // 종류 색은 제목 글자색으로 — `NEW`(새 무기·유물)는 XP 팝업과 같은 노랑(PixelUiSkin.HighlightText), `UPGRADE`는 초록(PixelUiSkin.UpgradeText).
    // 색 패널·외곽선 색 띠로 두는 안도 목업했으나 패널은 카드 안에서 버튼처럼 보였고 띠는 머리가 번잡했다 (Recordings/card_header_mockup_2026-10-08*.png).
    // 한 장이 레벨을 둘 이상 올리는 카드(아직 없다)는 제목 뒤에 글자 배율 꼬리 `(+n)`이 붙고, 제목과 꼬리를 한 덩이로 가운데에 둔다 (LevelTag, UpgradeOption.Levels).
    // 전시 칸은 일시정지·보상 화면의 빌드 격자와 같은 정사각 타일(ItemTilePainter)이고 배지는 고른 뒤의 상태(`LV1`·`LV3`·`X2`) — 레벨 줄의 "2 → 3"과 맞춘다.
    // 스탯 줄은 한 줄 문구 `DMG 240 → 270`(라벨·현재 값·화살표·다음 값, 값 하나면 `BOUNCE 3`)을 가운데 맞춤으로 적는다 (2026-10-09). 그전엔 라벨은 왼쪽 여백,
    // 값은 화살표를 한 열에 맞춘 오른쪽 열의 표였다 — 세 카드의 값이 같은 열에 서 가로로 비교하기 쉬웠지만 라벨과 값 사이 빈칸이 넓었고, 가운데 맞춘 머리 묶음·설명
    // 줄과 정렬이 갈렸다. 한 줄 문구를 왼쪽에 두는 안은 카드 오른쪽이 비었고, 줄만 왼쪽·설명은 가운데인 안은 한 카드에 정렬이 섞였다
    // (Recordings/card_inline_rows_mockup_2026-10-09.png).
    // 스탯 줄은 DMG·AMMO 뒤로 그 무기의 수치 특성(`BURST 3`·`BOUNCE 5`·`PIERCE 1`·`BLAST 2.5`, ItemInfoText.WeaponTraitStats)이 같은 꼴로 이어지고,
    // 수치가 없는 특성(`AUTO RETRIEVE`·`PULLS IN`)은 표 아래 본문 줄 — 유물 카드의 효과 줄과 같이 가운데 맞춤 일반 글꼴로, 카드의 설명 줄이 모두 같은 꼴이다
    // (2026-10-07 표 아래 가운데 맞춤 아웃라인 → 같은 날 값 열 오른쪽 정렬 → 2026-10-08 라벨 열 왼쪽 정렬 → 2026-10-09 다시 가운데. 처음 가운데 둔 때는 글꼴이
    // 아웃라인이라 표와 결이 달라 튀었다 — 지금은 표와 같은 일반 글꼴이고 표와 본문 간격으로 떨어진다, Recordings/card_center_desc_mockup_2026-10-09.png).
    // 무기를 가르는 것이 카드에서 바로 읽히게 — 그전에는 호버 툴팁에만 있었다. 가장 긴 조합(특성 줄 + 수치 특성 한 줄 — 부메랑·볼텍스)에서 아래 여백이 32px —
    // 최소 여백과 같아 줄이 하나 더 늘면 넘친다 (7×8타일 세로 쌓기, 2026-10-09. 7×7 가로 머리일 때 64px). 넘치면 예외.
    // 타일에 마우스를 올리면 격자와 같은 툴팁(TooltipPainter)이 뜬다 — 카드에 없는 탄속·넉백은 거기서 보인다 (2026-10-06).
    // 카드 호버는 테두리를 바깥으로 HoverOutlineTexels 넓히기 — 카드보다 그만큼 큰 흰 판 모양(PixelGui.FillPlateShape)을 패널 밑에 깔아 패널의 흰 바깥 선
    // 1텍셀이 2텍셀이 된다. 패널 모서리도 판처럼 1텍셀 깎여 있어 그 귀가 흰색으로 메워지고 바깥 귀만 깎인다. 리롤 버튼도 같은 값 (2026-10-08).
    // 그전엔 4텍셀 들림(띠→카드 간격 40px일 때 배지가 부제를 가림)·선택 테두리·비정수 배율 키우기(텍셀이 갈려 깨져 보이고 아래 빌드 줄 제목을 덮음)를 거쳤다.
    // 유물 카드(Relics §2)도 같은 패널 구성이다 — 제목은 `NEW`, 전시 칸에 유물 아이콘(배지 `X n`이 고른 뒤 보유 수), 무기 카드의 레벨 줄 자리에
    // 등급 이름 한 줄(`COMMON` — 라벨 없이 값만, 등급 색)뿐이고, 본문 줄에 그 유물이 더하는 수정치를 `GUN DAMAGE +10%`처럼 한 줄로 적는다
    // (2026-10-07, 등급 줄은 2026-10-09 — 처음엔 `GRADE COMMON`이었다가 같은 날 라벨을 뺐다).
    // 등급은 2026-10-08까지 제목 자리였다. 그 사이 이름 밑 부제 줄(`COMMON`·무기는 `LEVEL +1`)로 옮기고 레벨 줄을 빼는 안, 부제의 외곽선·이름 음영을 끄는 안을
    // 목업했으나 레벨 줄로 돌아왔다 (Recordings/card_subtitle_mockup_2026-10-08*.png·card_text_*_mockup_2026-10-09*.png·card_grade_row_mockup_2026-10-09.png).
    public static class UpgradeCardPainter
    {
        const int CardWidthTiles = 7;
        // 10 → 9 (2026-10-08, 표가 짧은 카드의 아래가 비어 보임) → 7 (같은 날 머리 가로 배치 — 타일 밑에 두던 이름이 타일 오른쪽으로 가고 타일도 배율 6→4가 되어
        // 머리가 240px에서 120px로, 위 여백도 배지 깊이를 빼며 80에서 40px로) → 8 (2026-10-09 세로 쌓기 — 머리 216px). 가장 긴 카드(볼텍스·부메랑)의 아래 여백 32px.
        const int CardHeightTiles = 8;
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
        // 첫 줄(무기 카드)의 라벨 — 값은 "n → n+1". 슬롯 번호(`SLOT 3`)는 2026-10-07에 뺐다 — 자루를 가리키는 번호였지만 전시 그림·레벨로 충분하고,
        // 레벨 값의 `LV` 접두도 함께 걷어 값 열이 좁아졌다. 유물 카드는 이 자리가 라벨 없는 등급 이름이다 (2026-10-09. 그전엔 줄이 없었고,
        // 2026-10-07엔 보유 수 `OWNED n → n+1` 줄을 뺐다 — 고른 뒤 보유 수는 전시 배지 `X n`이 말한다).
        const string LevelLabel = "LEVEL";
        // 줄이 없는 칸용 빈 묶음 (프레임마다 새로 만들지 않는다).
        static readonly string[] NoLines = new string[0];
        static readonly List<string> EmptyLines = new List<string>();

        // 글꼴에 있는 문자(A-Z 0-9 % + - ! ( ) / . 공백)만 써야 한다. 없는 문자는 PixelGui가 예외로 알린다.
        struct CardContent
        {
            // 머리 제목(종류)과 그 글자색 — `NEW`는 노랑, `UPGRADE`는 초록. TitleTag는 제목 뒤에 작게 붙는 보너스 꼬리(`(+2)`), 없으면 null.
            public string Title;
            public string TitleTag;
            public Color TitleTint;
            public Sprite ShowcaseSprite;
            public string ShowcaseBadge;
            public string Name;
            // 전시 타일 밑 첫 줄 — 모든 카드에 있다. 무기는 레벨(`LEVEL 2 -> 3`), 유물은 등급(`COMMON` — RowLabel이 null이면 라벨 없이 값만).
            // RowNow는 스탯 줄과 같은 규칙 — null이면 비교 없이 값 하나 (획득 카드·유물). RowTint는 값의 글자색 — 등급 색(Relics §5), 무기는 흰색.
            public string RowLabel;
            public string RowNow;
            public string RowNext;
            public Color RowTint;
            public string[] StatLabels;
            // StatNow[i]가 null이면 그 줄은 값 하나 — "현재 → 다음"이 뜻 없는 줄(레벨로 바뀌지 않는 특성)이라 화살표 없이 고른 뒤의 값만 적는다.
            public string[] StatNow;
            public string[] StatNext;
            // 표 아래 본문. BodyLines는 한 항목이 한 줄(가운데 맞춤 일반 글꼴 — 무기의 수치 없는 특성·유물의 수정치), Note는 단어 단위로 접는 문단.
            public string[] BodyLines;
            public string Note;
        }

        public static Vector2Int CardSize(PixelUiSkin skin, int scale)
        {
            int tile = skin.TileSize * scale;
            return new Vector2Int(CardWidthTiles * tile, CardHeightTiles * tile);
        }

        // 반환: 전시 타일의 영역 — UpgradeManager가 툴팁 호버 판정에 쓴다.
        public static Rect Draw(PixelUiSkin skin, UpgradeOption option, PlayerWeapons weapons, PlayerStats stats, Rect rect, int scale, int textScale, bool hovered)
        {
            var c = Build(skin, option, weapons, stats);
            int pad = SidePaddingTexels * scale;
            int gap = RowGapTexels * scale;
            int bodyGap = BodyGapTexels * scale;
            int lineH = skin.FontPlain.lineHeight * textScale;
            int space = skin.FontPlain.spaceWidth * textScale;
            int nameH = skin.FontOutlined.lineHeight * NameScale;

            int x0 = (int)rect.x + pad;
            int x1 = (int)rect.xMax - pad;
            int centerX = (int)rect.center.x;
            int innerW = x1 - x0;

            // 머리: 제목(배율 3, 꼬리와 한 덩이) → 이름(아웃라인 배율 2, 한 줄) → 전시 타일을 각각 카드 가운데에 쌓는다. 높이는 모든 카드가 같아 첫 줄 자리도 같다.
            int showcaseH = ItemTilePainter.Size(ShowcaseTileScale);
            int titleH = skin.FontPlain.lineHeight * TitleScale;
            int titleTextW = PixelGui.MeasureText(skin.FontPlain, c.Title, TitleScale);
            int titleW = titleTextW;
            if (c.TitleTag != null) titleW += space + PixelGui.MeasureText(skin.FontPlain, c.TitleTag, textScale);
            int nameW = PixelGui.MeasureText(skin.FontOutlined, c.Name, NameScale);
            int titleNameGap = TitleNameGapTexels * scale;
            int nameShowcaseGap = NameShowcaseGapTexels * scale;
            int headerH = titleH + titleNameGap + nameH + nameShowcaseGap + showcaseH;

            // 화살표는 글자 배율의 아이콘 한 칸.
            int iconSize = skin.TileSize * textScale;

            // 크기가 고정이라 내용이 카드를 넘칠 수 있다. 넘치면 어긋난 채 그리지 않고 예외로 알린다.
            // 제목·이름과 첫 줄·스탯·본문 줄은 카드 안쪽 폭에 — 첫 줄·스탯 줄은 한 줄 문구 전체의 폭으로 잰다.
            RequireFits(titleW, innerW, $"{c.Title} {c.TitleTag}", "카드 안쪽(제목)");
            RequireFits(nameW, innerW, c.Name, "카드 안쪽(이름)");
            RequireFits(RowWidth(skin, c.RowLabel, c.RowNow, c.RowNext, iconSize, space, textScale), innerW, $"{c.RowLabel} {c.RowNow} > {c.RowNext}", "카드 안쪽");
            for (int i = 0; i < c.StatLabels.Length; i++)
                RequireFits(RowWidth(skin, c.StatLabels[i], c.StatNow[i], c.StatNext[i], iconSize, space, textScale), innerW, $"{c.StatLabels[i]} {c.StatNow[i]} > {c.StatNext[i]}", "카드 안쪽");

            // 본문 줄: 한 항목이 한 줄(줄 안에서 나누지 않는다 — 항목 하나가 폭을 넘으면 문구를 줄여야 한다), 설명은 단어 단위로 접는다.
            foreach (var line in c.BodyLines)
                RequireFits(PixelGui.MeasureText(skin.FontPlain, line, textScale), innerW, line, "카드 안쪽");
            var noteLines = string.IsNullOrEmpty(c.Note)
                ? EmptyLines
                : PixelGui.WrapText(skin.FontPlain, c.Note, textScale, innerW);
            int bodyLines = c.BodyLines.Length + noteLines.Count;

            int topMargin = TopMarginTexels * scale;
            int bottomMin = BottomMinTexels * scale;
            int contentH = headerH + gap + lineH + gap + c.StatLabels.Length * lineH;
            if (bodyLines > 0) contentH += bodyGap + bodyLines * lineH;
            int innerH = (int)rect.height - topMargin - bottomMin;
            if (contentH > innerH)
                throw new System.Exception($"[PickShoot] UpgradeCardPainter: 카드 내용 높이 {contentH}px가 카드 안쪽 높이 {innerH}px(위 여백 {topMargin}·아래 최소 여백 {bottomMin} 제외)를 넘음 (본문 {bodyLines}줄). 본문 문구를 줄이거나 간격을 줄일 것 — 카드를 키우려면 보상 화면 블록(UpgradeManager)이 캔버스 세로에 드는지 볼 것.");

            // 호버면 카드보다 한 텍셀 큰 흰 판 모양을 먼저 깔아 테두리를 바깥으로 넓힌다.
            if (hovered)
            {
                int o = HoverOutlineTexels * scale;
                PixelGui.FillPlateShape(new Rect(rect.x - o, rect.y - o, rect.width + 2 * o, rect.height + 2 * o), scale, Color.white);
            }
            // 획득·레벨업·유물 카드는 같은 패널(일시정지 배너와 같은 파란 패널).
            PixelGui.DrawNineSlice(skin.PanelBlue, rect, skin.TileSize, scale);

            // 위 여백은 고정, 남는 공간은 아래 여백. 어떤 종류든 내용 위치가 같다.
            int y = (int)rect.y + topMargin;

            // 머리 — 제목, 이름, 전시 타일(그림과 고른 뒤의 상태 배지)을 차례로 가운데에.
            // 제목 글자색은 GUI.color로 곱한다 — 글리프의 흰 획이 그 색이 되고 어두운 상자도 함께 어두워진다 (XP 팝업과 같은 방식). 꼬리도 같은 색.
            int titleX = centerX - titleW / 2;
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
            y += titleH + titleNameGap;
            PixelGui.DrawTextCentered(skin.FontOutlined, c.Name, centerX, y, NameScale);
            y += nameH + nameShowcaseGap;
            int showcaseX = centerX - showcaseH / 2;
            var showcaseTile = new Rect(showcaseX, y, showcaseH, showcaseH);
            ItemTilePainter.Draw(skin, c.ShowcaseSprite, c.ShowcaseBadge, showcaseX, y, ShowcaseTileScale);
            y += showcaseH + gap;

            DrawRow(skin, c.RowLabel, c.RowNow, c.RowNext, c.RowTint, centerX, y, iconSize, space, textScale);
            y += lineH + gap;

            for (int i = 0; i < c.StatLabels.Length; i++)
            {
                DrawRow(skin, c.StatLabels[i], c.StatNow[i], c.StatNext[i], Color.white, centerX, y, iconSize, space, textScale);
                y += lineH;
            }

            // 본문도 가운데 맞춤 — 스탯 줄과는 본문 간격으로 덩어리가 갈린다. 무기의 수치 없는 특성과 유물 카드의 수정치가 여기 온다.
            // 글꼴은 표의 값과 같은 일반 글꼴 (2026-10-08, 그전엔 아웃라인 — 카드 안에서 이름만 아웃라인이게).
            if (bodyLines > 0) y += bodyGap;
            for (int i = 0; i < c.BodyLines.Length; i++)
            {
                PixelGui.DrawTextCentered(skin.FontPlain, c.BodyLines[i], centerX, y, textScale);
                y += lineH;
            }
            for (int i = 0; i < noteLines.Count; i++)
            {
                PixelGui.DrawTextCentered(skin.FontPlain, noteLines[i], centerX, y, textScale);
                y += lineH;
            }

            return showcaseTile;
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
                    var acquired = BuildWeaponStats(d, 0, start, stats.DamageMultiplier);
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
                        RowTint = Color.white,
                        StatLabels = acquired.Labels,
                        StatNow = acquired.Now,
                        StatNext = acquired.Next,
                        BodyLines = WeaponFlags(d),
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
                    var levelled = BuildWeaponStats(w.Data, w.Level, next, stats.DamageMultiplier);
                    return new CardContent
                    {
                        Title = LevelUpTitle,
                        TitleTag = LevelTag(option.Levels),
                        TitleTint = skin.UpgradeText,
                        ShowcaseSprite = w.Data.WeaponSprite,
                        ShowcaseBadge = ItemInfoText.WeaponBadge(next),
                        Name = w.Data.DisplayName.ToUpperInvariant(),
                        RowLabel = LevelLabel,
                        RowNow = w.Level.ToString(),
                        RowNext = next.ToString(),
                        RowTint = Color.white,
                        StatLabels = levelled.Labels,
                        StatNow = levelled.Now,
                        StatNext = levelled.Next,
                        BodyLines = WeaponFlags(w.Data),
                        Note = w.Data.Description,
                    };
                }

                case UpgradeKind.Relic:
                {
                    var d = option.RelicData;
                    if (d.Modifiers.IsZero)
                        throw new System.Exception($"[PickShoot] UpgradeCardPainter: 유물 '{d.DisplayName}'의 수정치가 전부 0 — 효과 없는 유물. 자산 확인.");
                    // 유물 카드의 표는 등급 한 줄뿐이다 — 그 유물이 더하는 수정치를 `GUN DAMAGE +10%` 한 줄로 본문(가운데 맞춤, 일반 글꼴)에 둔다 (2026-10-07).
                    // 라벨과 값을 좌우 끝으로 갈라 놓으면(표) 한 줄짜리 효과를 읽는 데 눈이 빈칸을 건너야 한다. 값은 "지금 총계 → 획득 뒤 총계"였다가
                    // 수정치 하나로 바꿨다 — 카드가 답할 것은 "이걸 고르면 무엇이 붙나"이고, 총계는 일시정지 화면이 보일 자리다 (Relics §7).
                    // 무기 카드의 수치 없는 특성 줄도 2026-10-09부터 같은 본문 줄이다 (그 사이 표의 라벨 열에 붙어 있었다).
                    var effects = new List<string>();
                    foreach (var s in PlayerStatModifiers.Stats)
                    {
                        int v = d.Modifiers.Get(s);
                        if (v == 0) continue;
                        effects.Add($"{PlayerStatModifiers.Label(s)} {PlayerStatModifiers.FormatSigned(s, v)}");
                    }
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
                        BodyLines = effects.ToArray(),
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
        }

        // 무기 카드의 표 — DMG·AMMO 뒤에 그 무기의 수치 특성(점사·튕김·관통·폭발)이 같은 줄로 이어진다 (ItemInfoText.WeaponTraitStats).
        // nowLevel이 0이면 획득 카드다 — 아직 없는 자루라 비교할 "현재"가 없고, 모든 줄이 값 하나다 (2026-10-07. 그전에는 "현재" 열에 `-`를 두어
        // 획득·레벨업 카드의 줄 구성을 맞췄는데, 읽는 사람에게는 뜻 없는 열이었다).
        // 레벨업 카드에서도 레벨로 바뀌지 않는 특성은 값 하나다 — 같은 수를 화살표 양쪽에 두면 그것도 오르는 값으로 읽힌다.
        static StatRows BuildWeaponStats(WeaponData data, int nowLevel, int nextLevel, float damageMultiplier)
        {
            var labels = new List<string>(ItemInfoText.WeaponStatLabels);
            var next = new List<string>(ItemInfoText.WeaponStatValues(data, nextLevel, damageMultiplier));
            var now = new List<string>();
            if (nowLevel > 0) now.AddRange(ItemInfoText.WeaponStatValues(data, nowLevel, damageMultiplier));
            else for (int i = 0; i < labels.Count; i++) now.Add(null);

            var traitLabels = new List<string>();
            var traitValues = new List<string>();
            ItemInfoText.WeaponTraitStats(data, nextLevel, traitLabels, traitValues);
            var nowLabels = new List<string>();
            var nowValues = new List<string>();
            if (nowLevel > 0) ItemInfoText.WeaponTraitStats(data, nowLevel, nowLabels, nowValues);
            for (int i = 0; i < traitLabels.Count; i++)
            {
                labels.Add(traitLabels[i]);
                next.Add(traitValues[i]);
                int j = nowLabels.IndexOf(traitLabels[i]);
                now.Add(j >= 0 && nowValues[j] != traitValues[i] ? nowValues[j] : null);
            }
            return new StatRows { Labels = labels.ToArray(), Now = now.ToArray(), Next = next.ToArray() };
        }

        // 수치가 없는 특성 — 표에 둘 값이 없어 표 아래 본문 줄(가운데 맞춤)이 된다 (자동 회수·당기는 폭발).
        static string[] WeaponFlags(WeaponData data)
        {
            var flags = new List<string>();
            ItemInfoText.WeaponTraitFlags(data, flags);
            return flags.ToArray();
        }

        // 제목 뒤의 보너스 꼬리. 한 장이 레벨을 둘 이상 올리는 카드(아직 없다 — 앞으로의 보너스 카드)는 `NEW (+2)`·`UPGRADE (+3)`처럼 올리는 수를 괄호에 붙인다.
        // 획득 카드의 수는 시작 레벨이라 1이면 레벨업의 +1과 같은 보통 카드 — 붙이지 않는다 (2026-10-08).
        static string LevelTag(int levels)
        {
            return levels >= 2 ? $"(+{levels})" : null;
        }

        // 등급 줄 값의 글자색 (Relics §1). 커먼은 흰색 — 배지 시절(2026-10-08까지)엔 회색 패널로 무기 카드의 노란 NEW!·파란 LV UP과 갈랐다. 레어·유니크는 그 단계에서 정한다 (Relics §5).
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

        // 스탯 줄 한 줄을 centerX 가운데에. 값(현재·화살표·다음)은 valueTint로 곱한다 — 유물 등급 줄의 등급 색. 라벨은 흰색.
        static void DrawRow(PixelUiSkin skin, string label, string now, string next, Color valueTint, int centerX, int y, int iconSize, int space, int textScale)
        {
            int x = centerX - RowWidth(skin, label, now, next, iconSize, space, textScale) / 2;
            if (label != null)
            {
                PixelGui.DrawText(skin.FontPlain, label, x, y, textScale);
                x += PixelGui.MeasureText(skin.FontPlain, label, textScale) + space;
            }
            var prevColor = GUI.color;
            GUI.color = prevColor * valueTint;
            if (now != null)
            {
                PixelGui.DrawText(skin.FontPlain, now, x, y, textScale);
                x += PixelGui.MeasureText(skin.FontPlain, now, textScale) + space;
                // 위쪽 화살표 아이콘을 시계방향 90° 돌려 오른쪽 화살표로 쓴다.
                PixelGui.DrawSpriteRotated(skin.IconArrowUp, new Rect(x, y, iconSize, iconSize), 90f);
                x += iconSize + space;
            }
            PixelGui.DrawText(skin.FontPlain, next, x, y, textScale);
            GUI.color = prevColor;
        }
    }
}
