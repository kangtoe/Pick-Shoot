using System.Collections.Generic;
using UnityEngine;

namespace PickShoot
{
    // 업그레이드 카드 한 장의 IMGUI 그리기 (Kenney Interface 시트 기반 픽셀 UI).
    // 레이아웃은 텍셀 단위로 정의하고 scale(패널·아이콘)과 textScale(글자)을 곱해 픽셀로 바꾼다.
    // 카드 크기는 타일 단위로 고정이며 내용에 맞춰 늘리지 않는다 — 내용이 카드를 넘치면 예외로 알린다.
    // 획득·레벨업 카드는 같은 패널·같은 줄 구성이고, 종류는 카드 위쪽 끝에 걸치는 배지(노란 NEW! / 카드와 같은 파란 LV UP)로만 표기한다.
    // 전시 칸은 일시정지·보상 화면의 빌드 격자와 같은 정사각 타일(ItemTilePainter, 배율 6)이고 배지는 고른 뒤의 상태(`LV1`·`LV3`·`X2`) — 레벨 줄의 "2 → 3"과 맞춘다.
    // 표에는 DMG·AMMO 뒤로 그 무기의 수치 특성(`BURST 3`·`BOUNCE 5`·`PIERCE 1`·`BLAST 2.5`, ItemInfoText.WeaponTraitStats)이 같은 줄로 이어지고,
    // 수치가 없는 특성(`AUTO RETRIEVE`·`PULLS IN`)은 표의 마지막 줄들로 — 값 없는 라벨처럼 라벨 열(왼쪽)에 일반 글꼴 (2026-10-07 표 아래 가운데 맞춤 아웃라인
    // → 같은 날 값 열 오른쪽 정렬 → 2026-10-08 라벨 열 왼쪽 정렬. 표와 결이 달라 튀던 것을 표에 붙였고, 라벨 자리가 읽는 순서에 맞다). 무기를 가르는 것이 카드에서 바로
    // 읽히게 — 그전에는 호버 툴팁에만 있었다. 자리는 RPM 줄을 뺀 자리와 간격을 줄여 만들었고, 가장 긴 조합(특성 줄 + 수치 특성 한 줄 — 부메랑·볼텍스)에서
    // 아래 여백이 48px 남는다 (7×9타일, 2026-10-08). 넘치면 예외.
    // 타일에 마우스를 올리면 격자와 같은 툴팁(TooltipPainter)이 뜬다 — 카드에 없는 탄속·넉백은 거기서 보인다 (2026-10-06).
    // 카드 호버는 테두리를 바깥으로 HoverOutlineTexels 넓히기 — 카드보다 그만큼 큰 흰 판 모양(PixelGui.FillPlateShape)을 패널 밑에 깔아 패널의 흰 바깥 선
    // 1텍셀이 2텍셀이 된다. 패널 모서리도 판처럼 1텍셀 깎여 있어 그 귀가 흰색으로 메워지고 바깥 귀만 깎인다. 리롤 버튼도 같은 값 (2026-10-08).
    // 그전엔 4텍셀 들림(띠→카드 간격 40px일 때 배지가 부제를 가림)·선택 테두리·비정수 배율 키우기(텍셀이 갈려 깨져 보이고 아래 빌드 줄 제목을 덮음)를 거쳤다.
    // 유물 카드(Relics §2)도 같은 패널 구성이다 — 배지는 등급(회색 COMMON), 전시 칸에 유물 아이콘(배지 `X n`이 고른 뒤 보유 수), 표는 아예 없고,
    // 본문 줄에 그 유물이 더하는 수정치를 `GUN DAMAGE +10%`처럼 한 줄로 적는다 (2026-10-07).
    public static class UpgradeCardPainter
    {
        const int CardWidthTiles = 7;
        // 10에서 9로 (2026-10-08) — 표가 짧은 카드(산탄총·유물)의 아래가 비어 보였다. 줄 간격(RowGap·NameGap)을 8에서 6텍셀로 줄여 가장 긴 카드도 아래 여백 48px을 지킨다.
        const int CardHeightTiles = 9;
        const int SidePaddingTexels = 10; // 좌우 여백 (패널 테두리 → 글자). 산탄총 고레벨의 "256X3" 값까지 7타일 안에 들어가는 최대치.
        const int TopMarginTexels = 20;   // 위 여백 = 배지가 카드 안으로 들어오는 깊이(BadgeInsetTexels) + 전시까지 간격 4
        const int BottomMinTexels = 8;    // 넘침 검사용 최소 아래 여백. 실제 아래 여백은 남는 만큼.
        const int RowGapTexels = 6;       // 전시→이름, 레벨 줄→스탯 줄 (2026-10-08 카드를 9타일로 낮추며 8에서)
        // 이름→레벨 줄. 위쪽(전시·이름)과 아래쪽(레벨·스탯 표)을 나눈다. 본문 줄이 들어오며 12에서 8로 (2026-10-07), 카드를 낮추며 6으로 (2026-10-08).
        const int NameGapTexels = 6;
        const int BodyGapTexels = 4;      // 스탯 표→본문 줄(특성·설명)
        // 전시 타일의 배율. 28텍셀 타일 × 6 = 168px. 8이면 224px라 카드가 한 타일 자라 보상 화면 블록이 캔버스 세로를 넘친다.
        // 전시 칸 높이는 타일 그대로다 — 3타일(192px)로 두던 여백 24px도 본문 줄에 넘겼다 (2026-10-07).
        const int ShowcaseTileScale = 6;
        // 호버 때 테두리를 바깥으로 넓히는 두께 (패널 텍셀). 리롤 버튼(UpgradeManager)도 같은 값.
        public const int HoverOutlineTexels = 1;
        // 무기 이름은 카드의 제목이라 글자 배율(2)보다 한 단계 큰 3 — 배너 제목(패널 배율 4)보다는 작게.
        const int NameScale = 3;
        // 종류 배지: 패널 한 줄, 패널 색과 문구로 구분 — 획득은 노란 NEW!, 레벨업은 카드 본체와 같은 파란 LV UP — 카드에서 이어진 탭처럼 보이고 새 무기만 튄다. 느낌표는 새 무기에만.
        // 패널·글자 모두 이름과 같은 배율 3. 카드 위쪽 끝에 걸쳐 BadgeInsetTexels(패널 텍셀)만큼 카드 안으로 들어온다 — 배지 높이의 2/3.
        // 문구는 짧게 — "LEVEL UP"은 배율 3에서 카드 폭의 2/3을 차지한다. 일시정지 화면의 상태 카드도 같은 배지(DrawBadge)를 쓴다.
        const string NewBadgeText = "NEW!";
        const string LevelUpBadgeText = "LV UP";
        // 표 첫 줄(무기 카드)의 라벨 — 값은 "n → n+1". 슬롯 번호(`SLOT 3`)는 2026-10-07에 뺐다 — 자루를 가리키는 번호였지만 전시 그림·레벨로 충분하고,
        // 레벨 값의 `LV` 접두도 함께 걷어 값 열이 좁아졌다. 유물 카드는 이 줄 자체가 없다 (같은 날 `OWNED n → n+1`도 뺐다).
        const string LevelLabel = "LEVEL";
        const int BadgeScale = 3;
        const int BadgePaddingXTexels = 10; // 배지 패널 테두리 → 글자, 좌우 (배지 배율)
        const int BadgePaddingYTexels = 8;  // 위아래
        public const int BadgeInsetTexels = 16;
        // 줄이 없는 칸용 빈 묶음 (프레임마다 새로 만들지 않는다).
        static readonly string[] NoLines = new string[0];
        static readonly List<string> EmptyLines = new List<string>();

