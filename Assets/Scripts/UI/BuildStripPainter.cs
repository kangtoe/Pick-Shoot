using System.Collections.Generic;
using UnityEngine;

namespace PickShoot
{
    // 현재 러닝의 빌드 두 줄 — 위 줄은 보유 무기(슬롯 순서, 작은 판 위 스프라이트 + LV n), 아래 줄은 보유 유물(종류별, 처음 얻은 순서, 아이콘 + Xn).
    // 일시정지 화면(PauseScreen)의 상태 카드 아래에 그린다. 보상 화면에는 두지 않는다 — 시도했다가 뺐다, 카드가 주인공이고 빌드는 ESC로 본다 (2026-10-04).
    // 패널 배경은 없다 — HUD 무기 줄처럼 어두운 막 위에 바로 그린다. 글자·HUD 배율(2)로 그리고, 글자는 아이콘 옆에 둔다 — 두 줄을 낮게 유지한다.
    // 줄은 각각 가운데 정렬. 줄 이름(WEAPONS·RELICS)은 두지 않는다 — 총과 열쇠라 그림으로 갈린다.
    // 무기 줄은 탄창을 보이지 않는다 — 카드 화면은 sweep 직후라 늘 가득하고, 여기서 필요한 것은 종류와 레벨뿐이다 (Weapons §3).
    // 폭: 무기 줄은 12자루 LV9에서 1080p 기준 1668px — 16:10(1728)까지 들어간다. 그래서 레벨은 HUD의 "LV 9"와 달리 공백 없는 "LV9"이고 간격도 좁다.
    public static class BuildStripPainter
    {
        const int IconTextGapTexels = 4;   // 아이콘(판) → 글자
        const int ItemGapTexels = 6;       // 항목 사이
        const int RowGapTexels = 4;        // 무기 줄 → 유물 줄

        // 글꼴에 있는 문자(A-Z 0-9)만.
        static string LevelLabel(int level) => $"LV{level}";
        static string CountLabel(int count) => $"X{count}";

        // 두 줄의 전체 높이 (캔버스 픽셀). 유물이 없어도 아래 줄 자리는 둔다 — 첫 유물을 얻을 때 묶음이 움직이지 않게.
        public static int Height(PixelUiSkin skin, int scale)
        {
            return (PixelGui.WeaponPlateHeightTexels + RowGapTexels + skin.TileSize) * scale;
        }

        // centerX·y는 묶음의 가로 중심과 위쪽 끝 (캔버스 픽셀). maxWidth를 넘치면 예외 — 슬롯 12·유물 종류가 늘어 한 줄에 못 들어가면 알린다.
        public static void Draw(PixelUiSkin skin, PlayerWeapons weapons, PlayerStats stats, int centerX, int y, int scale, int maxWidth)
        {
            var font = skin.FontPlain;
            int tile = skin.TileSize * scale;
            int lineH = font.lineHeight * scale;
            int iconGap = IconTextGapTexels * scale;
            int itemGap = ItemGapTexels * scale;

            // 무기 줄: 판(28×18텍셀) + 레벨. 스프라이트는 판 가운데, 글자는 판 높이의 세로 가운데.
            int plateW = PixelGui.WeaponPlateWidthTexels * scale;
            int plateH = PixelGui.WeaponPlateHeightTexels * scale;
            var slots = weapons.Slots;
            int rowW = 0;
            for (int i = 0; i < slots.Count; i++)
            {
                if (i > 0) rowW += itemGap;
                rowW += plateW + iconGap + PixelGui.MeasureText(font, LevelLabel(slots[i].Level), scale);
            }
            RequireFits(rowW, maxWidth, "무기 줄");
            int x = centerX - rowW / 2;
            for (int i = 0; i < slots.Count; i++)
            {
                var sprite = slots[i].Data.WeaponSprite;
                PixelGui.DrawWeaponPlate(skin, new Rect(x, y, plateW, plateH), scale);
                int sw = Mathf.RoundToInt(sprite.rect.width) * scale;
                int sh = Mathf.RoundToInt(sprite.rect.height) * scale;
                PixelGui.DrawSprite(sprite, new Rect(x + (plateW - sw) / 2, y + (plateH - sh) / 2, sw, sh));
                string level = LevelLabel(slots[i].Level);
                PixelGui.DrawText(font, level, x + plateW + iconGap, y + (plateH - lineH) / 2, scale);
                x += plateW + iconGap + PixelGui.MeasureText(font, level, scale) + itemGap;
            }

            // 유물 줄: 아이콘(타일 한 칸) + 개수. 종류별로 하나씩, 처음 얻은 순서.
            var kinds = new List<RelicData>();
            foreach (var r in stats.Relics)
                if (!kinds.Contains(r)) kinds.Add(r);
            if (kinds.Count == 0) return;

            int y2 = y + plateH + RowGapTexels * scale;
            rowW = 0;
            for (int i = 0; i < kinds.Count; i++)
            {
                if (i > 0) rowW += itemGap;
                rowW += tile + iconGap + PixelGui.MeasureText(font, CountLabel(stats.CountOf(kinds[i])), scale);
            }
            RequireFits(rowW, maxWidth, "유물 줄");
            x = centerX - rowW / 2;
            for (int i = 0; i < kinds.Count; i++)
            {
                var icon = kinds[i].Icon;
                int iw = Mathf.RoundToInt(icon.rect.width) * scale;
                int ih = Mathf.RoundToInt(icon.rect.height) * scale;
                PixelGui.DrawSprite(icon, new Rect(x + (tile - iw) / 2, y2 + (tile - ih) / 2, iw, ih));
                string count = CountLabel(stats.CountOf(kinds[i]));
                PixelGui.DrawText(font, count, x + tile + iconGap, y2 + (tile - lineH) / 2, scale);
                x += tile + iconGap + PixelGui.MeasureText(font, count, scale) + itemGap;
            }
        }

        static void RequireFits(int width, int maxWidth, string what)
        {
            if (width > maxWidth)
                throw new System.Exception($"[PickShoot] BuildStripPainter: {what}의 폭 {width}px가 허용 폭 {maxWidth}px를 넘음. 항목 간격을 줄이거나 줄을 나눌 것.");
        }
    }
}
