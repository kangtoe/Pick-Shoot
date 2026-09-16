using UnityEngine;

namespace PickShoot
{
    // OnGUI 안에서 스프라이트 시트 조각을 정수 배율로 그리는 도우미. 좌표·크기는 모두 화면 픽셀.
    // scale은 텍셀 1개가 차지하는 화면 픽셀 수. 정수만 써서 픽셀아트가 뭉개지지 않게 한다.
    public static class PixelGui
    {
        // 화면 높이를 기준 높이(텍셀)로 나눈 정수 배율. 1080p·기준 270 → 4.
        public static int ScaleForScreen(int referenceHeightTexels)
        {
            if (referenceHeightTexels <= 0)
                throw new System.Exception($"[PickShoot] PixelGui.ScaleForScreen: referenceHeightTexels가 0 이하 ({referenceHeightTexels}).");
            return Mathf.Max(1, Mathf.RoundToInt(Screen.height / (float)referenceHeightTexels));
        }

        public static void DrawSprite(Sprite sprite, Rect screenRect)
        {
            DrawSpritePart(sprite, screenRect, 0f, 0f, 1f, 1f);
        }

        // 화면 좌표 기준 시계방향 회전. 위쪽 화살표 아이콘을 90°로 돌리면 오른쪽 화살표가 된다.
        public static void DrawSpriteRotated(Sprite sprite, Rect screenRect, float degrees)
        {
            var saved = GUI.matrix;
            GUIUtility.RotateAroundPivot(degrees, screenRect.center);
            DrawSprite(sprite, screenRect);
            GUI.matrix = saved;
        }

        // 9-slice 패널. 모서리는 타일 원본 크기, 변·중앙은 타일을 반복해 채운다 (늘리지 않음).
        public static void DrawNineSlice(PixelUiSkin.NineSlice slice, Rect r, int tileSize, int scale)
        {
            float t = tileSize * scale;
            if (r.width < 2f * t || r.height < 2f * t)
                throw new System.Exception($"[PickShoot] PixelGui.DrawNineSlice: 영역({r.width}x{r.height})이 모서리 2개({2f * t})보다 작음.");
            var tiles = slice.tiles;
            float innerW = r.width - 2f * t;
            float innerH = r.height - 2f * t;

            DrawSprite(tiles[0], new Rect(r.x, r.y, t, t));
            TileFill(tiles[1], new Rect(r.x + t, r.y, innerW, t), t);
            DrawSprite(tiles[2], new Rect(r.xMax - t, r.y, t, t));

            TileFill(tiles[3], new Rect(r.x, r.y + t, t, innerH), t);
            TileFill(tiles[4], new Rect(r.x + t, r.y + t, innerW, innerH), t);
            TileFill(tiles[5], new Rect(r.xMax - t, r.y + t, t, innerH), t);

            DrawSprite(tiles[6], new Rect(r.x, r.yMax - t, t, t));
            TileFill(tiles[7], new Rect(r.x + t, r.yMax - t, innerW, t), t);
            DrawSprite(tiles[8], new Rect(r.xMax - t, r.yMax - t, t, t));
        }

        // 바를 양 끝 타일 반쪽씩으로 줄일 때의 최소 폭(텍셀). 반쪽 4텍셀 = 흰 선 1 + 테두리 2 + 안쪽 1.
        public const int MinBarWidthTexels = 8;

        // 가로 바. 양 끝은 타일 원본 크기, 가운데는 mid 타일을 반복해 채운다.
        // 폭이 타일 2개보다 좁으면 왼쪽 끝 타일의 왼쪽 부분과 오른쪽 끝 타일의 오른쪽 부분을 반씩 잘라 붙인다 (MinBarWidthTexels 이상).
        // cutRows(짝수)만큼 타일 가운데 행을 잘라내 세로로 얇게 그릴 수 있다 — 위·아래 (tileSize - cutRows)/2행씩만 그린다.
        // r.height는 (tileSize - cutRows) * scale이어야 한다.
        public static void DrawBarStrip(PixelUiSkin.BarStrip strip, Rect r, int tileSize, int scale, int cutRows = 0)
        {
            if (cutRows < 0 || cutRows >= tileSize || (cutRows & 1) != 0)
                throw new System.Exception($"[PickShoot] PixelGui.DrawBarStrip: cutRows({cutRows})는 0 이상 {tileSize} 미만의 짝수여야 함.");
            float t = tileSize * scale;
            if (r.width < MinBarWidthTexels * scale)
                throw new System.Exception($"[PickShoot] PixelGui.DrawBarStrip: 폭({r.width})이 최소 폭({MinBarWidthTexels * scale})보다 작음.");
            int keep = (tileSize - cutRows) / 2;
            float expectedH = 2 * keep * scale;
            if (!Mathf.Approximately(r.height, expectedH))
                throw new System.Exception($"[PickShoot] PixelGui.DrawBarStrip: 높이({r.height})가 (tileSize - cutRows) * scale({expectedH})와 다름.");
            float keepV = keep / (float)tileSize;
            float sliceH = keep * scale;
            DrawStripRow(strip, new Rect(r.x, r.y, r.width, sliceH), t, 0f, keepV);
            DrawStripRow(strip, new Rect(r.x, r.y + sliceH, r.width, sliceH), t, 1f - keepV, 1f);
        }

