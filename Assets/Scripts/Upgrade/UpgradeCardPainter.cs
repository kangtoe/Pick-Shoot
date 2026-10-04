using System.Collections.Generic;
using UnityEngine;

namespace PickShoot
{
    // 업그레이드 카드 한 장의 IMGUI 그리기 (Kenney Interface 시트 기반 픽셀 UI).
    // 레이아웃은 텍셀 단위로 정의하고 scale(패널·아이콘)과 textScale(글자)을 곱해 픽셀로 바꾼다.
    // 카드 크기는 타일 단위로 고정이며 내용에 맞춰 늘리지 않는다 — 내용이 카드를 넘치면 예외로 알린다.
    // 획득·레벨업 카드는 같은 패널·같은 줄 구성이고, 종류는 카드 위쪽 끝에 걸치는 배지(노란 NEW! / 카드와 같은 파란 LV UP)로만 표기한다.
    // 유물 카드(Relics §2)도 같은 패널·줄 구성이다 — 배지는 등급(회색 COMMON), 전시 칸에 유물 아이콘, 슬롯 줄 자리에 보유 수 "OWNED n → n+1",
    // 스탯 줄에는 그 유물이 건드리는 스탯만 "지금 총계 → 획득 뒤 총계"로 — 중첩이 카드에서 바로 읽힌다.
    public static class UpgradeCardPainter
    {
        const int CardWidthTiles = 7;
        const int CardHeightTiles = 10;
        const int SidePaddingTexels = 10; // 좌우 여백 (패널 테두리 → 글자). 산탄총 고레벨의 "256X3" 값까지 7타일 안에 들어가는 최대치.
        const int TopMarginTexels = 20;   // 위 여백 = 배지가 카드 안으로 들어오는 깊이(BadgeInsetTexels) + 전시까지 간격 4
        const int BottomMinTexels = 8;    // 넘침 검사용 최소 아래 여백. 실제 아래 여백은 남는 만큼.
        const int RowGapTexels = 8;       // 전시→이름, 슬롯 줄→스탯 줄
        const int NameGapTexels = 12;     // 이름→슬롯 줄. 위쪽(전시·이름)과 아래쪽(슬롯·스탯 표)을 나눈다.
        const int ShowcaseWidthTiles = 4;
        const int ShowcaseHeightTiles = 3;
        // 전시 칸의 그림(무기 스프라이트·유물 아이콘)은 패널 배율의 2배로 크게 보여준다 (16텍셀 → 1080p에서 128px).
        const int ShowcaseScaleMultiplier = 2;
        const int HoverLiftTexels = 4;
        // 무기 이름은 카드의 제목이라 글자 배율(2)보다 한 단계 큰 3 — 배너 제목(패널 배율 4)보다는 작게.
        const int NameScale = 3;
        // 획득·레벨업 카드는 줄 구성이 같다 — 값 열은 늘 "현재 → 다음". 새 무기는 현재 값이 없으므로 "-"를 둔다.
        const string NoValue = "-";
        // 종류 배지: 패널 한 줄, 패널 색과 문구로 구분 — 획득은 노란 NEW!, 레벨업은 카드 본체와 같은 파란 LV UP — 카드에서 이어진 탭처럼 보이고 새 무기만 튄다. 느낌표는 새 무기에만.
        // 패널·글자 모두 이름과 같은 배율 3. 카드 위쪽 끝에 걸쳐 BadgeInsetTexels(패널 텍셀)만큼 카드 안으로 들어온다 — 배지 높이의 2/3.
        // 문구는 짧게 — "LEVEL UP"은 배율 3에서 카드 폭의 2/3을 차지한다. 일시정지 화면의 상태 카드도 같은 배지(DrawBadge)를 쓴다.
        const string NewBadgeText = "NEW!";
        const string LevelUpBadgeText = "LV UP";
        // 유물 카드의 슬롯 줄 라벨 — 값은 보유 수 "n → n+1".
        const string OwnedLabel = "OWNED";
        const int BadgeScale = 3;
        const int BadgePaddingXTexels = 10; // 배지 패널 테두리 → 글자, 좌우 (배지 배율)
        const int BadgePaddingYTexels = 8;  // 위아래
        public const int BadgeInsetTexels = 16;

