using UnityEngine;

namespace PickShoot
{
    // 업그레이드 카드 한 장의 IMGUI 그리기 (Kenney Interface 시트 기반 픽셀 UI).
    // 레이아웃은 텍셀 단위로 정의하고 scale(패널·아이콘)과 textScale(글자)을 곱해 픽셀로 바꾼다.
    // 카드 크기는 내용(글자 폭)에 따라 달라지므로 UpgradeManager가 Measure로 모든 카드의 최대 크기를 구해 통일한다.
    public static class UpgradeCardPainter
    {
        const int PaddingTexels = 8;
        const int RowGapTexels = 4;
        const int ShowcaseWidthTiles = 4;
        const int ShowcaseHeightTiles = 3;
        const int MinWidthTiles = 6;
        // 무기 스프라이트는 패널 배율의 2배로 크게 보여준다 (16텍셀 → 1080p에서 128px).
        const int WeaponScaleMultiplier = 2;
        const int HoverLiftTexels = 4;
        static readonly Color AcquireTagColor = new Color(1f, 0.87f, 0.55f);

        // 글꼴에 있는 문자(A-Z 0-9 % + - 공백)만 써야 한다. 없는 문자는 PixelGui가 예외로 알린다.
        struct CardContent
        {
            public PixelUiSkin.NineSlice Panel;
            public Sprite TagIcon;
            public string Tag;
            public Color TagColor;
            public Sprite WeaponSprite;
            public string Name;
            public string SlotLabel;
            public string LevelNow;
            public string LevelNext;      // null이면 화살표 없이 LevelNow만 표시
            public string[] StatLabels;
            public string[] StatNow;
            public string[] StatNext;     // null이면 화살표 없이 StatNow만 표시
        }

        public static Vector2Int Measure(PixelUiSkin skin, UpgradeOption option, PlayerWeapons weapons, int scale, int textScale)
        {
            var c = Build(skin, option, weapons);
            int tile = skin.TileSize * scale;
            int pad = PaddingTexels * scale;
            int gap = RowGapTexels * scale;
            int lineH = skin.FontPlain.lineHeight * textScale;
            int iconSize = skin.TileSize * textScale;
            int space = skin.FontPlain.spaceWidth * textScale;

            int contentW = ShowcaseWidthTiles * tile;
            contentW = Mathf.Max(contentW, iconSize + space + PixelGui.MeasureText(skin.FontPlain, c.Tag, textScale));
            contentW = Mathf.Max(contentW, PixelGui.MeasureText(skin.FontOutlined, c.Name, textScale));
            contentW = Mathf.Max(contentW, PixelGui.MeasureText(skin.FontPlain, c.SlotLabel, textScale) + 2 * space
                                           + ValueWidth(skin, c.LevelNow, c.LevelNext, textScale));
            for (int i = 0; i < c.StatLabels.Length; i++)
                contentW = Mathf.Max(contentW, PixelGui.MeasureText(skin.FontPlain, c.StatLabels[i], textScale) + 2 * space
                                               + ValueWidth(skin, c.StatNow[i], c.StatNext == null ? null : c.StatNext[i], textScale));

            int contentH = lineH + gap                                  // 태그 줄
                         + ShowcaseHeightTiles * tile + gap             // 무기 전시
                         + lineH                                        // 이름
                         + lineH + gap                                  // 슬롯·레벨
                         + c.StatLabels.Length * lineH;                 // 스탯 줄들

            int w = Mathf.Max(MinWidthTiles * tile, CeilToMultiple(contentW + 2 * pad, tile));
            int h = CeilToMultiple(contentH + 2 * pad, tile);
            return new Vector2Int(w, h);
        }

