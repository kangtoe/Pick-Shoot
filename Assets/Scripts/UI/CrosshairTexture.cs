using System.Collections.Generic;
using UnityEngine;

namespace PickShoot
{
    // 커서를 대신하는 크로스헤어(중심점 + 탄약 링)를 텍셀 단위로 래스터라이즈한 텍스처. Kenney 시트에 링 조각이 없어 런타임에 그리며,
    // GameHud가 월드 텍셀과 같은 정수 배율로 커서 위치에 띄운다 — 텍셀이 월드 스프라이트의 픽셀과 같은 크기라 원의 계단이 그대로 보인다.
    // 텍스처 한 변은 홀수라 중심이 텍셀 한가운데에 놓인다. 반경은 모두 "중심 텍셀에서 센 텍셀 수"다: 중심 텍셀까지의 거리가
    // 반경 + 0.5 미만인 텍셀이 그 반경 안쪽 (반경 0 = 중심 텍셀 하나, 반경 6 = 지름 13텍셀).
    // 링은 12시부터 시계 방향으로 "모든 자루의 탄창 비율 평균"(Σ 현재/최대 ÷ 자루 수)만큼 채워진다. 자루마다 같은 비중이라
    // 탄창이 작은 무기(라이플)가 비어도 큰 무기(산탄총)가 빈 것만큼 링이 줄어든다. 무기별 잔량은 하단 HUD 패널의 핍이 담당한다
    // — 종류별 색 구분은 종류가 늘면 읽기 어려워 링에는 두지 않는다.
    // 중심점은 조준점 그 자체다. 탄약이 바닥나 링이 전부 빈 색이어도 점은 채움 색 그대로라 조준점을 잃지 않는다.
    // 링 반경은 호출자가 정한다 (GameHud가 사격마다 벌렸다가 되돌린다). 띠 두께는 그대로 두고 반경만 바뀌므로 확대가 아니라 벌어짐이다.
    // 텍스처는 지금까지 본 가장 큰 반경에 맞춰 두고 줄이지 않는다 — 반경이 출렁여도 다시 할당하지 않으며, 링은 항상 텍스처 중심에 있다.
    // 윤곽선은 링에도 중심점에도 두르지 않는다 — 시트 외곽선 색으로 링 안팎을 둘러 봤으나 이 텍셀 크기에서는 선이 띠만큼 두꺼워져
    // 조준점이 무거워 보였다. 탄약·설정이 바뀐 프레임에만 다시 그린다.
    public class CrosshairTexture
    {
        Texture2D texture;
        Color32[] pixels;
        readonly List<int> signature = new List<int>();
        readonly List<int> lastSignature = new List<int>();

        public Texture2D Texture => texture;
        // 텍스처 한 변의 텍셀 수 (홀수 — 중심이 텍셀 한가운데). 링 지름보다 클 수 있다. Refresh 이후에만 유효.
        public int SizeTexels => texture != null ? texture.width : 0;

