using System.Collections.Generic;
using UnityEngine;

namespace PickShoot
{
    // 커서 주위 탄약 링을 텍셀 단위로 래스터라이즈한 텍스처. Kenney 시트에 링 조각이 없어 런타임에 그리며,
    // GameHud가 다른 픽셀 UI와 같은 정수 배율로 커서 위치에 띄운다.
    // 링은 12시부터 시계 방향으로 "모든 자루의 탄창 비율 평균"(Σ 현재/최대 ÷ 자루 수)만큼 채워진다. 자루마다 같은 비중이라
    // 탄창이 작은 무기(라이플)가 비어도 큰 무기(산탄총)가 빈 것만큼 링이 줄어든다. 무기별 잔량은 하단 HUD 패널의 핍이 담당한다
    // — 종류별 색 구분은 종류가 늘면 읽기 어려워 링에는 두지 않는다.
    // 링 안팎 1텍셀 가장자리는 윤곽선 색 — 픽셀 스프라이트의 어두운 외곽선과 같은 역할. 탄약·설정이 바뀐 프레임에만 다시 그린다.
    public class AmmoRingTexture
    {
        Texture2D texture;
        Color32[] pixels;
        readonly List<int> signature = new List<int>();
        readonly List<int> lastSignature = new List<int>();

        public Texture2D Texture => texture;
        // 텍스처 한 변의 텍셀 수 (윤곽선 포함 지름). Refresh 이후에만 유효.
        public int SizeTexels => texture != null ? texture.width : 0;

        // radiusTexels: 링 바깥 가장자리 반경 (윤곽선 제외). thicknessTexels: 채움 띠 두께.
        public void Refresh(IReadOnlyList<Weapon> slots, float radiusTexels, int thicknessTexels,
            Color fillColor, Color emptyColor, Color outlineColor)
        {
            if (slots == null || slots.Count == 0)
                throw new System.Exception("[PickShoot] AmmoRingTexture.Refresh: 무기 슬롯이 비어 있음.");
            if (thicknessTexels < 1)
                throw new System.Exception($"[PickShoot] AmmoRingTexture.Refresh: 띠 두께({thicknessTexels})는 1텍셀 이상이어야 함.");
            if (radiusTexels - thicknessTexels < 1f)
                throw new System.Exception($"[PickShoot] AmmoRingTexture.Refresh: 반경({radiusTexels})에서 두께({thicknessTexels})를 뺀 안쪽 반경이 1텍셀 미만.");

            if (!BuildSignature(slots, radiusTexels, thicknessTexels, fillColor, emptyColor, outlineColor) && texture != null)
                return;

            float ratio = AverageAmmoRatio(slots);

            // 윤곽선 1텍셀을 안팎에 두므로 지름은 (반경 + 1)의 두 배. 짝수 한 변이라 중심이 텍셀 경계에 놓여 상하좌우 대칭.
            int size = Mathf.CeilToInt(radiusTexels + 1f) * 2;
            EnsureTexture(size);

            float c = size * 0.5f;
            float outer = radiusTexels;
            float inner = radiusTexels - thicknessTexels;
            Color32 fill = fillColor;
            Color32 empty = emptyColor;
            Color32 outline = outlineColor;
            var clear = new Color32(0, 0, 0, 0);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - c;
                    float dy = y + 0.5f - c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    Color32 col = clear;
                    if (d >= inner - 1f && d < outer + 1f)
                    {
                        if (d < inner || d >= outer)
                        {
                            col = outline;
                        }
                        else
                        {
                            // 텍스처 +y가 화면 위쪽이므로 atan2(dx, dy)는 12시가 0, 오른쪽(3시)으로 갈수록 커진다 = 화면 시계 방향.
                            float theta = Mathf.Atan2(dx, dy);
                            if (theta < 0f) theta += 2f * Mathf.PI;
                            float f = theta / (2f * Mathf.PI);
                            col = f < ratio ? fill : empty;
                        }
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
                    throw new System.Exception($"[PickShoot] AmmoRingTexture: 슬롯 {i + 1} '{w.Data.DisplayName}' Lv{w.Level}의 최대 탄약이 0 이하 ({w.MaxAmmo}). WeaponData.maxAmmo 확인.");
                sum += (float)w.CurrentAmmo / w.MaxAmmo;
            }
            return sum / slots.Count;
        }

        // 그리기에 영향을 주는 값을 전부 정수 목록으로 모아 직전과 비교한다. 바뀌었으면 true.
        bool BuildSignature(IReadOnlyList<Weapon> slots, float radiusTexels, int thicknessTexels,
            Color fillColor, Color emptyColor, Color outlineColor)
        {
            signature.Clear();
            signature.Add(Mathf.RoundToInt(radiusTexels * 1000f));
            signature.Add(thicknessTexels);
            signature.Add(Pack(fillColor));
            signature.Add(Pack(emptyColor));
            signature.Add(Pack(outlineColor));
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

        void EnsureTexture(int size)
        {
            if (texture != null && texture.width == size) return;
            if (texture != null) Object.Destroy(texture);
            texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "AmmoRing",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            pixels = new Color32[size * size];
        }
    }
}