        // 글꼴에 있는 문자(A-Z 0-9 % + - ! 공백)만 써야 한다. 없는 문자는 PixelGui가 예외로 알린다.
        struct CardContent
        {
            public string Badge;
            public PixelUiSkin.NineSlice BadgePanel;
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
            int nameGap = NameGapTexels * scale;
            int bodyGap = BodyGapTexels * scale;
            int lineH = skin.FontPlain.lineHeight * textScale;
            int space = skin.FontPlain.spaceWidth * textScale;
            int nameH = skin.FontOutlined.lineHeight * NameScale;

            int x0 = (int)rect.x + pad;
            int x1 = (int)rect.xMax - pad;
            int centerX = (int)rect.center.x;

            // 값 열은 화살표를 한 열에 맞춘다 — 다음 값은 화살표 오른쪽에 왼쪽 정렬, 현재 값은 화살표 왼쪽에 오른쪽 정렬.
            // 화살표 열은 가장 넓은 "다음" 값이 오른쪽 여백 안에 들어가는 자리. 항목 이름 열은 왼쪽 여백에 따로 맞춘다.
            int iconSize = skin.TileSize * textScale;
            int nextColW = c.RowLabel != null ? PixelGui.MeasureText(skin.FontPlain, c.LevelNext, textScale) : 0;
            for (int i = 0; i < c.StatNext.Length; i++)
                nextColW = Mathf.Max(nextColW, PixelGui.MeasureText(skin.FontPlain, c.StatNext[i], textScale));
            int arrowX = x1 - nextColW - space - iconSize;
            int valueTail = space + iconSize + space + nextColW;   // 현재 값 뒤: 공백·화살표·공백·다음 값 열