        // 글꼴에 있는 문자(A-Z 0-9 % + - ! 공백)만 써야 한다. 없는 문자는 PixelGui가 예외로 알린다.
        struct CardContent
        {
            public string Badge;
            public PixelUiSkin.NineSlice BadgePanel;
            public Sprite ShowcaseSprite;
            public string Name;
            public string SlotLabel;
            public string LevelNow;
            public string LevelNext;
            public string[] StatLabels;
            public string[] StatNow;
            public string[] StatNext;
        }

        public static Vector2Int CardSize(PixelUiSkin skin, int scale)
        {
            int tile = skin.TileSize * scale;
            return new Vector2Int(CardWidthTiles * tile, CardHeightTiles * tile);
        }

        public static void Draw(PixelUiSkin skin, UpgradeOption option, PlayerWeapons weapons, PlayerStats stats, Rect rect, int scale, int textScale, bool hovered)
        {
            var c = Build(skin, option, weapons, stats);
            int tile = skin.TileSize * scale;
            int pad = SidePaddingTexels * scale;
            int gap = RowGapTexels * scale;
            int nameGap = NameGapTexels * scale;
            int lineH = skin.FontPlain.lineHeight * textScale;
            int space = skin.FontPlain.spaceWidth * textScale;
            int nameH = skin.FontOutlined.lineHeight * NameScale;

            if (hovered) rect.y -= HoverLiftTexels * scale;
            int x0 = (int)rect.x + pad;
            int x1 = (int)rect.xMax - pad;
            int centerX = (int)rect.center.x;

            // 값 열은 화살표를 한 열에 맞춘다 — 다음 값은 화살표 오른쪽에 왼쪽 정렬, 현재 값은 화살표 왼쪽에 오른쪽 정렬.
            // 화살표 열은 가장 넓은 "다음" 값이 오른쪽 여백 안에 들어가는 자리. 항목 이름 열은 왼쪽 여백에 따로 맞춘다.
            int iconSize = skin.TileSize * textScale;
            int nextColW = PixelGui.MeasureText(skin.FontPlain, c.LevelNext, textScale);
            for (int i = 0; i < c.StatNext.Length; i++)
                nextColW = Mathf.Max(nextColW, PixelGui.MeasureText(skin.FontPlain, c.StatNext[i], textScale));
            int arrowX = x1 - nextColW - space - iconSize;
            int valueTail = space + iconSize + space + nextColW;   // 현재 값 뒤: 공백·화살표·공백·다음 값 열

            // 크기가 고정이라 내용이 카드를 넘칠 수 있다. 넘치면 어긋난 채 그리지 않고 예외로 알린다.
            // 항목 이름과 현재 값 사이는 최소 공백 하나.
            int innerW = x1 - x0;
            RequireFits(PixelGui.MeasureText(skin.FontOutlined, c.Name, NameScale), innerW, c.Name);
            RequireFits(PixelGui.MeasureText(skin.FontPlain, c.SlotLabel, textScale) + space
                        + PixelGui.MeasureText(skin.FontPlain, c.LevelNow, textScale) + valueTail, innerW, $"{c.SlotLabel} {c.LevelNow} > {c.LevelNext}");
            for (int i = 0; i < c.StatLabels.Length; i++)
                RequireFits(PixelGui.MeasureText(skin.FontPlain, c.StatLabels[i], textScale) + space
                            + PixelGui.MeasureText(skin.FontPlain, c.StatNow[i], textScale) + valueTail, innerW, $"{c.StatLabels[i]} {c.StatNow[i]} > {c.StatNext[i]}");
            int topMargin = TopMarginTexels * scale;
            int bottomMin = BottomMinTexels * scale;
            int contentH = ShowcaseHeightTiles * tile + gap + nameH + nameGap + lineH + gap + c.StatLabels.Length * lineH;
            int innerH = (int)rect.height - topMargin - bottomMin;
            if (contentH > innerH)
                throw new System.Exception($"[PickShoot] UpgradeCardPainter: 카드 내용 높이 {contentH}px가 카드 안쪽 높이 {innerH}px(위 여백 {topMargin}·아래 최소 여백 {bottomMin} 제외)를 넘음. CardHeightTiles를 늘리거나 간격을 줄일 것.");

            // 획득·레벨업 카드는 같은 패널(일시정지 배너와 같은 파란 패널).
            PixelGui.DrawNineSlice(skin.PanelBlue, rect, skin.TileSize, scale);

            // 위 여백은 고정(배지 자리), 남는 공간은 아래 여백. 획득·레벨업 카드의 내용 위치가 같다.
            int y = (int)rect.y + topMargin;

            // 전시: 회색 패널 안에 무기 스프라이트·유물 아이콘을 크게
            int showcaseW = ShowcaseWidthTiles * tile;
            int showcaseH = ShowcaseHeightTiles * tile;
            var showcase = new Rect(centerX - showcaseW / 2, y, showcaseW, showcaseH);
            PixelGui.DrawNineSlice(skin.PanelGrey, showcase, skin.TileSize, scale);
            int showcaseScale = scale * ShowcaseScaleMultiplier;
            var sr = c.ShowcaseSprite.rect;
            int spriteW = Mathf.RoundToInt(sr.width) * showcaseScale;
            int spriteH = Mathf.RoundToInt(sr.height) * showcaseScale;
            PixelGui.DrawSprite(c.ShowcaseSprite,
                new Rect((int)showcase.center.x - spriteW / 2, (int)showcase.center.y - spriteH / 2, spriteW, spriteH));
            y += showcaseH + gap;

            PixelGui.DrawTextCentered(skin.FontOutlined, c.Name, centerX, y, NameScale);
            y += nameH + nameGap;

            PixelGui.DrawText(skin.FontPlain, c.SlotLabel, x0, y, textScale);
            DrawValue(skin, c.LevelNow, c.LevelNext, arrowX, y, textScale);
            y += lineH + gap;

            for (int i = 0; i < c.StatLabels.Length; i++)
            {
                PixelGui.DrawText(skin.FontPlain, c.StatLabels[i], x0, y, textScale);
                DrawValue(skin, c.StatNow[i], c.StatNext[i], arrowX, y, textScale);
                y += lineH;
            }

            if (hovered)
                PixelGui.DrawNineSlice(skin.SelectionFrame, rect, skin.TileSize, scale);

            // 배지는 카드 테두리·선택 테두리 위에 얹히므로 맨 마지막에 그린다. 카드와 함께 hover 들림을 따른다.
            DrawBadge(skin, rect, scale, c.Badge, c.BadgePanel);
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
                    return new CardContent
                    {
                        Badge = NewBadgeText,
                        BadgePanel = skin.PanelYellow,
                        ShowcaseSprite = d.WeaponSprite,
                        Name = d.DisplayName.ToUpperInvariant(),
                        SlotLabel = $"SLOT {weapons.Slots.Count + 1}",
                        LevelNow = NoValue,
                        LevelNext = "LV 1",
                        StatLabels = new[] { "DMG", "RPM", "AMMO" },
                        StatNow = new[] { NoValue, NoValue, NoValue },
                        StatNext = StatValues(d, 1, stats.DamageMultiplier),
                    };
                }

