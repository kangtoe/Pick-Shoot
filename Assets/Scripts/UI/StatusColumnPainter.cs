using UnityEngine;

namespace PickShoot
{
    // 상태 열 (2026-10-17) — 위 띠(제목 줄 아웃라인 글꼴 패널 배율 + 부제 한 줄 글자 배율) 아래에 `STATS` 스탯 총계 표(StatPanelPainter, 2열),
    // `WEAPONS (7/12)` 무기 격자와 `RELICS (5)` 유물 격자(BuildGridPainter, 패널 배율·열 폭에 드는 6열)를 위에서부터 쌓는다.
    // 보상 화면(UpgradeManager)의 왼쪽 열과 일시정지 화면(PauseScreen)의 가운데 열이 같이 쓴다 — 일시정지 화면은 보상 화면 상태 열의 크기·y를 그대로 받아
    // x만 캔버스 가운데로 옮기므로(UpgradeManager.CenteredStatusColumn) 두 화면을 오가도 열 안의 배치가 같고 보기 카드도 같은 높이에 뜬다.
    // 그전 일시정지 화면은 12칸 폭의 가운데 묶음 — 띠 아래 4열 스탯 표, 12열 무기 한 줄과 유물 격자 — 이라 같은 정보가 두 화면에서 다른 꼴이었다.
    // 띠 오른쪽은 화면마다 다르다 — 보상 화면은 비고, 일시정지 화면은 웨이브·레벨·XP를 둔다.
    // 무기 격자는 최대 슬롯만큼 줄(6열 × 2줄)을 잡아 유물 구역이 움직이지 않고, 유물 격자는 열 아래 끝까지 드는 줄 수(2줄 12칸 — 캐릭터 빼고 유물 11종)가
    // 한계다. 넘치면 BuildGridPainter가 예외로 알린다.
    public static class StatusColumnPainter
    {
        // 띠 → STATS (패널 텍셀, 40px). 섹션 사이 STATS → WEAPONS → RELICS (패널 텍셀, 24px).
        const int HeaderGapTexels = 10;
        const int SectionGapTexels = 6;
        // 스탯 표 — 2열, 열 사이 패널 텍셀 12(48px). 8개가 4줄 — 왼쪽 공격·생존, 오른쪽 기동·회수 (PlayerStat 순서).
        const string StatsTitle = "STATS";
        const int StatColumns = 2;
        const int StatGutterTexels = 12;
        // 격자의 타일 배율 — 카드 전시 타일과 같은 패널 배율(112px), 열 폭(768px)에 6열 (2026-10-14).
        // 그전 보상 화면은 중간 배율 3(84px, 8열 × 유물 3줄 24칸)이었다 — 카드 밑에 12칸 한 줄씩 두던 때(2026-10-07~14)는 세로가 없어 패널 배율이 들지 않았다.
        // 글자 배율(2, 56px)은 레벨 배지 `LV5`(64px)가 타일보다 넓어 이웃 배지와 붙는다.
        const int TileScale = PixelGui.PanelScale;

        // 마우스가 올라간 타일. 보기 카드는 화면이 다른 요소를 다 그린 뒤 맨 마지막에 DrawViewCard로 띄운다 — 이웃 타일·카드·버튼 위로 뜬다.
        public struct Hover
        {
            public int Slot;          // 무기 슬롯 인덱스, 없으면 -1
            public Rect WeaponTile;
            public bool Character;    // 유물 격자 첫 칸(캐릭터)
            public RelicData Relic;
            public Rect RelicTile;

            public void DrawViewCard(PixelUiSkin skin, PixelGui.CanvasScope canvas, PlayerWeapons weapons, PlayerStats stats)
            {
                if (Slot >= 0)
                    UpgradeCardPainter.DrawOwnedWeapon(skin, canvas, weapons.Slots[Slot], stats, WeaponTile);
                else if (Character)
                    UpgradeCardPainter.DrawOwnedCharacter(skin, canvas, stats.Character, RelicTile);
                else if (Relic != null)
                    UpgradeCardPainter.DrawOwnedRelic(skin, canvas, Relic, stats.CountOf(Relic), RelicTile);
            }
        }

        // 띠의 높이 (96px).
        public static int HeaderHeight(PixelUiSkin skin)
        {
            return skin.FontOutlined.lineHeight * PixelGui.PanelScale + skin.FontPlain.lineHeight * PixelGui.TextScale;
        }

        // 열 전체 — 띠 왼쪽의 제목·부제, 그 아래 STATS 표·무기 격자·유물 격자. area는 띠를 포함한 열이고 유물 격자는 area 아래 끝까지 드는 줄 수만큼.
        // preview는 스탯 표의 미리보기(StatPanelPainter) — 없으면 null. 제목·부제가 열 폭을 넘으면 예외.
        public static Hover Draw(PixelUiSkin skin, RectInt area, string title, string subtitle, PlayerWeapons weapons, PlayerStats stats, PlayerStatModifiers? preview, Vector2 mouse)
        {
            int scale = PixelGui.PanelScale;
            int textScale = PixelGui.TextScale;
            if (PixelGui.MeasureText(skin.FontOutlined, title, scale) > area.width)
                throw new System.Exception($"[PickShoot] StatusColumnPainter: 제목 '{title}'이 상태 열 폭 {area.width}px를 넘음.");
            if (PixelGui.MeasureText(skin.FontPlain, subtitle, textScale) > area.width)
                throw new System.Exception($"[PickShoot] StatusColumnPainter: 부제 '{subtitle}'이 상태 열 폭 {area.width}px를 넘음.");
            int x = area.x;
            PixelGui.DrawText(skin.FontOutlined, title, x, area.y, scale);
            PixelGui.DrawText(skin.FontPlain, subtitle, x, area.y + skin.FontOutlined.lineHeight * scale, textScale);

            int rowTitleH = BuildGridPainter.TitleHeight(skin);
            int sectionGap = SectionGapTexels * scale;
            int y = area.y + HeaderHeight(skin) + HeaderGapTexels * scale;
            BuildGridPainter.DrawTitle(skin, StatsTitle, x, y);
            y += rowTitleH;
            StatPanelPainter.Draw(skin, stats.Total, preview, x, y, area.width, StatColumns, StatGutterTexels * scale);
            y += StatPanelPainter.Height(skin, StatColumns) + sectionGap;

            var hover = new Hover();
            int columns = BuildGridPainter.ColumnsFitting(TileScale, area.width);
            BuildGridPainter.DrawTitle(skin, BuildGridPainter.WeaponTitle(weapons), x, y);
            hover.Slot = BuildGridPainter.DrawWeaponGrid(skin, weapons, x, y + rowTitleH, TileScale, columns, mouse, out hover.WeaponTile);
            y += rowTitleH + BuildGridPainter.Height(skin, TileScale, BuildGridPainter.WeaponRows(columns)) + sectionGap;
            BuildGridPainter.DrawTitle(skin, BuildGridPainter.RelicTitle(stats), x, y);
            int relicTop = y + rowTitleH;
            int relicRows = BuildGridPainter.RowsFitting(skin, TileScale, area.yMax - relicTop);
            hover.Relic = BuildGridPainter.DrawRelicGrid(skin, stats, x, relicTop, TileScale, columns, relicRows, mouse, out hover.Character, out hover.RelicTile);
            return hover;
        }
    }
}
