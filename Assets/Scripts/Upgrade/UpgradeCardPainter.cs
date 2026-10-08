using System.Collections.Generic;
using UnityEngine;

namespace PickShoot
{
    // 업그레이드 카드 한 장의 IMGUI 그리기 (Kenney Interface 시트 기반 픽셀 UI).
    // 레이아웃은 텍셀 단위로 정의하고 scale(패널·아이콘)과 textScale(글자)을 곱해 픽셀로 바꾼다.
    // 카드 크기는 타일 단위로 고정이며 내용에 맞춰 늘리지 않는다 — 내용이 카드를 넘치면 예외로 알린다.
    // 머리는 브로타토식 가로 배치 (2026-10-08): 왼쪽에 전시 타일, 그 오른쪽에 종류 제목(일반 글꼴 배율 3 — 획득 `NEW`·레벨업 `LV UP`·유물은 등급 `COMMON`)과 이름(아웃라인 글꼴,
    // 긴 이름은 두 줄). 타일과 글자 칸 묶음은 카드 가운데에 둔다 (같은 날 왼쪽 붙임에서 — 오른쪽만 비어 보였고, 제목은 글자 배율 2에서 3으로 키웠다).
    // 그전에는 종류를 카드 위쪽 끝에 걸치는 배지(노란 NEW! / 카드와 같은 파란 LV UP)로 적고 타일 밑에 이름을 가운데 맞춰 세로로 쌓았다 — 그 배지 그리기는 일시정지 화면의
    // PAUSED 탭(PauseScreen.DrawBadge)에만 남았다. 종류 색은 제목 글자색으로 — 새 무기만 XP 팝업과 같은 노랑(PixelUiSkin.HighlightText), 레벨업·커먼은 흰색.
    // 색 패널·외곽선 색 띠로 두는 안도 목업했으나 패널은 카드 안에서 버튼처럼 보였고 띠는 머리가 번잡했다 (Recordings/card_header_mockup_2026-10-08*.png).
    // 한 장이 레벨을 둘 이상 올리는 카드(아직 없다)는 제목 뒤에 글자 배율 꼬리 `(+n)`이 붙는다 (LevelTag, UpgradeOption.Levels).
    // 머리를 가로로 눕히며 전시 타일은 배율 6(168px)에서 일시정지 격자와 같은 4(112px)로, 이름은 배율 3에서 2로 — 글자 칸은 1080p에서 200px(NameWrapTexels)이라
    // `BOOMERANG`(200px)까지 한 줄, 두 낱말 유물 이름은 두 줄. 머리 세로가 240 → 120px로 줄어 카드도 9타일에서 7타일로 낮췄다.
    // 전시 칸은 일시정지·보상 화면의 빌드 격자와 같은 정사각 타일(ItemTilePainter)이고 배지는 고른 뒤의 상태(`LV1`·`LV3`·`X2`) — 레벨 줄의 "2 → 3"과 맞춘다.
    // 표에는 DMG·AMMO 뒤로 그 무기의 수치 특성(`BURST 3`·`BOUNCE 5`·`PIERCE 1`·`BLAST 2.5`, ItemInfoText.WeaponTraitStats)이 같은 줄로 이어지고,
    // 수치가 없는 특성(`AUTO RETRIEVE`·`PULLS IN`)은 표의 마지막 줄들로 — 값 없는 라벨처럼 라벨 열(왼쪽)에 일반 글꼴 (2026-10-07 표 아래 가운데 맞춤 아웃라인
    // → 같은 날 값 열 오른쪽 정렬 → 2026-10-08 라벨 열 왼쪽 정렬. 표와 결이 달라 튀던 것을 표에 붙였고, 라벨 자리가 읽는 순서에 맞다). 무기를 가르는 것이 카드에서 바로
    // 읽히게 — 그전에는 호버 툴팁에만 있었다. 가장 긴 조합(특성 줄 + 수치 특성 한 줄 — 부메랑·볼텍스)에서 아래 여백이 80px 남는다 (7×7타일, 2026-10-08). 넘치면 예외.
    // 타일에 마우스를 올리면 격자와 같은 툴팁(TooltipPainter)이 뜬다 — 카드에 없는 탄속·넉백은 거기서 보인다 (2026-10-06).
    // 카드 호버는 테두리를 바깥으로 HoverOutlineTexels 넓히기 — 카드보다 그만큼 큰 흰 판 모양(PixelGui.FillPlateShape)을 패널 밑에 깔아 패널의 흰 바깥 선
    // 1텍셀이 2텍셀이 된다. 패널 모서리도 판처럼 1텍셀 깎여 있어 그 귀가 흰색으로 메워지고 바깥 귀만 깎인다. 리롤 버튼도 같은 값 (2026-10-08).
    // 그전엔 4텍셀 들림(띠→카드 간격 40px일 때 배지가 부제를 가림)·선택 테두리·비정수 배율 키우기(텍셀이 갈려 깨져 보이고 아래 빌드 줄 제목을 덮음)를 거쳤다.
    // 유물 카드(Relics §2)도 같은 패널 구성이다 — 제목은 등급(COMMON), 전시 칸에 유물 아이콘(배지 `X n`이 고른 뒤 보유 수), 표는 아예 없고,
    // 본문 줄에 그 유물이 더하는 수정치를 `GUN DAMAGE +10%`처럼 한 줄로 적는다 (2026-10-07).
    public static class UpgradeCardPainter
    {
        const int CardWidthTiles = 7;
        // 10 → 9 (2026-10-08, 표가 짧은 카드의 아래가 비어 보임) → 7 (같은 날 머리 가로 배치 — 타일 밑에 두던 이름이 타일 오른쪽으로 가고 타일도 배율 6→4가 되어
        // 머리가 240px에서 120px로, 위 여백도 배지 깊이를 빼며 80에서 40px로). 가장 긴 카드(볼텍스·부메랑)의 아래 여백 80px.
        const int CardHeightTiles = 7;
        const int SidePaddingTexels = 10; // 좌우 여백 (패널 테두리 → 글자). 산탄총 고레벨의 "256X3" 값까지 7타일 안에 들어가는 최대치.
        const int TopMarginTexels = 10;   // 위 여백 — 좌우와 같다. 배지가 카드 위 끝에 걸치던 때는 배지 깊이 16 + 4였다.
        const int BottomMinTexels = 8;    // 넘침 검사용 최소 아래 여백. 실제 아래 여백은 남는 만큼.
        const int RowGapTexels = 6;       // 머리→레벨 줄, 레벨 줄→스탯 줄 (2026-10-08 카드를 9타일로 낮추며 8에서)
        const int BodyGapTexels = 4;      // 스탯 표→본문 줄(특성·설명)
        // 머리 안: 전시 타일 → 글자 칸 (패널 텍셀), 제목 줄 → 이름 줄. 타일과 글자 칸을 합친 묶음을 카드 안쪽 폭 가운데에 둔다 — 왼쪽 여백에 붙이면 오른쪽만 비어 보였다.
        const int HeaderGapTexels = 4;
        const int HeaderTextGapTexels = 2;
        // 전시 타일의 배율. 28텍셀 타일 × 4 = 112px — 일시정지 격자의 타일과 같다. 6(168px)이면 글자 칸이 184px라 제목 배율 3·이름 200px이 들어가지 않는다.
        const int ShowcaseTileScale = 4;
        // 호버 때 테두리를 바깥으로 넓히는 두께 (패널 텍셀). 리롤 버튼(UpgradeManager)도 같은 값.
        public const int HoverOutlineTexels = 1;
        // 종류 제목의 일반 글꼴 배율 — 이름(아웃라인 배율 2)보다 한 단계 커 종류가 먼저 읽힌다 (2026-10-08, 글자 배율 2에서 키움). 보너스 꼬리 `(+n)`은 글자 배율 그대로
        // 제목 뒤에 글리프 상자 아래 변을 맞춰 붙인다 — 배율 3이면 `LV UP (+2)`가 285px로 글자 칸을 넘는다.
        const int TitleScale = 3;
        // 이름의 아웃라인 글꼴 배율. 제목·표와 글꼴로 가른다 — 배율 3이면 `BOOMERANG`이 300px로 한 줄에 들지 않는다.
        const int NameScale = 2;
        // 이름은 이 폭(패널 텍셀, 200px)에서 낱말 단위로 접고 최대 두 줄. 글자 칸 전체(240px)로 두면 아무 이름도 접히지 않아 묶음이 왼쪽에 붙고, 200은 가장 긴 한 낱말
        // `BOOMERANG`·`REGEN UNIT`(200px)이 한 줄에 드는 최소라 두 낱말 유물 이름(`ARMOR PLATE` 234px)만 접힌다. 한 낱말이 넘으면 PixelGui.WrapText가 예외.
        const int NameWrapTexels = 50;
        const int NameMaxLines = 2;
        // 종류 제목 — 획득 `NEW`, 레벨업 `LV UP`. 느낌표는 뺐다 (배지 때는 `NEW!`) — 글자 제목에서는 색이 새 무기를 가르고, `(+n)` 앞에서 어색하다. 유물은 등급 이름.
        const string NewTitle = "NEW";
        const string LevelUpTitle = "LV UP";
        // 표 첫 줄(무기 카드)의 라벨 — 값은 "n → n+1". 슬롯 번호(`SLOT 3`)는 2026-10-07에 뺐다 — 자루를 가리키는 번호였지만 전시 그림·레벨로 충분하고,
        // 레벨 값의 `LV` 접두도 함께 걷어 값 열이 좁아졌다. 유물 카드는 이 줄 자체가 없다 (같은 날 `OWNED n → n+1`도 뺐다).
        const string LevelLabel = "LEVEL";
        // 줄이 없는 칸용 빈 묶음 (프레임마다 새로 만들지 않는다).
        static readonly string[] NoLines = new string[0];
        static readonly List<string> EmptyLines = new List<string>();