        // radiusTexels: 링 채움 띠의 바깥 반경. thicknessTexels: 채움 띠 두께.
        // dotRadiusTexels: 중심점 반경 (0이면 중심 텍셀 하나, 1이면 3×3텍셀).
        public void Refresh(IReadOnlyList<Weapon> slots, int radiusTexels, int thicknessTexels, int dotRadiusTexels,
            Color fillColor, Color emptyColor)
        {
            if (slots == null || slots.Count == 0)
                throw new System.Exception("[PickShoot] CrosshairTexture.Refresh: 무기 슬롯이 비어 있음.");
            if (thicknessTexels < 1)
                throw new System.Exception($"[PickShoot] CrosshairTexture.Refresh: 띠 두께({thicknessTexels})는 1텍셀 이상이어야 함.");
            if (dotRadiusTexels < 0)
                throw new System.Exception($"[PickShoot] CrosshairTexture.Refresh: 중심점 반경({dotRadiusTexels})은 0텍셀 이상이어야 함.");
            // 점과 링 안쪽 가장자리 사이에 빈 텍셀이 적어도 한 겹은 있어야 점이 링에 붙어 보이지 않는다.
            if (dotRadiusTexels >= radiusTexels - thicknessTexels)
                throw new System.Exception($"[PickShoot] CrosshairTexture.Refresh: 중심점(반경 {dotRadiusTexels})이 링 안쪽 가장자리(반경 {radiusTexels} - 두께 {thicknessTexels})에 닿음.");

            if (!BuildSignature(slots, radiusTexels, thicknessTexels, dotRadiusTexels, fillColor, emptyColor) && texture != null)
                return;

            float ratio = AverageAmmoRatio(slots);

            // 중심 텍셀 하나 + 양쪽으로 반경만큼. 홀수 한 변이라 중심이 텍셀 한가운데에 놓여 상하좌우 대칭.
            EnsureTexture(radiusTexels * 2 + 1);
            int size = texture.width;

            float c = size * 0.5f;
            float outer = radiusTexels + 0.5f;
            float inner = outer - thicknessTexels;
            float dotEdge = dotRadiusTexels + 0.5f;
            Color32 fill = fillColor;
            Color32 empty = emptyColor;
            var clear = new Color32(0, 0, 0, 0);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - c;
                    float dy = y + 0.5f - c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    Color32 col = clear;
                    if (d < dotEdge)
                    {
                        col = fill;
                    }
                    else if (d >= inner && d < outer)
                    {
                        // 텍스처 +y가 화면 위쪽이므로 atan2(dx, dy)는 12시가 0, 오른쪽(3시)으로 갈수록 커진다 = 화면 시계 방향.
                        float theta = Mathf.Atan2(dx, dy);
                        if (theta < 0f) theta += 2f * Mathf.PI;
                        float f = theta / (2f * Mathf.PI);
                        col = f < ratio ? fill : empty;
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
            lastSignature.Clear();
        }

        // 자루별 (현재 탄약 ÷ 최대 탄약)의 평균. 탄창 크기와 무관하게 자루마다 같은 비중.
        static float AverageAmmoRatio(IReadOnlyList<Weapon> slots)
        {
            float sum = 0f;
            for (int i = 0; i < slots.Count; i++)
            {
                var w = slots[i];
                if (w.MaxAmmo <= 0)
                    throw new System.Exception($"[PickShoot] CrosshairTexture: 슬롯 {i + 1} '{w.Data.DisplayName}' Lv{w.Level}의 최대 탄약이 0 이하 ({w.MaxAmmo}). WeaponData.maxAmmo 확인.");
                sum += (float)w.CurrentAmmo / w.MaxAmmo;
            }
            return sum / slots.Count;
        }

        // 그리기에 영향을 주는 값을 전부 정수 목록으로 모아 직전과 비교한다. 바뀌었으면 true.
        bool BuildSignature(IReadOnlyList<Weapon> slots, int radiusTexels, int thicknessTexels, int dotRadiusTexels,
            Color fillColor, Color emptyColor)
        {
            signature.Clear();
            signature.Add(radiusTexels);
            signature.Add(thicknessTexels);
            signature.Add(dotRadiusTexels);
            signature.Add(Pack(fillColor));
            signature.Add(Pack(emptyColor));
            signature.Add(slots.Count);
            for (int i = 0; i < slots.Count; i++)
            {
                signature.Add(slots[i].CurrentAmmo);
                signature.Add(slots[i].MaxAmmo);
            }

            bool changed = signature.Count != lastSignature.Count;
            for (int i = 0; !changed && i < signature.Count; i++)
                changed = signature[i] != lastSignature[i];
            if (!changed) return false;

            lastSignature.Clear();
            lastSignature.AddRange(signature);
            return true;
        }

        static int Pack(Color color)
        {
            Color32 c = color;
            return c.r | (c.g << 8) | (c.b << 16) | (c.a << 24);
        }

        // 필요한 크기보다 작을 때만 다시 만든다. 이미 더 크면 그대로 쓴다 (남는 둘레는 투명으로 칠해진다).
        void EnsureTexture(int size)
        {
            if (texture != null && texture.width >= size) return;
            if (texture != null) Object.Destroy(texture);
            texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Crosshair",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            pixels = new Color32[size * size];
        }
    }
}
