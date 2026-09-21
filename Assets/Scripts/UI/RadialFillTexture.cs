using UnityEngine;

namespace PickShoot
{
    // 시계 방향으로 차오르는 원판(라디얼 필)을 텍셀 단위로 래스터라이즈한 텍스처. 플레이어 머리 위 대시 쿨다운 표시에 쓴다.
    // Kenney 시트에 원형 게이지 조각이 없어 런타임에 그린다. 규칙은 CrosshairTexture와 같다: 텍스처 한 변은 홀수라 중심이 텍셀
    // 한가운데에 놓이고, 반경은 "중심 텍셀에서 센 텍셀 수"(중심 텍셀까지의 거리가 반경 + 0.5 미만인 텍셀이 원판 안쪽),
    // 12시부터 시계 방향으로 비율만큼 채움 색·나머지는 바탕 색이다.
    // 비율은 RatioSteps 단계로 끊어 본다 — 원판이 작아 그보다 잘게 나눠도 텍셀이 바뀌지 않으며, 단계가 바뀐 프레임에만 다시 그린다.
    public class RadialFillTexture
    {
        const int RatioSteps = 64;

        Texture2D texture;
        Color32[] pixels;
        int lastRadius = -1;
        int lastStep = -1;
        Color32 lastFill;
        Color32 lastBack;

        public Texture2D Texture => texture;
        // 텍스처 한 변의 텍셀 수 (홀수). Refresh 이후에만 유효.
        public int SizeTexels => texture != null ? texture.width : 0;

        public void Refresh(int radiusTexels, float ratio, Color fillColor, Color backColor)
        {
            if (radiusTexels < 1)
                throw new System.Exception($"[PickShoot] RadialFillTexture.Refresh: 반경({radiusTexels})은 1텍셀 이상이어야 함.");
            if (ratio < 0f || ratio > 1f)
                throw new System.Exception($"[PickShoot] RadialFillTexture.Refresh: 비율({ratio})이 0~1 범위를 벗어남.");

            int step = Mathf.FloorToInt(ratio * RatioSteps);
            Color32 fill = fillColor;
            Color32 back = backColor;
            if (texture != null && radiusTexels == lastRadius && step == lastStep && Same(fill, lastFill) && Same(back, lastBack))
                return;
            lastRadius = radiusTexels;
            lastStep = step;
            lastFill = fill;
            lastBack = back;

            EnsureTexture(radiusTexels * 2 + 1);
            int size = texture.width;
            float c = size * 0.5f;
            float edge = radiusTexels + 0.5f;
            float filled = step / (float)RatioSteps;
            var clear = new Color32(0, 0, 0, 0);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - c;
                    float dy = y + 0.5f - c;
                    Color32 col = clear;
                    if (dx * dx + dy * dy < edge * edge)
                    {
                        // 텍스처 +y가 화면 위쪽이므로 atan2(dx, dy)는 12시가 0, 오른쪽(3시)으로 갈수록 커진다 = 화면 시계 방향.
                        float theta = Mathf.Atan2(dx, dy);
                        if (theta < 0f) theta += 2f * Mathf.PI;
                        col = theta / (2f * Mathf.PI) < filled ? fill : back;
                    }
                    pixels[y * size + x] = col;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false);
        }

        public void Dispose()
        {
            if (texture != null) Object.Destroy(texture);
            texture = null;
            pixels = null;
        }

        static bool Same(Color32 a, Color32 b)
        {
            return a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
        }

        // 반경이 바뀌면 다시 만든다 (원판이 텍스처를 꽉 채워야 호출자가 텍스처 크기로 위치를 잡을 수 있다).
        void EnsureTexture(int size)
        {
            if (texture != null && texture.width == size) return;
            if (texture != null) Object.Destroy(texture);
            texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "RadialFill",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            pixels = new Color32[size * size];
        }
    }
}