        // 글꼴에 있는 문자(A-Z 0-9 % + - ! ( ) / . 공백)만 써야 한다. 없는 문자는 PixelGui가 예외로 알린다.
        struct CardContent
        {
            // 머리 제목(종류·등급)과 그 글자색 — 새 무기만 강조색, 나머지는 흰색. TitleTag는 제목 뒤에 작게 붙는 보너스 꼬리(`(+2)`), 없으면 null.
            public string Title;
            public string TitleTag;
            public Color TitleTint;
            public Sprite ShowcaseSprite;
            public string ShowcaseBadge;
            public string Name;
            // 표 첫 줄 — 무기는 레벨(`LEVEL 2 -> 3`). null이면 줄을 두지 않는다 (유물 카드: 보유 수는 전시 배지 `X n`이 말한다).
            // LevelNow는 스탯 줄과 같은 규칙 — null이면 비교 없이 값 하나 (획득 카드).
            public string RowLabel;
            public string LevelNow;
            public string LevelNext;
            public string[] StatLabels;
            // StatNow[i]가 null이면 그 줄은 값 하나 — "현재 → 다음"이 뜻 없는 줄(레벨로 바뀌지 않는 특성)이라 화살표 없이 "다음" 열에만 적는다.
            public string[] StatNow;
            public string[] StatNext;
            // 표 끝의 특성 줄 — 값 없는 라벨처럼 라벨 열에 일반 글꼴, 한 항목이 한 줄 (무기의 수치 없는 특성). 접지 않는다.
            public string[] TraitLines;
            // 표 아래 본문. BodyLines는 한 항목이 한 줄(가운데 맞춤 일반 글꼴 — 유물의 수정치), Note는 단어 단위로 접는 문단.
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

