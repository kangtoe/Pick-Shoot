using System.Collections.Generic;
using UnityEngine;

namespace PickShoot
{
    // 타일(ItemTilePainter)에 마우스를 올렸을 때 뜨는 상세 패널 (2026-10-06). 회색 9-slice 패널(패널 배율) 안에 제목(아웃라인 글꼴)과 글자 배율 문구.
    // 내용은 사실 묶음(facts)과 설명 문단(notes)으로 받는다 — 사실 항목(`DMG 15`)은 줄을 나누지 않고 폭에 맞춰 여러 개를 한 줄에 채우고, 문단은 단어 단위로 접는다.
    // 폭은 내용에 맞추되 카드 폭(7타일)이 상한, 높이는 줄 수에서 — 크기를 고정하지 않는 점이 카드와 다르다. 툴팁은 한 번에 하나뿐이고 자리가 정해져 있지 않아서다.
    // 10월 4일 롤백한 "카드 아래 고정 상세 패널"(3줄 한계)과 다른 점이 이것이고, 글자 배율은 2를 지켜 720p 가독성(②안이 걸린 문제)을 피한다 (Roadmap).
    // 자리는 기준 사각형 위 가운데, 위에 자리가 없으면 아래. 좌우는 캔버스 안으로 민다. 이웃 타일·카드 위로 뜨므로 화면의 다른 요소를 다 그린 뒤 맨 마지막에 그린다.
    public static class TooltipPainter
    {
        const int MaxWidthTiles = 7;       // 카드와 같은 폭이 상한
        const int PaddingTexels = 8;       // 패널 테두리 → 글자 (패널 배율)
        const int TitleGapTexels = 4;      // 제목 → 첫 줄 (글자 배율)
        const int LineGapTexels = 2;       // 줄 사이 (글자 배율)
        const int AnchorGapTexels = 2;     // 기준 사각형 → 패널 (패널 배율)
        const string ItemSeparator = "  "; // 한 줄에 놓인 사실 항목 사이

        // anchor는 툴팁이 가리키는 사각형 (캔버스 픽셀).
        public static void Draw(PixelUiSkin skin, PixelGui.CanvasScope canvas, Rect anchor, string title, IList<string> facts, IList<string> notes)
        {
            int scale = PixelGui.PanelScale;
            int textScale = PixelGui.TextScale;
            int tile = skin.TileSize * scale;
            var plain = skin.FontPlain;
            var outlined = skin.FontOutlined;
            int pad = PaddingTexels * scale;
            int maxInner = MaxWidthTiles * tile - 2 * pad;

            var lines = new List<string>();
            PackItems(plain, facts, textScale, maxInner, lines);
            foreach (var note in notes)
                lines.AddRange(PixelGui.WrapText(plain, note, textScale, maxInner));

            int innerW = PixelGui.MeasureText(outlined, title, textScale);
            if (innerW > maxInner)
                throw new System.Exception($"[PickShoot] TooltipPainter: 제목 '{title}'의 폭 {innerW}px가 허용 폭 {maxInner}px를 넘음.");
            foreach (var line in lines)
                innerW = Mathf.Max(innerW, PixelGui.MeasureText(plain, line, textScale));
            int lineH = plain.lineHeight * textScale;
            int titleH = outlined.lineHeight * textScale;
            int innerH = titleH;
            if (lines.Count > 0)
                innerH += TitleGapTexels * textScale + lines.Count * lineH + (lines.Count - 1) * LineGapTexels * textScale;
            // 9-slice는 모서리 2개(타일 2개)보다 넓어야 한다.
            int w = Mathf.Max(2 * tile, innerW + 2 * pad);
            int h = Mathf.Max(2 * tile, innerH + 2 * pad);

            int gap = AnchorGapTexels * scale;
            int x = Mathf.Clamp((int)anchor.center.x - w / 2, 0, canvas.Width - w);
            int y = (int)anchor.y - gap - h;
            if (y < 0) y = (int)anchor.yMax + gap;
            PixelGui.DrawNineSlice(skin.PanelGrey, new Rect(x, y, w, h), skin.TileSize, scale);

            int tx = x + pad;
            int ty = y + (h - innerH) / 2;
            PixelGui.DrawText(outlined, title, tx, ty, textScale);
            ty += titleH + TitleGapTexels * textScale;
            foreach (var line in lines)
            {
                PixelGui.DrawText(plain, line, tx, ty, textScale);
                ty += lineH + LineGapTexels * textScale;
            }
        }

        // 사실 항목을 왼쪽부터 채워, 다음 항목을 더하면 maxWidth를 넘길 때 줄을 바꾼다. 항목 하나가 maxWidth보다 넓으면 예외 — 항목 안에서 줄을 나누지 않는다.
        static void PackItems(PixelUiSkin.BitmapFont font, IList<string> items, int scale, int maxWidth, List<string> lines)
        {
            string line = "";
            foreach (var item in items)
            {
                if (PixelGui.MeasureText(font, item, scale) > maxWidth)
                    throw new System.Exception($"[PickShoot] TooltipPainter: 항목 '{item}'의 폭이 허용 폭 {maxWidth}px를 넘음. 문구를 줄일 것.");
                string candidate = line.Length == 0 ? item : line + ItemSeparator + item;
                if (PixelGui.MeasureText(font, candidate, scale) <= maxWidth)
                {
                    line = candidate;
                    continue;
                }
                lines.Add(line);
                line = item;
            }
            if (line.Length > 0) lines.Add(line);
        }
    }
}