        public static void Draw(PixelUiSkin skin, UpgradeOption option, PlayerWeapons weapons, Rect rect, int scale, int textScale, bool hovered)
        {
            var c = Build(skin, option, weapons);
            int tile = skin.TileSize * scale;
            int pad = PaddingTexels * scale;
            int gap = RowGapTexels * scale;
            int lineH = skin.FontPlain.lineHeight * textScale;
            int iconSize = skin.TileSize * textScale;
            int space = skin.FontPlain.spaceWidth * textScale;

            if (hovered) rect.y -= HoverLiftTexels * scale;
            int x0 = (int)rect.x + pad;
            int x1 = (int)rect.xMax - pad;
            int centerX = (int)rect.center.x;

            PixelGui.DrawNineSlice(c.Panel, rect, skin.TileSize, scale);

            // 세로 여백은 위아래로 균등 분배 (카드 높이가 타일 배수로 올림되어 있음).
            int contentH = lineH + gap + ShowcaseHeightTiles * tile + gap + lineH + lineH + gap + c.StatLabels.Length * lineH;
            int y = (int)rect.y + ((int)rect.height - contentH) / 2;

            // 태그: 아이콘 + 종류 이름, 가운데 정렬
            int tagTextW = PixelGui.MeasureText(skin.FontPlain, c.Tag, textScale);
            int tagX = centerX - (iconSize + space + tagTextW) / 2;
            PixelGui.DrawSprite(c.TagIcon, new Rect(tagX, y, iconSize, iconSize));
            GUI.color = c.TagColor;
            PixelGui.DrawText(skin.FontPlain, c.Tag, tagX + iconSize + space, y, textScale);
            GUI.color = Color.white;
            y += lineH + gap;

            // 무기 전시: 회색 패널 안에 스프라이트를 크게
            int showcaseW = ShowcaseWidthTiles * tile;
            int showcaseH = ShowcaseHeightTiles * tile;
            var showcase = new Rect(centerX - showcaseW / 2, y, showcaseW, showcaseH);
            PixelGui.DrawNineSlice(skin.PanelGrey, showcase, skin.TileSize, scale);
            int weaponScale = scale * WeaponScaleMultiplier;
            var wr = c.WeaponSprite.rect;
            int weaponW = Mathf.RoundToInt(wr.width) * weaponScale;
            int weaponH = Mathf.RoundToInt(wr.height) * weaponScale;
            PixelGui.DrawSprite(c.WeaponSprite,
                new Rect((int)showcase.center.x - weaponW / 2, (int)showcase.center.y - weaponH / 2, weaponW, weaponH));
            y += showcaseH + gap;

            PixelGui.DrawTextCentered(skin.FontOutlined, c.Name, centerX, y, textScale);
            y += lineH;

            PixelGui.DrawText(skin.FontPlain, c.SlotLabel, x0, y, textScale);
            DrawValueRight(skin, c.LevelNow, c.LevelNext, x1, y, textScale);
            y += lineH + gap;

            for (int i = 0; i < c.StatLabels.Length; i++)
            {
                PixelGui.DrawText(skin.FontPlain, c.StatLabels[i], x0, y, textScale);
                DrawValueRight(skin, c.StatNow[i], c.StatNext == null ? null : c.StatNext[i], x1, y, textScale);
                y += lineH;
            }

            if (hovered)
                PixelGui.DrawNineSlice(skin.SelectionFrame, rect, skin.TileSize, scale);
        }

