using System.Collections.Generic;
using UnityEngine;

namespace PickShoot
{
    // 현재 러닝의 빌드를 타일 격자(ItemTilePainter)로 (2026-10-06, 그전의 두 줄 띠 BuildStripPainter를 대체).
    // 브로타토식 — 줄마다 위에 제목과 수량(`WEAPONS (7/12)`·`RELICS (5)`)을 글자 배율 일반 글꼴로 격자 왼쪽 끝에 맞춰 두고, 그 아래 타일.
    // 무기 줄은 슬롯 순서로 보유 자루만 한 줄 — 남은 슬롯 수는 제목의 `n/12`가 말한다. 처음엔 빈 슬롯을 어두운 타일로 12칸 채웠는데 제목에
    // 수량이 붙으면서 같은 정보의 반복이라 뺐다 (2026-10-06, 같은 날 뒤집음). 줄의 왼쪽 끝은 12칸(PlayerWeapons.MaxSlots) 폭을 가운데 둔 자리라
    // 자루가 늘어도 줄이 움직이지 않는다.
    // 유물 격자는 종류별(처음 얻은 순서) 한 칸씩 같은 12열에 왼쪽부터 채우고 넘치면 다음 줄 — 종류가 레어·유니크로 늘어도 접힌다. 없어도 한 줄 자리는 둔다.
    // 일시정지 화면(PauseScreen)은 패널 배율로 두 격자를 다 보이고(전체 인벤토리), 보상 화면(UpgradeManager)은 타일 배율 3(배지 `LV5`가 들어가는 최소 타일)으로
    // 무기 줄·유물 줄을 카드 밑에 둘 다 둔다 (2026-10-07 — 그전엔 제목 배너·리롤 버튼이 세로를 차지해 그 화면 종류의 한 줄뿐이었다). 10월 4일 "보상 화면에는 두지 않는다"를
    // 뒤집은 것 — 제목의 남은 슬롯 수와 호버 툴팁(보유 자루와 카드 비교)으로 줄에 기능이 생겼다 (Roadmap).
    // 패널 배경은 없다 — 어두운 막 위에 바로 그린다. 줄 제목은 "총과 열쇠라 그림으로 갈린다"며 두지 않다가 수량이 붙어 쓸모가 생기면서 넣었다.
    // 무기 줄은 탄창을 보이지 않는다 — 카드 화면은 sweep 직후라 늘 가득하고, 여기서 필요한 것은 종류와 레벨뿐이다 (Weapons §3).
    // 호버한 타일을 돌려주고, 툴팁(TooltipPainter)은 화면이 맨 마지막에 그린다.
    public static class BuildGridPainter
    {
        public const int Columns = PlayerWeapons.MaxSlots;
        const int ColumnGapTexels = 4;   // 타일 사이 (타일 배율)
        const int RowGapTexels = 4;      // 줄 사이 (타일 배율)
        const int TitleGapTexels = 4;    // 제목 줄 아래 → 타일 (글자 배율)

        // 제목 줄이 차지하는 높이 (줄 + 타일까지 간격). 격자 배율과 무관하게 글자 배율.
        public static int TitleHeight(PixelUiSkin skin)
        {
            int ts = PixelGui.TextScale;
            return skin.FontPlain.lineHeight * ts + TitleGapTexels * ts;
        }

        // 격자 왼쪽 끝에 맞춘 제목. (x, y)는 제목 줄의 좌상단 = 격자 묶음의 좌상단.
        public static void DrawTitle(PixelUiSkin skin, string text, int x, int y)
        {
            PixelGui.DrawText(skin.FontPlain, text, x, y, PixelGui.TextScale);
        }

        // 수량은 리롤 버튼의 `REROLL (2)`와 같은 괄호 꼴. 무기는 자루 수 / 최대 슬롯, 유물은 종류 수(타일 수).
        public static string WeaponTitle(PlayerWeapons weapons)
        {
            return $"WEAPONS ({weapons.Slots.Count}/{PlayerWeapons.MaxSlots})";
        }

        public static string RelicTitle(PlayerStats stats)
        {
            return $"RELICS ({RelicKinds(stats).Count})";
        }

        // 12칸 폭. 무기 줄이 가득 찼을 때이자 유물 격자의 한 줄 — 격자 묶음을 가운데 두는 기준.
        public static int Width(int scale)
        {
            return Columns * ItemTilePainter.Size(scale) + (Columns - 1) * ColumnGapTexels * scale;
        }

