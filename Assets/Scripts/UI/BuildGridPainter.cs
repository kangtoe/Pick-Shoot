using System.Collections.Generic;
using UnityEngine;

namespace PickShoot
{
    // 현재 러닝의 빌드를 타일 격자(ItemTilePainter)로 (2026-10-06, 그전의 두 줄 띠 BuildStripPainter를 대체).
    // 브로타토식 — 줄마다 위에 제목과 수량(`WEAPONS (7/12)`·`RELICS (5)`)을 글자 배율 일반 글꼴로 격자 왼쪽 끝에 맞춰 두고, 그 아래 타일.
    // 무기 격자는 슬롯 순서로 보유 자루만 — 남은 슬롯 수는 제목의 `n/12`가 말한다. 처음엔 빈 슬롯을 어두운 타일로 12칸 채웠는데 제목에
    // 수량이 붙으면서 같은 정보의 반복이라 뺐다 (2026-10-06, 같은 날 뒤집음). 격자의 왼쪽 끝은 상태 열의 왼쪽 끝이라 자루가 늘어도 움직이지 않는다.
    // 유물 격자는 종류별(처음 얻은 순서) 한 칸씩 무기와 같은 열 수에 왼쪽부터 채우고 넘치면 다음 줄 — 종류가 언커먼·레어로 늘어도 접힌다. 없어도 한 줄 자리는 둔다.
    // 첫 칸은 늘 캐릭터다 (2026-10-14, Characters §4) — 캐릭터를 러닝 시작에 받는 유물로 보고 같은 줄에 둔다. 그림은 캐릭터의 아이콘(CharacterData.Icon),
    // 배지는 `X1`, 제목 수량에도 센다. 러닝 중 능력·보너스를 다시 볼 곳이 여기뿐이다 (보기 카드 UpgradeCardPainter.DrawOwnedCharacter).
    // 보상 화면(UpgradeManager)과 일시정지 화면(PauseScreen)이 같은 상태 열(StatusColumnPainter)에 패널 배율 6열로 둘 다 둔다 — 열 수는 그리는 쪽이 넘기고,
    // 무기도 그 열 수에서 다음 줄로 접힌다 (보상 화면은 2026-10-14 가로 카드 배치부터, 그전엔 카드 밑 12열 한 줄씩. 일시정지 화면은 2026-10-17부터, 그전엔
    // 12열 — 무기가 한 줄이었다). 10월 4일 "보상 화면에는 두지 않는다"를 2026-10-07에 뒤집은 것 — 제목의 남은 슬롯 수와 호버(보유 자루와 카드 비교)로 줄에
    // 기능이 생겼다 (Roadmap).
    // 패널 배경은 없다 — 어두운 막 위에 바로 그린다. 줄 제목은 "총과 열쇠라 그림으로 갈린다"며 두지 않다가 수량이 붙어 쓸모가 생기면서 넣었다.
    // 무기 줄은 탄창을 보이지 않는다 — 카드 화면은 sweep 직후라 늘 가득하고, 여기서 필요한 것은 종류와 레벨뿐이다 (Weapons §3).
    // 호버한 타일을 돌려주고, 그 보기 카드(UpgradeCardPainter.DrawOwnedWeapon·DrawOwnedRelic·DrawOwnedCharacter)는 화면이 맨 마지막에 그린다 (2026-10-12, 그전엔 툴팁).
    public static class BuildGridPainter
    {
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

        // 수량은 리롤 버튼의 `REROLL (2)`와 같은 괄호 꼴. 무기는 자루 수 / 최대 슬롯, 유물은 타일 수(캐릭터 + 유물 종류 수).
        public static string WeaponTitle(PlayerWeapons weapons)
        {
            return $"WEAPONS ({weapons.Slots.Count}/{PlayerWeapons.MaxSlots})";
        }

        public static string RelicTitle(PlayerStats stats)
        {
            return $"RELICS ({RelicCellCount(stats)})";
        }