            // 머리: 왼쪽 전시 타일, 오른쪽 글자 칸에 제목(배율 3)과 이름(최대 두 줄). 글자 칸 폭은 이름 접힘 폭이 기본이고 제목이 더 넓으면 제목 폭 — 타일 + 간격 + 글자 칸
            // 묶음을 카드 안쪽 가운데에 둔다 (보통 양옆 20px). 머리 높이는 이름이 두 줄일 때로 고정해 표의 시작 줄이 카드마다 같다 — 타일(112px)보다 글자(120px)가 크다.
            int showcaseH = ItemTilePainter.Size(ShowcaseTileScale);
            int titleH = skin.FontPlain.lineHeight * TitleScale;
            int titleW = PixelGui.MeasureText(skin.FontPlain, c.Title, TitleScale);
            if (c.TitleTag != null) titleW += space + PixelGui.MeasureText(skin.FontPlain, c.TitleTag, textScale);
            int nameWrapW = NameWrapTexels * scale;
            var nameLines = PixelGui.WrapText(skin.FontOutlined, c.Name, NameScale, nameWrapW);
            if (nameLines.Count > NameMaxLines)
                throw new System.Exception($"[PickShoot] UpgradeCardPainter: 이름 '{c.Name}'이 {nameWrapW}px 폭에서 {nameLines.Count}줄로 접힘 — 최대 {NameMaxLines}줄. 이름을 줄이거나 NameWrapTexels를 늘릴 것.");
            int headerTextGap = HeaderTextGapTexels * scale;
            int headerTextW = Mathf.Max(nameWrapW, titleW);
            int headerW = showcaseH + HeaderGapTexels * scale + headerTextW;
            int headerTextH = titleH + headerTextGap + NameMaxLines * nameH;
            int headerH = Mathf.Max(showcaseH, headerTextH);