        // 줄 위 가장자리 → 다음 줄 위 가장자리.
        public static int RowPitch(PixelUiSkin skin, int scale)
        {
            return ItemTilePainter.Size(scale) + RowGapTexels * scale;
        }

        // rows줄 격자의 높이.
        public static int Height(PixelUiSkin skin, int scale, int rows)
        {
            return rows * RowPitch(skin, scale) - RowGapTexels * scale;
        }

        // 12칸이 캔버스 폭을 넘으면 예외 — 패널 배율에서 1520px라 16:10(1728)까지 들어가고 4:3(1440)은 안 된다.
        public static void RequireFits(int scale, int canvasWidth)
        {
            int w = Width(scale);
            if (w > canvasWidth)
                throw new System.Exception($"[PickShoot] BuildGridPainter: 격자 폭 {w}px가 캔버스 폭 {canvasWidth}px를 넘음 (배율 {scale}). 열 수를 줄이거나 배율을 낮출 것.");
        }

        // 보유 유물의 종류 (처음 얻은 순서).
        public static List<RelicData> RelicKinds(PlayerStats stats)
        {
            var kinds = new List<RelicData>();
            foreach (var r in stats.Relics)
                if (!kinds.Contains(r)) kinds.Add(r);
            return kinds;
        }

        // 유물 종류 수가 차지하는 줄 수. 없어도 한 줄 — 첫 유물을 얻을 때 묶음이 움직이지 않게.
        public static int RelicRows(int kinds)
        {
            return Mathf.Max(1, (kinds + Columns - 1) / Columns);
        }

        // 무기 줄 (보유 자루만). (x, y)는 격자 좌상단. 반환: 마우스가 올라간 자루의 슬롯 인덱스, 없으면 -1. hovered는 그 타일.
        public static int DrawWeaponRow(PixelUiSkin skin, PlayerWeapons weapons, int x, int y, int scale, Vector2 mouse, out Rect hovered)
        {
            var slots = weapons.Slots;
            int pitch = ItemTilePainter.Size(scale) + ColumnGapTexels * scale;
            int result = -1;
            hovered = default;
            for (int i = 0; i < slots.Count; i++)
            {
                int tx = x + i * pitch;
                var w = slots[i];
                ItemTilePainter.Draw(skin, w.Data.WeaponSprite, ItemInfoText.WeaponBadge(w.Level), ItemInfoText.ModMark(w.Mod), tx, y, scale);
                var r = TileRect(skin, tx, y, scale);
                if (r.Contains(mouse))
                {
                    result = i;
                    hovered = r;
                }
            }
            return result;
        }

        // 유물 격자. maxRows를 넘치면 예외. 반환: 마우스가 올라간 유물, 없으면 null.
        public static RelicData DrawRelicGrid(PixelUiSkin skin, PlayerStats stats, int x, int y, int scale, int maxRows, Vector2 mouse, out Rect hovered)
        {
            var kinds = RelicKinds(stats);
            int rows = RelicRows(kinds.Count);
            if (rows > maxRows)
                throw new System.Exception($"[PickShoot] BuildGridPainter: 유물 종류 {kinds.Count}이 {maxRows}줄({maxRows * Columns}칸)을 넘음. 보상 화면의 한 줄 한계 — 종류가 늘면 접는 방식을 정할 것 (Relics §5).");
            int pitch = ItemTilePainter.Size(scale) + ColumnGapTexels * scale;
            int rowPitch = RowPitch(skin, scale);
            RelicData result = null;
            hovered = default;
            for (int i = 0; i < kinds.Count; i++)
            {
                int tx = x + (i % Columns) * pitch;
                int ty = y + (i / Columns) * rowPitch;
                ItemTilePainter.Draw(skin, kinds[i].Icon, ItemInfoText.RelicBadge(stats.CountOf(kinds[i])), tx, ty, scale);
                var r = TileRect(skin, tx, ty, scale);
                if (r.Contains(mouse))
                {
                    result = kinds[i];
                    hovered = r;
                }
            }
            return result;
        }

        static Rect TileRect(PixelUiSkin skin, int x, int y, int scale)
        {
            int size = ItemTilePainter.Size(scale);
            return new Rect(x, y, size, size);
        }
    }
}