        static CardContent Build(PixelUiSkin skin, UpgradeOption option, PlayerWeapons weapons)
        {
            switch (option.Kind)
            {
                case UpgradeKind.AcquireWeapon:
                {
                    var d = option.AcquireData;
                    return new CardContent
                    {
                        Panel = skin.PanelBlue,
                        TagIcon = skin.IconBullet,
                        Tag = "NEW WEAPON",
                        TagColor = AcquireTagColor,
                        WeaponSprite = d.WeaponSprite,
                        Name = d.DisplayName.ToUpperInvariant(),
                        SlotLabel = $"SLOT {weapons.Slots.Count + 1}",
                        LevelNow = "LV 1",
                        LevelNext = null,
                        StatLabels = new[] { "DMG", "RPM", "AMMO" },
                        StatNow = StatValues(d, 1),
                        StatNext = null,
                    };
                }

                case UpgradeKind.LevelUp:
                {
                    if (option.TargetSlotIndex >= weapons.Slots.Count)
                        throw new System.Exception($"[PickShoot] UpgradeCardPainter: 레벨업 카드의 slotIndex {option.TargetSlotIndex}가 범위 밖 (현재 슬롯 {weapons.Slots.Count}개).");
                    var w = weapons.Slots[option.TargetSlotIndex];
                    return new CardContent
                    {
                        Panel = skin.PanelYellow,
                        TagIcon = skin.IconChevronUp,
                        Tag = "LEVEL UP",
                        TagColor = Color.white,
                        WeaponSprite = w.Data.WeaponSprite,
                        Name = w.Data.DisplayName.ToUpperInvariant(),
                        SlotLabel = $"SLOT {option.TargetSlotIndex + 1}",
                        LevelNow = $"LV {w.Level}",
                        LevelNext = $"LV {w.Level + 1}",
                        StatLabels = new[] { "DMG", "RPM", "AMMO" },
                        StatNow = StatValues(w.Data, w.Level),
                        StatNext = StatValues(w.Data, w.Level + 1),
                    };
                }

                default:
                    throw new System.Exception($"[PickShoot] UpgradeCardPainter: 알 수 없는 Kind {option.Kind}");
            }
        }

        // 카드에 싣는 3개 스탯: 공격력(다발이면 X발수), 분당 발사 수(RPM), 최대 탄약. 모두 정수 표기.
        static string[] StatValues(WeaponData d, int level)
        {
            int dmg = Weapon.DamageAtLevel(d, level);
            string dmgText = d.ProjectilesPerShot > 1 ? $"{dmg}X{d.ProjectilesPerShot}" : dmg.ToString();
            int rpm = Mathf.RoundToInt(60f / Weapon.CooldownAtLevel(d, level));
            return new[] { dmgText, rpm.ToString(), Weapon.MaxAmmoAtLevel(d, level).ToString() };
        }

        // "지금 → 다음" 값 묶음의 폭. next가 null이면 now만.
        static int ValueWidth(PixelUiSkin skin, string now, string next, int textScale)
        {
            int w = PixelGui.MeasureText(skin.FontPlain, now, textScale);
            if (next == null) return w;
            int iconSize = skin.TileSize * textScale;
            int space = skin.FontPlain.spaceWidth * textScale;
            return w + space + iconSize + space + PixelGui.MeasureText(skin.FontPlain, next, textScale);
        }

        static void DrawValueRight(PixelUiSkin skin, string now, string next, int rightX, int y, int textScale)
        {
            if (next == null)
            {
                PixelGui.DrawTextRight(skin.FontPlain, now, rightX, y, textScale);
                return;
            }
            int iconSize = skin.TileSize * textScale;
            int space = skin.FontPlain.spaceWidth * textScale;
            int nextW = PixelGui.MeasureText(skin.FontPlain, next, textScale);
            int nowW = PixelGui.MeasureText(skin.FontPlain, now, textScale);
            int x = rightX - (nowW + space + iconSize + space + nextW);
            PixelGui.DrawText(skin.FontPlain, now, x, y, textScale);
            x += nowW + space;
            // 위쪽 화살표 아이콘을 시계방향 90° 돌려 오른쪽 화살표로 쓴다.
            PixelGui.DrawSpriteRotated(skin.IconArrowUp, new Rect(x, y, iconSize, iconSize), 90f);
            x += iconSize + space;
            PixelGui.DrawText(skin.FontPlain, next, x, y, textScale);
        }

        static int CeilToMultiple(int value, int multiple)
        {
            return ((value + multiple - 1) / multiple) * multiple;
        }
    }
}