            // 값 열은 화살표를 한 열에 맞춘다 — 다음 값은 화살표 오른쪽에 왼쪽 정렬, 현재 값은 화살표 왼쪽에 오른쪽 정렬.
            // 화살표 열은 가장 넓은 "다음" 값이 오른쪽 여백 안에 들어가는 자리. 항목 이름 열은 왼쪽 여백에 따로 맞춘다.
            int iconSize = skin.TileSize * textScale;
            int nextColW = c.RowLabel != null ? PixelGui.MeasureText(skin.FontPlain, c.LevelNext, textScale) : 0;
            for (int i = 0; i < c.StatNext.Length; i++)
                nextColW = Mathf.Max(nextColW, PixelGui.MeasureText(skin.FontPlain, c.StatNext[i], textScale));
            int arrowX = x1 - nextColW - space - iconSize;
            int valueTail = space + iconSize + space + nextColW;   // 현재 값 뒤: 공백·화살표·공백·다음 값 열

            // 크기가 고정이라 내용이 카드를 넘칠 수 있다. 넘치면 어긋난 채 그리지 않고 예외로 알린다.
            // 머리 묶음(타일 + 글자 칸)과 표·본문은 카드 안쪽 폭에. 항목 이름과 현재 값 사이는 최소 공백 하나.
            RequireFits(headerW, innerW, $"{c.Title} / {c.Name}", "카드 안쪽(머리 묶음)");
            if (c.RowLabel != null)
                RequireFits(PixelGui.MeasureText(skin.FontPlain, c.RowLabel, textScale) + space
                            + (c.LevelNow == null ? nextColW : PixelGui.MeasureText(skin.FontPlain, c.LevelNow, textScale) + valueTail),
                            innerW, $"{c.RowLabel} {c.LevelNow} > {c.LevelNext}", "카드 안쪽");
            for (int i = 0; i < c.StatLabels.Length; i++)
            {
                if (c.StatNow[i] == null)
                    RequireFits(PixelGui.MeasureText(skin.FontPlain, c.StatLabels[i], textScale) + space + nextColW, innerW, $"{c.StatLabels[i]} {c.StatNext[i]}", "카드 안쪽");
                else
                    RequireFits(PixelGui.MeasureText(skin.FontPlain, c.StatLabels[i], textScale) + space
                                + PixelGui.MeasureText(skin.FontPlain, c.StatNow[i], textScale) + valueTail, innerW, $"{c.StatLabels[i]} {c.StatNow[i]} > {c.StatNext[i]}", "카드 안쪽");
            }