                case UpgradeKind.LevelUp:
                {
                    if (option.TargetSlotIndex >= weapons.Slots.Count)
                        throw new System.Exception($"[PickShoot] UpgradeCardPainter: 레벨업 카드의 slotIndex {option.TargetSlotIndex}가 범위 밖 (현재 슬롯 {weapons.Slots.Count}개).");
                    var w = weapons.Slots[option.TargetSlotIndex];
                    return new CardContent
                    {
                        Badge = LevelUpBadgeText,
                        BadgePanel = skin.PanelBlue,
                        ShowcaseSprite = w.Data.WeaponSprite,
                        Name = w.Data.DisplayName.ToUpperInvariant(),
                        SlotLabel = $"SLOT {option.TargetSlotIndex + 1}",
                        LevelNow = $"LV {w.Level}",
                        LevelNext = $"LV {w.Level + 1}",
                        StatLabels = new[] { "DMG", "RPM", "AMMO" },
                        StatNow = StatValues(w.Data, w.Level, stats.DamageMultiplier),
                        StatNext = StatValues(w.Data, w.Level + 1, stats.DamageMultiplier),
                    };
                }

                case UpgradeKind.Relic:
                {
                    var d = option.RelicData;
                    if (d.Modifiers.IsZero)
                        throw new System.Exception($"[PickShoot] UpgradeCardPainter: 유물 '{d.DisplayName}'의 수정치가 전부 0 — 효과 없는 유물. 자산 확인.");
                    // 줄은 그 유물이 건드리는 스탯만. 값은 "지금 총계 → 획득 뒤 총계".
                    var now = stats.Total;
                    var next = now + d.Modifiers;
                    var labels = new List<string>();
                    var nowText = new List<string>();
                    var nextText = new List<string>();
                    foreach (var s in PlayerStatModifiers.Stats)
                    {
                        if (d.Modifiers.Get(s) == 0) continue;
                        labels.Add(PlayerStatModifiers.Label(s));
                        nowText.Add(PlayerStatModifiers.Format(s, now.Get(s)));
                        nextText.Add(PlayerStatModifiers.Format(s, next.Get(s)));
                    }
                    int owned = stats.CountOf(d);
                    return new CardContent
                    {
                        Badge = d.Grade.ToString().ToUpperInvariant(),
                        BadgePanel = GradeBadgePanel(skin, d.Grade),
                        ShowcaseSprite = d.Icon,
                        Name = d.DisplayName.ToUpperInvariant(),
                        SlotLabel = OwnedLabel,
                        LevelNow = owned.ToString(),
                        LevelNext = (owned + 1).ToString(),
                        StatLabels = labels.ToArray(),
                        StatNow = nowText.ToArray(),
                        StatNext = nextText.ToArray(),
                    };
                }

