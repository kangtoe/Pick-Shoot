using System.Collections.Generic;
using UnityEngine;

namespace PickShoot
{
    // OnGUI 안에서 스프라이트 시트 조각을 그리는 도우미.
    // 픽셀 UI는 월드 카메라와 같은 규칙으로 보인다: 화면 세로가 항상 ReferenceHeightTexels(270)텍셀. 그래서 월드 스프라이트처럼
    // 해상도가 달라도 화면에서 차지하는 비율이 같다. 화면비는 16:9로 고정이다(ScreenLetterbox, 2026-10-14) — 캔버스는 늘 DesignWidth × DesignHeight
    // (1920×1080)이고 화면의 16:9 영역에 놓인다. 그전엔 캔버스 가로가 화면비를 따라 바뀌었다.
    // 구현: 기준 캔버스 위에 정수 배율로 배치하고, BeginCanvas가 GUI.matrix로 캔버스 전체를 16:9 영역의 높이에 맞춰 늘리거나 줄여 그 영역의 좌상단에
    // 옮긴다 (1080p에서는 캔버스 = 화면). 이 클래스의 좌표·크기는 모두 캔버스 픽셀이고, scale은 텍셀 1개가 차지하는 캔버스 픽셀 수(정수)다.
    public static class PixelGui
    {
        // 화면 세로에 해당하는 텍셀 수. 월드 카메라의 세로 텍셀 수(직교 크기 8.4375 × 2 × PPU 16)와 같다.
        public const int ReferenceHeightTexels = 270;
        // 기준 캔버스에서 텍셀 1개의 픽셀 수. 패널·큰 아이콘은 PanelScale(월드 텍셀과 같은 크기), 글자·HUD는 그 절반인 TextScale.
        public const int PanelScale = 4;
        public const int TextScale = 2;
        public const int DesignHeight = ReferenceHeightTexels * PanelScale;
        public const int DesignWidth = DesignHeight * ScreenLetterbox.AspectWidth / ScreenLetterbox.AspectHeight;

        // 지금 열려 있는 캔버스의 1픽셀이 차지하는 화면 픽셀 수. 캔버스 밖에서는 1. SnapToScreenPixels가 쓴다.
        static float currentPixelSize = 1f;

        // BeginCanvas가 연 기준 캔버스. Dispose에서 GUI.matrix를 되돌리므로 using으로 감싼다.
        // 캔버스가 열려 있는 동안 Event.current.mousePosition도 캔버스 좌표다 (GUI.matrix의 역변환이 적용됨).
        public readonly struct CanvasScope : System.IDisposable
        {
            readonly Matrix4x4 savedMatrix;
            readonly float savedPixelSize;
            // 캔버스 좌상단의 화면 위치 (화면 픽셀, 원점 좌상단 — 16:9 영역의 좌상단, 띠가 없으면 0). 정수라 화면 픽셀 맞춤(SnapToScreenPixels)이 그대로 맞는다.
            readonly Vector2Int origin;
            // 캔버스 1픽셀이 차지하는 화면 픽셀 수 (16:9 영역 높이 / DesignHeight).
            public readonly float PixelSize;
            // 캔버스 크기 (캔버스 픽셀) — 늘 DesignWidth × DesignHeight.
            public readonly int Width;
            public readonly int Height;

            internal CanvasScope(RectInt viewport)
            {
                savedMatrix = GUI.matrix;
                savedPixelSize = currentPixelSize;
                origin = viewport.position;
                PixelSize = viewport.height / (float)DesignHeight;
                Width = DesignWidth;
                Height = DesignHeight;
                GUI.matrix = savedMatrix * Matrix4x4.Translate(new Vector3(origin.x, origin.y, 0f)) * Matrix4x4.Scale(new Vector3(PixelSize, PixelSize, 1f));
                currentPixelSize = savedPixelSize * PixelSize;
            }

            // Camera.WorldToScreenPoint 결과(화면 픽셀, 원점 좌하단)를 캔버스 좌표(원점 좌상단)로 바꾼다.
            public Vector2 FromScreenPoint(Vector3 screenPoint)
            {
                return new Vector2((screenPoint.x - origin.x) / PixelSize, (Screen.height - screenPoint.y - origin.y) / PixelSize);
            }

            public void Dispose()
            {
                GUI.matrix = savedMatrix;
                currentPixelSize = savedPixelSize;
            }
        }

        // 기준 캔버스를 연다. 클릭 판정(GUI.Button)을 하는 화면은 Repaint뿐 아니라 모든 이벤트에서 열어야 한다.
        public static CanvasScope BeginCanvas()
        {
            return new CanvasScope(ScreenLetterbox.GuiRect);
        }