            // 크기가 고정이라 내용이 카드를 넘칠 수 있다. 넘치면 어긋난 채 그리지 않고 예외로 알린다.
            // 항목 이름과 현재 값 사이는 최소 공백 하나.
            int innerW = x1 - x0;
            RequireFits(PixelGui.MeasureText(skin.FontOutlined, c.Name, NameScale), innerW, c.Name);
            if (c.RowLabel != null)
                RequireFits(PixelGui.MeasureText(skin.FontPlain, c.RowLabel, textScale) + space
                            + (c.LevelNow == null ? nextColW : PixelGui.MeasureText(skin.FontPlain, c.LevelNow, textScale) + valueTail),
                            innerW, $"{c.RowLabel} {c.LevelNow} > {c.LevelNext}");
            for (int i = 0; i < c.StatLabels.Length; i++)
            {
                if (c.StatNow[i] == null)
                    RequireFits(PixelGui.MeasureText(skin.FontPlain, c.StatLabels[i], textScale) + space + nextColW, innerW, $"{c.StatLabels[i]} {c.StatNext[i]}");
                else
                    RequireFits(PixelGui.MeasureText(skin.FontPlain, c.StatLabels[i], textScale) + space
                                + PixelGui.MeasureText(skin.FontPlain, c.StatNow[i], textScale) + valueTail, innerW, $"{c.StatLabels[i]} {c.StatNow[i]} > {c.StatNext[i]}");
            }

            // 특성·본문 줄: 한 항목이 한 줄(줄 안에서 나누지 않는다 — 항목 하나가 폭을 넘으면 문구를 줄여야 한다), 설명은 단어 단위로 접는다.
            foreach (var line in c.TraitLines)
                RequireFits(PixelGui.MeasureText(skin.FontPlain, line, textScale), innerW, line);
            foreach (var line in c.BodyLines)
                RequireFits(PixelGui.MeasureText(skin.FontPlain, line, textScale), innerW, line);
            var noteLines = string.IsNullOrEmpty(c.Note)
                ? EmptyLines
                : PixelGui.WrapText(skin.FontPlain, c.Note, textScale, innerW);
            int bodyLines = c.BodyLines.Length + noteLines.Count;

            int topMargin = TopMarginTexels * scale;
            int bottomMin = BottomMinTexels * scale;
            int showcaseH = ItemTilePainter.Size(ShowcaseTileScale);
            int contentH = showcaseH + gap + nameH + nameGap + (c.StatLabels.Length + c.TraitLines.Length) * lineH;
            if (c.RowLabel != null) contentH += lineH + gap;
            if (bodyLines > 0) contentH += bodyGap + bodyLines * lineH;
            int innerH = (int)rect.height - topMargin - bottomMin;
            if (contentH > innerH)
                throw new System.Exception($"[PickShoot] UpgradeCardPainter: 카드 내용 높이 {contentH}px가 카드 안쪽 높이 {innerH}px(위 여백 {topMargin}·아래 최소 여백 {bottomMin} 제외)를 넘음 (본문 {bodyLines}줄). 본문 문구를 줄이거나 간격을 줄일 것 — 카드를 키우면 보상 화면 블록이 캔버스 세로를 넘친다 (UpgradeManager).");

            // 호버면 카드보다 한 텍셀 큰 흰 판 모양을 먼저 깔아 테두리를 바깥으로 넓힌다.
            if (hovered)
            {
                int o = HoverOutlineTexels * scale;
                PixelGui.FillPlateShape(new Rect(rect.x - o, rect.y - o, rect.width + 2 * o, rect.height + 2 * o), scale, Color.white);
            }
            // 획득·레벨업 카드는 같은 패널(일시정지 배너와 같은 파란 패널).
            PixelGui.DrawNineSlice(skin.PanelBlue, rect, skin.TileSize, scale);

            // 위 여백은 고정(배지 자리), 남는 공간은 아래 여백. 획득·레벨업 카드의 내용 위치가 같다.
            int y = (int)rect.y + topMargin;

            // 전시: 빌드 격자와 같은 정사각 타일 — 그림과 고른 뒤의 상태 배지.
            var showcaseTile = new Rect(centerX - showcaseH / 2, y, showcaseH, showcaseH);
            ItemTilePainter.Draw(skin, c.ShowcaseSprite, c.ShowcaseBadge, (int)showcaseTile.x, (int)showcaseTile.y, ShowcaseTileScale);
            y += showcaseH + gap;

            PixelGui.DrawTextCentered(skin.FontOutlined, c.Name, centerX, y, NameScale);
            y += nameH + nameGap;

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

            // 배지는 카드 테두리 위에 얹히므로 맨 마지막에 그린다.
            DrawBadge(skin, rect, scale, c.Badge, c.BadgePanel);
            return showcaseTile;
        }