            // 특성·본문 줄: 한 항목이 한 줄(줄 안에서 나누지 않는다 — 항목 하나가 폭을 넘으면 문구를 줄여야 한다), 설명은 단어 단위로 접는다.
            foreach (var line in c.TraitLines)
                RequireFits(PixelGui.MeasureText(skin.FontPlain, line, textScale), innerW, line, "카드 안쪽");
            foreach (var line in c.BodyLines)
                RequireFits(PixelGui.MeasureText(skin.FontPlain, line, textScale), innerW, line, "카드 안쪽");
            var noteLines = string.IsNullOrEmpty(c.Note)
                ? EmptyLines
                : PixelGui.WrapText(skin.FontPlain, c.Note, textScale, innerW);
            int bodyLines = c.BodyLines.Length + noteLines.Count;

            int topMargin = TopMarginTexels * scale;
            int bottomMin = BottomMinTexels * scale;
            int contentH = headerH + gap + (c.StatLabels.Length + c.TraitLines.Length) * lineH;
            if (c.RowLabel != null) contentH += lineH + gap;
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

            // 머리 — 묶음을 가운데에 놓고, 타일(그림과 고른 뒤의 상태 배지)과 글자 칸은 위 변을 맞춘다. 이름이 한 줄이면 둘째 줄 자리는 비워 둔다 (제목 자리가 카드마다 같게).
            int headerX = x0 + (innerW - headerW) / 2;
            int headerTextX = headerX + showcaseH + HeaderGapTexels * scale;
            var showcaseTile = new Rect(headerX, y, showcaseH, showcaseH);
            ItemTilePainter.Draw(skin, c.ShowcaseSprite, c.ShowcaseBadge, headerX, y, ShowcaseTileScale);
            // 제목 글자색은 GUI.color로 곱한다 — 글리프의 흰 획이 그 색이 되고 어두운 상자도 함께 어두워진다 (XP 팝업과 같은 방식). 꼬리도 같은 색.
            var prevColor = GUI.color;
            GUI.color = prevColor * c.TitleTint;
            PixelGui.DrawText(skin.FontPlain, c.Title, headerTextX, y, TitleScale);
            if (c.TitleTag != null)
            {
                // 꼬리는 글자 배율 — 글리프 상자의 아래 변을 제목에 맞춘다 (상자 아래 변 = 줄 위 y + (capTop + capHeight) × 배율).
                int tagX = headerTextX + PixelGui.MeasureText(skin.FontPlain, c.Title, TitleScale) + space;
                int tagY = y + (skin.FontPlain.capTop + skin.FontPlain.capHeight) * (TitleScale - textScale);
                PixelGui.DrawText(skin.FontPlain, c.TitleTag, tagX, tagY, textScale);
            }
            GUI.color = prevColor;
            int nameY = y + titleH + headerTextGap;
            foreach (var line in nameLines)
            {
                PixelGui.DrawText(skin.FontOutlined, line, headerTextX, nameY, NameScale);
                nameY += nameH;
            }
            y += headerH + gap;

            if (c.RowLabel != null)
            {
                PixelGui.DrawText(skin.FontPlain, c.RowLabel, x0, y, textScale);
                DrawRowValue(skin, c.LevelNow, c.LevelNext, arrowX, iconSize, space, y, textScale);
                y += lineH + gap;
            }