                default:
                    throw new System.Exception($"[PickShoot] UpgradeCardPainter: 알 수 없는 Kind {option.Kind}");
            }
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

        // 카드에 싣는 3개 스탯: 공격력(한 차례에 여러 발이면 X발수 — 동시 발사 × 점사), 분당 사격 차례 수(RPM, 점사 소요 시간 포함), 최대 탄약. 모두 정수 표기.
        // 공격력은 플레이어 전역 배율(유물)을 곱한 실제 값 — 유물을 얻은 뒤의 무기 카드는 그만큼 큰 수를 보인다.
        static string[] StatValues(WeaponData d, int level, float damageMultiplier)
        {
            int dmg = Weapon.DamageAtLevel(d, level, damageMultiplier);
            int bullets = Weapon.BulletsPerTriggerAtLevel(d, level);
            string dmgText = bullets > 1 ? $"{dmg}X{bullets}" : dmg.ToString();
            int rpm = Mathf.RoundToInt(60f / Weapon.TriggerIntervalAtLevel(d, level));
            return new[] { dmgText, rpm.ToString(), Weapon.MaxAmmoAtLevel(d, level).ToString() };
        }

        // "현재 → 다음" 한 줄. 현재 값은 화살표 왼쪽에 오른쪽 정렬, 다음 값은 화살표 오른쪽에 왼쪽 정렬. arrowX는 화살표 아이콘의 왼쪽 끝.
        static void DrawValue(PixelUiSkin skin, string now, string next, int arrowX, int y, int textScale)
        {
            int iconSize = skin.TileSize * textScale;
            int space = skin.FontPlain.spaceWidth * textScale;
            PixelGui.DrawTextRight(skin.FontPlain, now, arrowX - space, y, textScale);
            // 위쪽 화살표 아이콘을 시계방향 90° 돌려 오른쪽 화살표로 쓴다.
            PixelGui.DrawSpriteRotated(skin.IconArrowUp, new Rect(arrowX, y, iconSize, iconSize), 90f);
            PixelGui.DrawText(skin.FontPlain, next, arrowX + iconSize + space, y, textScale);
        }
    }
}