        // 배지가 카드 위쪽 끝 위로 솟는 높이 (기준 캔버스 픽셀). 카드를 포함한 묶음의 높이를 잴 때 쓴다.
        public static int BadgeOverhang(PixelUiSkin skin, int scale)
        {
            return BadgeHeight(skin) - BadgeInsetTexels * scale;
        }

        static int BadgeHeight(PixelUiSkin skin)
        {
            return (skin.FontPlain.lineHeight + 2 * BadgePaddingYTexels) * BadgeScale;
        }

        // card는 배지를 얹을 카드의 영역(캔버스 픽셀), scale은 그 카드의 패널 배율. 배지는 카드 위쪽 끝 가운데에 걸친다.
        public static void DrawBadge(PixelUiSkin skin, Rect card, int scale, string text, PixelUiSkin.NineSlice panel)
        {
            int badgeTile = skin.TileSize * BadgeScale;
            int padX = BadgePaddingXTexels * BadgeScale;
            int lineH = skin.FontPlain.lineHeight * BadgeScale;
            // 폭은 타일 배수로 올리지 않는다 — 올리면 문구에 따라 글자 양옆 여백이 들쭉날쭉해진다. 9-slice는 부분 타일을 지원하고
            // 모서리 2개(타일 2개)보다는 넓어야 한다.
            int w = Mathf.Max(2 * badgeTile, PixelGui.MeasureText(skin.FontPlain, text, BadgeScale) + 2 * padX);
            int h = BadgeHeight(skin);
            if (w > (int)card.width)
                throw new System.Exception($"[PickShoot] UpgradeCardPainter: 배지 '{text}'의 폭 {w}px가 카드 폭 {(int)card.width}px를 넘음. 문구를 줄이거나 BadgeScale을 낮출 것.");
            int centerX = (int)card.center.x;
            var badge = new Rect(centerX - w / 2, (int)card.y + BadgeInsetTexels * scale - h, w, h);
            PixelGui.DrawNineSlice(panel, badge, skin.TileSize, BadgeScale);
            PixelGui.DrawTextCentered(skin.FontPlain, text, centerX, (int)badge.y + (h - lineH) / 2, BadgeScale);
        }

        static void RequireFits(int width, int innerW, string what)
        {
            if (width > innerW)
                throw new System.Exception($"[PickShoot] UpgradeCardPainter: 카드 내용 '{what}'의 폭 {width}px가 카드 안쪽 폭 {innerW}px를 넘음. 카드 크기는 고정이라 내용에 맞춰 늘지 않는다 — CardWidthTiles를 늘리거나 문구를 줄일 것.");
        }

        static CardContent Build(PixelUiSkin skin, UpgradeOption option, PlayerWeapons weapons, PlayerStats stats)
        {
            switch (option.Kind)
            {
                case UpgradeKind.AcquireWeapon:
                {
                    var d = option.AcquireData;
                    var acquired = BuildWeaponStats(d, 0, 1, stats.DamageMultiplier);
                    return new CardContent
                    {
                        Badge = NewBadgeText,
                        BadgePanel = skin.PanelYellow,
                        ShowcaseSprite = d.WeaponSprite,
                        ShowcaseBadge = ItemInfoText.WeaponBadge(1),
                        Name = d.DisplayName.ToUpperInvariant(),
                        RowLabel = LevelLabel,
                        LevelNow = null,
                        LevelNext = "1",
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
                    var levelled = BuildWeaponStats(w.Data, w.Level, w.Level + 1, stats.DamageMultiplier);
                    return new CardContent
                    {
                        Badge = LevelUpBadgeText,
                        BadgePanel = skin.PanelBlue,
                        ShowcaseSprite = w.Data.WeaponSprite,
                        ShowcaseBadge = ItemInfoText.WeaponBadge(w.Level + 1),
                        Name = w.Data.DisplayName.ToUpperInvariant(),
                        RowLabel = LevelLabel,
                        LevelNow = w.Level.ToString(),
                        LevelNext = (w.Level + 1).ToString(),
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
                        Badge = d.Grade.ToString().ToUpperInvariant(),
                        BadgePanel = GradeBadgePanel(skin, d.Grade),
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

        // 등급 배지의 패널 (Relics §1). 커먼은 회색 — 무기 카드의 노란 NEW!·파란 LV UP과 겹치지 않는다. 레어·유니크는 그 단계에서 정한다 (Relics §5).
        static PixelUiSkin.NineSlice GradeBadgePanel(PixelUiSkin skin, RelicGrade grade)
        {
            switch (grade)
            {
                case RelicGrade.Common: return skin.PanelGrey;
                default:
                    throw new System.Exception($"[PickShoot] UpgradeCardPainter: 등급 {grade}의 배지 패널이 아직 정해지지 않음 (Relics §5 3·4단계).");
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