            for (int i = 0; i < c.StatLabels.Length; i++)
            {
                PixelGui.DrawText(skin.FontPlain, c.StatLabels[i], x0, y, textScale);
                DrawRowValue(skin, c.StatNow[i], c.StatNext[i], arrowX, iconSize, space, y, textScale);
                y += lineH;
            }

            // 특성 줄은 표의 연장 — 값 없는 라벨처럼 라벨 열(x0)에. 가운데 맞춤 아웃라인으로 두면 표와 결이 달라 튀었고, 값 열 오른쪽 정렬은 라벨을 읽던 눈이 건너뛰어야 했다.
            foreach (var line in c.TraitLines)
            {
                PixelGui.DrawText(skin.FontPlain, line, x0, y, textScale);
                y += lineH;
            }

            // 본문은 가운데 맞춤 — 왼쪽 라벨·오른쪽 값의 표와 다른 결이라 덩어리가 갈린다. 유물 카드의 수정치가 여기 온다 (표가 없어 기댈 열이 없다).
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
                        LevelNow = null,
                        LevelNext = start.ToString(),
                        StatLabels = acquired.Labels,
                        StatNow = acquired.Now,
                        StatNext = acquired.Next,
                        TraitLines = WeaponFlags(d),
                        BodyLines = NoLines,
                        Note = d.Description,
                    };
                }

                case UpgradeKind.LevelUp:
                {
                    if (option.TargetSlotIndex >= weapons.Slots.Count)
                        throw new System.Exception($"[PickShoot] UpgradeCardPainter: 레벨업 카드의 slotIndex {option.TargetSlotIndex}가 범위 밖 (현재 슬롯 {weapons.Slots.Count}개).");
                    var w = weapons.Slots[option.TargetSlotIndex];
                    // 오르는 단계 수(UpgradeOption.Levels)는 보통 1. 둘 이상이면 제목이 `LV UP (+n)`이고 "다음" 열·배지가 그 레벨의 값.
                    int next = w.Level + option.Levels;
                    if (next > Weapon.MaxLevel)
                        throw new System.Exception($"[PickShoot] UpgradeCardPainter: 레벨업 카드가 '{w.Data.DisplayName}' 레벨 {w.Level}에 {option.Levels}를 더하면 최대 {Weapon.MaxLevel}을 넘음. 후보를 만들 때 걸러야 함 (UpgradeManager).");
                    var levelled = BuildWeaponStats(w.Data, w.Level, next, stats.DamageMultiplier);
                    return new CardContent
                    {
                        Title = LevelUpTitle,
                        TitleTag = LevelTag(option.Levels),
                        TitleTint = Color.white,
                        ShowcaseSprite = w.Data.WeaponSprite,
                        ShowcaseBadge = ItemInfoText.WeaponBadge(next),
                        Name = w.Data.DisplayName.ToUpperInvariant(),
                        RowLabel = LevelLabel,
                        LevelNow = w.Level.ToString(),
                        LevelNext = next.ToString(),
                        StatLabels = levelled.Labels,
                        StatNow = levelled.Now,
                        StatNext = levelled.Next,
                        TraitLines = WeaponFlags(w.Data),
                        BodyLines = NoLines,
                        Note = w.Data.Description,
                    };
                }