        // 그 폭에 드는 최대 열 수 (상태 열 768px · 패널 배율 → 6).
        public static int ColumnsFitting(int scale, int width)
        {
            int pitch = ItemTilePainter.Size(scale) + ColumnGapTexels * scale;
            return (width + ColumnGapTexels * scale) / pitch;
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

        // height 안에 드는 줄 수 (상태 열의 유물 격자 자리).
        public static int RowsFitting(PixelUiSkin skin, int scale, int height)
        {
            return (height + RowGapTexels * scale) / RowPitch(skin, scale);
        }

        // 보유 유물의 종류 (처음 얻은 순서).
        public static List<RelicData> RelicKinds(PlayerStats stats)
        {
            var kinds = new List<RelicData>();
            foreach (var r in stats.Relics)
                if (!kinds.Contains(r)) kinds.Add(r);
            return kinds;
        }

        // 유물 격자의 칸 수 — 첫 칸의 캐릭터 + 유물 종류 수. 캐릭터가 없으면 예외 — 격자는 러닝 시작(캐릭터 고르기) 뒤에만 그린다.
        public static int RelicCellCount(PlayerStats stats)
        {
            if (stats.Character == null)
                throw new System.Exception("[PickShoot] BuildGridPainter: 캐릭터가 없음. 빌드 격자는 러닝 시작 화면에서 캐릭터를 고른 뒤에만 그린다.");
            return 1 + RelicKinds(stats).Count;
        }

        // 칸 수가 차지하는 줄 수. 한 줄 자리는 늘 있다 — 첫 칸이 캐릭터다.
        public static int RelicRows(int cells, int columns)
        {
            return Mathf.Max(1, (cells + columns - 1) / columns);
        }

        // 무기 격자가 차지할 줄 수 — 최대 슬롯 기준이라 자루가 늘어도 아래 유물 구역이 움직이지 않는다.
        public static int WeaponRows(int columns)
        {
            return (PlayerWeapons.MaxSlots + columns - 1) / columns;
        }

        // 무기 격자 (보유 자루만, 슬롯 순서로 columns칸마다 다음 줄). (x, y)는 격자 좌상단. 반환: 마우스가 올라간 자루의 슬롯 인덱스, 없으면 -1. hovered는 그 타일.
        public static int DrawWeaponGrid(PixelUiSkin skin, PlayerWeapons weapons, int x, int y, int scale, int columns, Vector2 mouse, out Rect hovered)
        {
            var slots = weapons.Slots;
            int pitch = ItemTilePainter.Size(scale) + ColumnGapTexels * scale;
            int rowPitch = RowPitch(skin, scale);
            int result = -1;
            hovered = default;
            for (int i = 0; i < slots.Count; i++)
            {
                int tx = x + (i % columns) * pitch;
                int ty = y + (i / columns) * rowPitch;
                var w = slots[i];
                ItemTilePainter.Draw(skin, w.Data.WeaponSprite, ItemInfoText.WeaponBadge(w.Level), ItemInfoText.ModMark(w.Mod), tx, ty, scale);
                var r = TileRect(skin, tx, ty, scale);
                if (r.Contains(mouse))
                {
                    result = i;
                    hovered = r;
                }
            }
            return result;
        }

        // 유물 격자 — 첫 칸 캐릭터, 그 뒤 유물 종류. maxRows를 넘치면 예외. 반환: 마우스가 올라간 유물, 없으면 null. characterHovered는 첫 칸(캐릭터)에 올라갔는가.
        // hovered는 그 타일.
        public static RelicData DrawRelicGrid(PixelUiSkin skin, PlayerStats stats, int x, int y, int scale, int columns, int maxRows, Vector2 mouse, out bool characterHovered, out Rect hovered)
        {
            int cells = RelicCellCount(stats);
            var kinds = RelicKinds(stats);
            int rows = RelicRows(cells, columns);
            if (rows > maxRows)
                throw new System.Exception($"[PickShoot] BuildGridPainter: 유물 격자 {cells}칸(캐릭터 + 유물 {kinds.Count}종)이 {maxRows}줄({maxRows * columns}칸)을 넘음. 그 화면의 자리 한계 — 종류가 늘면 접는 방식을 정할 것 (Relics §5).");
            int pitch = ItemTilePainter.Size(scale) + ColumnGapTexels * scale;
            int rowPitch = RowPitch(skin, scale);
            RelicData result = null;
            characterHovered = false;
            hovered = default;
            for (int i = 0; i < cells; i++)
            {
                int tx = x + (i % columns) * pitch;
                int ty = y + (i / columns) * rowPitch;
                var relic = i == 0 ? null : kinds[i - 1];
                if (relic == null)
                    ItemTilePainter.Draw(skin, stats.Character.Icon, ItemInfoText.RelicBadge(1), tx, ty, scale);
                else
                    ItemTilePainter.Draw(skin, relic.Icon, ItemInfoText.RelicBadge(stats.CountOf(relic)), tx, ty, scale);
                var r = TileRect(skin, tx, ty, scale);
                if (r.Contains(mouse))
                {
                    result = relic;
                    characterHovered = relic == null;
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