        // 화면을 덮는 막(어두운 막·피격 플래시)을 16:9 영역에만 — 띠는 늘 검다. 캔버스를 열기 전(GUI.matrix가 화면 픽셀일 때) 부른다.
        public static void FillScreen(Color color)
        {
            var r = ScreenLetterbox.GuiRect;
            var prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, r.height), Texture2D.whiteTexture);
            GUI.color = prev;
        }
        // 단색 사각형. 시트 조각 없이 평면 색으로 칠하는 면(머리 위 게이지 등)에 쓴다. 색은 그대로 쓰되 알파에는 GUI.color의 알파를 곱한다 — 시트 조각·글자처럼
        // 바깥에서 GUI.color 알파로 묶음 전체를 투명하게(시작 화면의 캐릭터 카드) 할 수 있게 (2026-10-13). RGB는 곱하지 않는다 — 어둡게 보일 판(리롤 버튼)은 호출자가 색에 곱한다.
        public static void FillRect(Rect screenRect, Color color)
        {
            var prev = GUI.color;
            GUI.color = new Color(color.r, color.g, color.b, color.a * prev.a);
            GUI.DrawTexture(screenRect, Texture2D.whiteTexture);
            GUI.color = prev;
        }

        public static void DrawSprite(Sprite sprite, Rect screenRect)
        {
            DrawSpritePart(sprite, screenRect, 0f, 0f, 1f, 1f);
        }

        // 무기 스프라이트 밑의 작은 판 (HUD 무기 줄). 크기는 텍셀 단위 고정 — 16텍셀 무기 그림이 여백을 두고 들어간다. 무기·유물 타일(ItemTilePainter)은 같은 그리기를 정사각 r로 쓴다.
        // 네 귀를 1텍셀씩 깎은 외곽선 색 사각형 위에 바탕색을 채운다. 귀를 깎은 모양은 가로로 긴 사각형과 세로로 긴 사각형을 겹쳐 만든다 —
        // 겹쳐 그리므로 비정수 배율에서도 조각 사이에 틈이 없다. 시트의 작은 판 조각은 흰 테두리·리벳이 총 그림과 겹쳐 어수선해 쓰지 않는다 (DEVELOPMENT §2).
        public const int WeaponPlateWidthTexels = 28;
        public const int WeaponPlateHeightTexels = 18;
        const int WeaponPlateOutlineTexels = 2;

        public static void DrawWeaponPlate(PixelUiSkin skin, Rect r, int scale)
        {
            DrawWeaponPlate(skin, r, scale, Color.white);
        }

        // tint는 판 색에 곱한다. 단색 사각형은 GUI.color의 RGB를 타지 않으므로, 어둡게 보일 판(리롤 버튼의 0회)은 막으로 덮는 대신 색을 곱한다 — 직사각형 막은
        // 네 귀의 깎인 자리까지 덮어 모서리가 각져 보였다 (2026-10-08).
        // 테두리 여섯 조각과 바탕이 서로 겹치지 않게 그린다 (2026-10-13) — 테두리 모양 위에 바탕을 덮어 그리면 GUI.color 알파로 투명하게 할 때 바탕 밑의 테두리 색이 비친다.
        public static void DrawWeaponPlate(PixelUiSkin skin, Rect r, int scale, Color tint)
        {
            int o = WeaponPlateOutlineTexels * scale;
            int s = scale;
            var line = skin.WeaponPlateOutline * tint;
            FillRect(new Rect(r.x + s, r.y, r.width - 2 * s, s), line);                 // 맨 윗줄 (귀가 깎인 줄)
            FillRect(new Rect(r.x, r.y + s, r.width, o - s), line);                     // 위 테두리 나머지
            FillRect(new Rect(r.x, r.y + o, o, r.height - 2 * o), line);                // 왼쪽
            FillRect(new Rect(r.xMax - o, r.y + o, o, r.height - 2 * o), line);         // 오른쪽
            FillRect(new Rect(r.x, r.yMax - o, r.width, o - s), line);                  // 아래 테두리 나머지
            FillRect(new Rect(r.x + s, r.yMax - s, r.width - 2 * s, s), line);          // 맨 아랫줄
            FillRect(new Rect(r.x + o, r.y + o, r.width - 2 * o, r.height - 2 * o), skin.WeaponPlateFill * tint);
        }

        // 네 귀를 1텍셀씩 깎은 사각형을 한 색으로 채운다 — 판의 바깥에 두르는 흰 선(카드 호버·리롤 버튼·시작 화면 타일)이 쓴다. 맨 윗줄·가운데·맨 아랫줄 세 조각이
        // 겹치지 않아 반투명(GUI.color 알파)에도 고르다 (2026-10-13. 그전엔 가로로 긴 사각형과 세로로 긴 사각형을 겹쳐 불투명 색만 쓸 수 있었다).
        public static void FillPlateShape(Rect r, int scale, Color color)
        {
            FillRect(new Rect(r.x + scale, r.y, r.width - 2 * scale, scale), color);
            FillRect(new Rect(r.x, r.y + scale, r.width, r.height - 2 * scale), color);
            FillRect(new Rect(r.x + scale, r.yMax - scale, r.width - 2 * scale, scale), color);
        }

        // 좌우 반전. 화면 오른쪽에 거울상으로 놓는 조각(HUD 무기 스프라이트)용 — UV의 가로 구간을 뒤집어 넘긴다.
        public static void DrawSpriteMirrored(Sprite sprite, Rect screenRect)
        {
            DrawSpritePart(sprite, screenRect, 1f, 0f, 0f, 1f);
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

            DrawTile(tiles[0], new Rect(r.x, r.y, t, t), 0f, 0f, 1f, 1f);
            TileFill(tiles[1], new Rect(r.x + t, r.y, innerW, t), t);
            DrawTile(tiles[2], new Rect(r.xMax - t, r.y, t, t), 0f, 0f, 1f, 1f);

            TileFill(tiles[3], new Rect(r.x, r.y + t, t, innerH), t);
            TileFill(tiles[4], new Rect(r.x + t, r.y + t, innerW, innerH), t);
            TileFill(tiles[5], new Rect(r.xMax - t, r.y + t, t, innerH), t);

            DrawTile(tiles[6], new Rect(r.x, r.yMax - t, t, t), 0f, 0f, 1f, 1f);
            TileFill(tiles[7], new Rect(r.x + t, r.yMax - t, innerW, t), t);
            DrawTile(tiles[8], new Rect(r.xMax - t, r.yMax - t, t, t), 0f, 0f, 1f, 1f);
        }

        // 바를 양 끝 타일 반쪽씩으로 줄일 때의 최소 폭(텍셀). 반쪽 4텍셀 = 흰 선 1 + 테두리 2 + 안쪽 1.
        public const int MinBarWidthTexels = 8;

        // 가로 바. 양 끝은 타일 원본 크기, 가운데는 mid 타일을 반복해 채운다.
        // 폭이 타일 2개보다 좁으면 왼쪽 끝 타일의 왼쪽 부분과 오른쪽 끝 타일의 오른쪽 부분을 반씩 잘라 붙인다 (MinBarWidthTexels 이상).
        // r.height는 타일 높이(tileSize * scale)여야 한다.
        public static void DrawBarStrip(PixelUiSkin.BarStrip strip, Rect r, int tileSize, int scale)
        {
            DrawBarStrip(strip, r, tileSize, scale, r.width);
        }

        // visibleWidth: 바의 왼쪽 끝에서 이 폭까지만 그리고 나머지는 잘라낸다 (비율 게이지의 채움용). 배치는 r 전체 기준 그대로라
        // 같은 r에 그린 프레임과 타일 이음매·텍셀 위치가 일치한다. GUI.BeginGroup으로 자르면 그룹 원점이 따로 반올림돼 프레임과 1px 어긋날 수 있다.
        public static void DrawBarStrip(PixelUiSkin.BarStrip strip, Rect r, int tileSize, int scale, float visibleWidth)
        {
            float t = tileSize * scale;
            if (r.width < MinBarWidthTexels * scale)
                throw new System.Exception($"[PickShoot] PixelGui.DrawBarStrip: 폭({r.width})이 최소 폭({MinBarWidthTexels * scale})보다 작음.");
            if (!Mathf.Approximately(r.height, t))
                throw new System.Exception($"[PickShoot] PixelGui.DrawBarStrip: 높이({r.height})가 타일 높이({t})와 다름.");
            float clipRight = r.x + visibleWidth;
            if (r.width < 2f * t)
            {
                float half = r.width / 2f;
                DrawTileClipped(strip.left, new Rect(r.x, r.y, half, t), 0f, half / t, clipRight);
                DrawTileClipped(strip.right, new Rect(r.x + half, r.y, half, t), 1f - half / t, 1f, clipRight);
                return;
            }
            DrawTileClipped(strip.left, new Rect(r.x, r.y, t, t), 0f, 1f, clipRight);
            for (float x = r.x + t; x < r.xMax - t; x += t)
            {
                float w = Mathf.Min(t, r.xMax - t - x);
                DrawTileClipped(strip.mid, new Rect(x, r.y, w, t), 0f, w / t, clipRight);
            }
            DrawTileClipped(strip.right, new Rect(r.xMax - t, r.y, t, t), 0f, 1f, clipRight);
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

        // 글을 공백 단위로 접어 maxWidth(캔버스 픽셀) 안에 들어가는 줄들로 나눈다. 같은 줄에 남는 연속 공백은 그대로, 줄 끝의 공백은 뗀다.
        // 한 단어가 maxWidth보다 넓으면 예외 — 글자 단위로 쪼개 읽히지 않게 만들지 않는다. 펼친 카드의 상세 글(설명·특성)이 쓴다.
        public static List<string> WrapText(PixelUiSkin.BitmapFont font, string text, int scale, int maxWidth)
        {
            var lines = new List<string>();
            string line = "";
            foreach (var word in text.Split(' '))
            {
                // 빈 토큰은 연속 공백 — 줄을 바꾸지 않고 공백만 더한다.
                string candidate = word.Length == 0 ? line + " " : (line.Length == 0 ? word : line + " " + word);
                if (MeasureText(font, candidate.TrimEnd(), scale) <= maxWidth)
                {
                    line = candidate;
                    continue;
                }
                if (MeasureText(font, word, scale) > maxWidth)
                    throw new System.Exception($"[PickShoot] PixelGui.WrapText: 단어 '{word}'의 폭이 허용 폭 {maxWidth}px를 넘음. 문구를 줄이거나 영역을 넓힐 것.");
                lines.Add(line.TrimEnd());
                line = word;
            }
            if (line.TrimEnd().Length > 0) lines.Add(line.TrimEnd());
            return lines;
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

        // 영역을 한 타일로 반복해 채운다 — 시작 화면의 바닥 무늬. 타일 변을 화면 픽셀에 맞추므로(DrawTile) 비정수 배율에서도 틈이 없다. t는 타일 한 변 (캔버스 픽셀).
        public static void FillTiles(Sprite sprite, Rect area, float t)
        {
            TileFill(sprite, area, t);
        }

        // 이웃과 맞닿는 타일 한 장 — 바닥 무늬 위에 덧놓는 변형 타일처럼 FillTiles의 칸과 변이 같아야 하는 조각. 혼자 놓이는 조각은 DrawSprite.
        public static void DrawTileSnapped(Sprite sprite, Rect screenRect)
        {
            DrawTile(sprite, screenRect, 0f, 0f, 1f, 1f);
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
                    DrawTile(sprite, new Rect(x, y, w, h), 0f, 0f, w / t, h / t);
                }
            }
        }

        // 타일 조각(가로 구간 [left, right], 세로 전체)을 캔버스 x = clipRight에서 잘라 그린다. 잘린 비율만큼 UV 오른쪽도 줄인다.
        static void DrawTileClipped(Sprite sprite, Rect rect, float left, float right, float clipRight)
        {
            if (rect.x >= clipRight) return;
            if (rect.xMax > clipRight)
            {
                float visible = clipRight - rect.x;
                right = left + (right - left) * (visible / rect.width);
                rect.width = visible;
            }
            DrawTile(sprite, rect, left, 0f, right, 1f);
        }

        // 이웃 조각과 맞닿아야 하는 타일용 DrawSpritePart. 네 변을 각각 가장 가까운 화면 픽셀 경계로 옮겨 그린다.
        // IMGUI는 GUI.matrix에 비정수 배율이 걸리면 쿼드마다 위치와 크기를 따로 화면 픽셀로 반올림한다. 그래서 캔버스 좌표로는 변을
        // 공유하는 이웃 타일도 화면에서는 1px 벌어지거나 겹친다 — 720p에서 HUD 타일은 21.33px인데 폭은 21로 내리고 위치는 .67에서
        // 올려, 세 칸마다 배경이 비치는 틈이 생겼다. 변을 미리 화면 픽셀에 맞춰 두면 위치·크기가 이미 정수라 엔진이 반올림해도 그대로고,
        // 이웃 타일은 같은 변 값을 같은 식으로 맞추므로 항상 맞닿는다. 그 대신 타일 폭이 자리에 따라 1px씩 다르다 (비정수 배율에서
        // 텍셀 폭이 고르지 않은 것과 같은 조건). 혼자 놓이는 조각(글자·아이콘)에는 쓰지 않는다 — 엔진의 반올림은 크기를 위치와
        // 무관하게 지키므로, 움직이는 조각(데미지 숫자)이 자리에 따라 1px씩 커졌다 작아졌다 하지 않는다.
        static void DrawTile(Sprite sprite, Rect rect, float left, float top, float right, float bottom)
        {
            rect = SnapToScreenPixels(rect);
            if (rect.width <= 0f || rect.height <= 0f) return;
            DrawSpritePart(sprite, rect, left, top, right, bottom);
        }

        // 결과도 캔버스 좌표다 (화면 픽셀로 옮겨 반올림한 뒤 되돌린다).
        static Rect SnapToScreenPixels(Rect r)
        {
            float k = currentPixelSize;
            return Rect.MinMaxRect(Mathf.Round(r.xMin * k) / k, Mathf.Round(r.yMin * k) / k,
                Mathf.Round(r.xMax * k) / k, Mathf.Round(r.yMax * k) / k);
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