                case UpgradeKind.Relic:
                {
                    var d = option.RelicData;
                    if (d.Modifiers.IsZero)
                        throw new System.Exception($"[PickShoot] UpgradeCardPainter: 유물 '{d.DisplayName}'의 수정치가 전부 0 — 효과 없는 유물. 자산 확인.");
                    // 유물 카드에는 표가 없다 — 그 유물이 더하는 수정치를 `GUN DAMAGE +10%` 한 줄로 본문(가운데 맞춤, 일반 글꼴)에 둔다 (2026-10-07).
                    // 라벨과 값을 좌우 끝으로 갈라 놓으면(표) 한 줄짜리 효과를 읽는 데 눈이 빈칸을 건너야 한다. 값은 "지금 총계 → 획득 뒤 총계"였다가
                    // 수정치 하나로 바꿨다 — 카드가 답할 것은 "이걸 고르면 무엇이 붙나"이고, 총계는 일시정지 화면이 보일 자리다 (Relics §7).
                    // 무기 카드의 특성 줄은 같은 날 표에 붙어 오른쪽 정렬 일반 글꼴이 됐지만, 표가 없는 이 카드에서 한 줄을 오른쪽에 붙이면 기댈 열이 없다.
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
                        Title = d.Grade.ToString().ToUpperInvariant(),
                        TitleTag = null,
                        TitleTint = GradeTitleTint(d.Grade),
                        ShowcaseSprite = d.Icon,
                        ShowcaseBadge = ItemInfoText.RelicBadge(owned + 1),
                        Name = d.DisplayName.ToUpperInvariant(),
                        // 보유 수 줄(`OWNED 1 -> 2`)은 2026-10-07에 뺐다 — 전시 타일의 `X n` 배지가 고른 뒤의 보유 수를 이미 말한다.
                        RowLabel = null,
                        StatLabels = NoLines,
                        StatNow = NoLines,
                        StatNext = NoLines,
                        TraitLines = NoLines,
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

        // 수치가 없는 특성 — 표에 둘 값이 없어 라벨 없는 줄이 된다 (자동 회수·당기는 폭발).
        static string[] WeaponFlags(WeaponData data)
        {
            var flags = new List<string>();
            ItemInfoText.WeaponTraitFlags(data, flags);
            return flags.ToArray();
        }

        // 제목 뒤의 보너스 꼬리. 한 장이 레벨을 둘 이상 올리는 카드(아직 없다 — 앞으로의 보너스 카드)는 `NEW (+2)`·`LV UP (+3)`처럼 올리는 수를 괄호에 붙인다.
        // 획득 카드의 수는 시작 레벨이라 1이면 레벨업의 +1과 같은 보통 카드 — 붙이지 않는다 (2026-10-08).
        static string LevelTag(int levels)
        {
            return levels >= 2 ? $"(+{levels})" : null;
        }

        // 등급 제목의 글자색 (Relics §1). 커먼은 흰색 — 배지 시절(2026-10-08까지)엔 회색 패널로 무기 카드의 노란 NEW!·파란 LV UP과 갈랐다. 레어·유니크는 그 단계에서 정한다 (Relics §5).
        static Color GradeTitleTint(RelicGrade grade)
        {
            switch (grade)
            {
                case RelicGrade.Common: return Color.white;
                default:
                    throw new System.Exception($"[PickShoot] UpgradeCardPainter: 등급 {grade}의 제목 글자색이 아직 정해지지 않음 (Relics §5 3·4단계).");
            }
        }

        // 한 줄의 값. now가 null이면 "다음" 열에 값 하나만(비교할 것이 없는 줄), 아니면 "현재 → 다음" — 현재 값은 화살표 왼쪽에 오른쪽 정렬,
        // 다음 값은 화살표 오른쪽에 왼쪽 정렬이다. arrowX는 화살표 아이콘의 왼쪽 끝. 값 하나든 둘이든 "고른 뒤의 값"은 늘 같은 열에 선다.
        static void DrawRowValue(PixelUiSkin skin, string now, string next, int arrowX, int iconSize, int space, int y, int textScale)
        {
            if (now != null)
            {
                PixelGui.DrawTextRight(skin.FontPlain, now, arrowX - space, y, textScale);
                // 위쪽 화살표 아이콘을 시계방향 90° 돌려 오른쪽 화살표로 쓴다.
                PixelGui.DrawSpriteRotated(skin.IconArrowUp, new Rect(arrowX, y, iconSize, iconSize), 90f);
            }
            PixelGui.DrawText(skin.FontPlain, next, arrowX + iconSize + space, y, textScale);
        }
    }
}
