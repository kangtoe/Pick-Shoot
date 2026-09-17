using UnityEngine;

namespace PickShoot
{
    // OnGUI 안에서 스프라이트 시트 조각을 그리는 도우미.
    // 픽셀 UI는 월드 카메라와 같은 규칙으로 보인다: 화면 세로가 항상 ReferenceHeightTexels(270)텍셀. 그래서 월드 스프라이트처럼
    // 해상도가 달라도 화면에서 차지하는 비율이 같고, 종횡비는 가로로 보이는 범위만 바꿀 뿐 크기에는 영향을 주지 않는다.
    // 구현: 세로 DesignHeight(1080)짜리 기준 캔버스 위에 정수 배율로 배치하고, BeginCanvas가 GUI.matrix로 캔버스 전체를
    // 화면 높이에 맞춰 늘리거나 줄인다 (1080p에서는 캔버스 = 화면). 이 클래스의 좌표·크기는 모두 캔버스 픽셀이고,
    // scale은 텍셀 1개가 차지하는 캔버스 픽셀 수(정수)다.
    public static class PixelGui
    {
        // 화면 세로에 해당하는 텍셀 수. 월드 카메라의 세로 텍셀 수(직교 크기 8.4375 × 2 × PPU 16)와 같다.
        public const int ReferenceHeightTexels = 270;
        // 기준 캔버스에서 텍셀 1개의 픽셀 수. 패널·큰 아이콘은 PanelScale(월드 텍셀과 같은 크기), 글자·HUD는 그 절반인 TextScale.
        public const int PanelScale = 4;
        public const int TextScale = 2;
        public const int DesignHeight = ReferenceHeightTexels * PanelScale;

        // BeginCanvas가 연 기준 캔버스. Dispose에서 GUI.matrix를 되돌리므로 using으로 감싼다.
        // 캔버스가 열려 있는 동안 Event.current.mousePosition도 캔버스 좌표다 (GUI.matrix의 역변환이 적용됨).
        public readonly struct CanvasScope : System.IDisposable
        {
            readonly Matrix4x4 savedMatrix;
            // 캔버스 1픽셀이 차지하는 화면 픽셀 수 (화면 높이 / DesignHeight).
            public readonly float PixelSize;
            // 캔버스 크기 (캔버스 픽셀). 세로는 항상 DesignHeight, 가로는 화면비에 따라 달라진다.
            public readonly int Width;
            public readonly int Height;

            internal CanvasScope(float pixelSize)
            {
                savedMatrix = GUI.matrix;
                PixelSize = pixelSize;
                Width = Mathf.RoundToInt(Screen.width / pixelSize);
                Height = DesignHeight;
                GUI.matrix = savedMatrix * Matrix4x4.Scale(new Vector3(pixelSize, pixelSize, 1f));
            }

            // Camera.WorldToScreenPoint 결과(화면 픽셀, 원점 좌하단)를 캔버스 좌표(원점 좌상단)로 바꾼다.
            public Vector2 FromScreenPoint(Vector3 screenPoint)
            {
                return new Vector2(screenPoint.x / PixelSize, (Screen.height - screenPoint.y) / PixelSize);
            }

            public void Dispose()
            {
                GUI.matrix = savedMatrix;
            }
        }

        // 기준 캔버스를 연다. 클릭 판정(GUI.Button)을 하는 화면은 Repaint뿐 아니라 모든 이벤트에서 열어야 한다.
        public static CanvasScope BeginCanvas()
        {
            return new CanvasScope(Screen.height / (float)DesignHeight);
        }

        // 단색 사각형. 시트 조각 없이 평면 색으로 칠하는 면(머리 위 게이지 등)에 쓴다.
        public static void FillRect(Rect screenRect, Color color)
        {
            var prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(screenRect, Texture2D.whiteTexture);
            GUI.color = prev;
        }

        public static void DrawSprite(Sprite sprite, Rect screenRect)
        {
            DrawSpritePart(sprite, screenRect, 0f, 0f, 1f, 1f);
        }

        // 화면 기준 시계방향 회전. 위쪽 화살표 아이콘을 90°로 돌리면 오른쪽 화살표가 된다.
        // GUIUtility.RotateAroundPivot은 피벗을 GUI.matrix 적용 뒤의 좌표로 해석해 캔버스 배율이 걸리면 어긋난다.
        // 그래서 회전을 캔버스 좌표에서 먼저 하고 그 바깥에 기존 행렬(캔버스 배율)이 걸리도록 직접 합성한다.
        public static void DrawSpriteRotated(Sprite sprite, Rect screenRect, float degrees)
        {
            var saved = GUI.matrix;
            Vector3 pivot = screenRect.center;
            GUI.matrix = saved * Matrix4x4.Translate(pivot) * Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, degrees)) * Matrix4x4.Translate(-pivot);
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
        // r.height는 타일 높이(tileSize * scale)여야 한다.
        public static void DrawBarStrip(PixelUiSkin.BarStrip strip, Rect r, int tileSize, int scale)
        {
            float t = tileSize * scale;
            if (r.width < MinBarWidthTexels * scale)
                throw new System.Exception($"[PickShoot] PixelGui.DrawBarStrip: 폭({r.width})이 최소 폭({MinBarWidthTexels * scale})보다 작음.");
            if (!Mathf.Approximately(r.height, t))
                throw new System.Exception($"[PickShoot] PixelGui.DrawBarStrip: 높이({r.height})가 타일 높이({t})와 다름.");
            if (r.width < 2f * t)
            {
                float half = r.width / 2f;
                DrawSpritePart(strip.left, new Rect(r.x, r.y, half, t), 0f, 0f, half / t, 1f);
                DrawSpritePart(strip.right, new Rect(r.x + half, r.y, half, t), 1f - half / t, 0f, 1f, 1f);
                return;
            }
            DrawSprite(strip.left, new Rect(r.x, r.y, t, t));
            for (float x = r.x + t; x < r.xMax - t; x += t)
            {
                float w = Mathf.Min(t, r.xMax - t - x);
                DrawSpritePart(strip.mid, new Rect(x, r.y, w, t), 0f, 0f, w / t, 1f);
            }
            DrawSprite(strip.right, new Rect(r.xMax - t, r.y, t, t));
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