        // 바 한 줄: 타일의 세로 구간 [top, bottom](0~1)만 잘라 왼쪽 끝·가운데 반복·오른쪽 끝을 그린다.
        static void DrawStripRow(PixelUiSkin.BarStrip strip, Rect r, float t, float top, float bottom)
        {
            if (r.width < 2f * t)
            {
                float half = r.width / 2f;
                DrawSpritePart(strip.left, new Rect(r.x, r.y, half, r.height), 0f, top, half / t, bottom);
                DrawSpritePart(strip.right, new Rect(r.x + half, r.y, half, r.height), 1f - half / t, top, 1f, bottom);
                return;
            }
            DrawSpritePart(strip.left, new Rect(r.x, r.y, t, r.height), 0f, top, 1f, bottom);
            for (float x = r.x + t; x < r.xMax - t; x += t)
            {
                float w = Mathf.Min(t, r.xMax - t - x);
                DrawSpritePart(strip.mid, new Rect(x, r.y, w, r.height), 0f, top, w / t, bottom);
            }
            DrawSpritePart(strip.right, new Rect(r.xMax - t, r.y, t, r.height), 0f, top, 1f, bottom);
        }

        public static int MeasureText(PixelUiSkin.BitmapFont font, string text, int scale)
        {
            int width = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (i > 0) width += font.letterSpacing;
                width += AdvanceTexels(font, text[i]);
            }
            return width * scale;
        }

        // (x, y)는 텍스트 줄의 좌상단 픽셀. 줄 높이는 font.lineHeight * scale.
        public static void DrawText(PixelUiSkin.BitmapFont font, string text, int x, int y, int scale)
        {
            int penX = x;
            int tileTexels = font.lineHeight;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (i > 0) penX += font.letterSpacing * scale;
                if (c == ' ')
                {
                    penX += font.spaceWidth * scale;
                    continue;
                }
                int g = GlyphIndex(font, c);
                int left = font.glyphLeft[g];
                int width = font.glyphWidth[g];
                DrawSpritePart(font.glyphs[g],
                    new Rect(penX, y, width * scale, tileTexels * scale),
                    left / (float)tileTexels, 0f, (left + width) / (float)tileTexels, 1f);
                penX += width * scale;
            }
        }

        public static void DrawTextCentered(PixelUiSkin.BitmapFont font, string text, int centerX, int y, int scale)
        {
            DrawText(font, text, centerX - MeasureText(font, text, scale) / 2, y, scale);
        }

        public static void DrawTextRight(PixelUiSkin.BitmapFont font, string text, int rightX, int y, int scale)
        {
            DrawText(font, text, rightX - MeasureText(font, text, scale), y, scale);
        }

        static int AdvanceTexels(PixelUiSkin.BitmapFont font, char c)
        {
            if (c == ' ') return font.spaceWidth;
            return font.glyphWidth[GlyphIndex(font, c)];
        }

        static int GlyphIndex(PixelUiSkin.BitmapFont font, char c)
        {
            int g = font.characters.IndexOf(c);
            if (g < 0)
                throw new System.Exception($"[PickShoot] PixelGui: 비트맵 글꼴에 없는 문자 '{c}' (U+{(int)c:X4}). 사용 가능: \"{font.characters}\" 및 공백.");
            return g;
        }

        // 영역을 타일 크기 t로 반복해 채운다. 끝에 남는 부분은 타일의 좌상단 조각만 잘라 그린다.
        static void TileFill(Sprite sprite, Rect area, float t)
        {
            if (area.width <= 0f || area.height <= 0f) return;
            for (float y = area.y; y < area.yMax; y += t)
            {
                float h = Mathf.Min(t, area.yMax - y);
                for (float x = area.x; x < area.xMax; x += t)
                {
                    float w = Mathf.Min(t, area.xMax - x);
                    DrawSpritePart(sprite, new Rect(x, y, w, h), 0f, 0f, w / t, h / t);
                }
            }
        }

        // 스프라이트의 일부를 그린다. left/top/right/bottom은 스프라이트 사각형 기준 0~1이며 top=0이 위쪽.
        // GUI.DrawTextureWithTexCoords의 UV는 원점이 좌하단이라 세로를 뒤집어 넘긴다.
        static void DrawSpritePart(Sprite sprite, Rect screenRect, float left, float top, float right, float bottom)
        {
            var tex = sprite.texture;
            var tr = sprite.textureRect;
            float u0 = (tr.x + tr.width * left) / tex.width;
            float u1 = (tr.x + tr.width * right) / tex.width;
            float v0 = (tr.y + tr.height * (1f - bottom)) / tex.height;
            float v1 = (tr.y + tr.height * (1f - top)) / tex.height;
            GUI.DrawTextureWithTexCoords(screenRect, tex, new Rect(u0, v0, u1 - u0, v1 - v0), true);
        }
    }
}
