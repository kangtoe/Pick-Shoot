using UnityEngine;

namespace PickShoot
{
    // 폭발 탄환의 폭발 연출. 접촉 지점에서 폭발 반경까지 짧게 퍼진 뒤 그 크기 그대로 옅어져 사라지는 단색 원판.
    // 시트에 폭발 조각이 없어 런타임에 그린다. 1유닛 지름의 원을 discTexels 텍셀로 한 번 래스터라이즈하고 폭발 반경에 맞춰 스프라이트를
    // 확대한다 — 원의 픽셀 하나가 (폭발 지름 ÷ discTexels) 유닛으로 굵어져 큰 원에서도 계단이 뚜렷하다. 기본 16텍셀은 시트 조각 한 칸과
    // 같은 규격(월드 PPU)으로, 반경 2 폭발에서 픽셀 하나가 월드 텍셀 4개 크기다. 32(월드 텍셀 2개)도 써 봤으나 16이 나았고, 월드 텍셀
    // 크기로 반경 32텍셀 원판을 직접 그리면 계단이 1텍셀뿐이라 매끈한 원으로 읽혔다 (2026-09-24).
    // 윤곽선·색 띠·디더링은 두지 않는다 — 시도 뒤 단색 원으로 정리 (2026-09-23~24). 크로스헤어처럼 윤곽선 없음.
    [RequireComponent(typeof(SpriteRenderer))]
    public class ExplosionBurst : MonoBehaviour
    {
        // 원판의 지름은 1유닛. 반경 = 0.5유닛을 기준으로 확대한다.
        const float DiscRadiusUnits = 0.5f;

        // 1유닛 지름의 원을 몇 텍셀로 래스터라이즈할지 (= 원 텍스처의 PPU). 확대 뒤 픽셀 크기 = 폭발 지름 ÷ 이 값.
        [SerializeField, Min(2)] int discTexels = 16;
        [SerializeField, Min(0.01f)] float expandDuration = 0.1f;
        [SerializeField, Min(0.01f)] float fadeDuration = 0.25f;
        // 퍼지기 시작할 때의 반경 (최종 반경 비율).
        [SerializeField, Range(0f, 1f)] float startRadiusFraction = 0.2f;
        [SerializeField] Color fillColor = new Color(1f, 0.6f, 0.2f, 0.75f);

        SpriteRenderer sr;
        Texture2D texture;
        Sprite sprite;
        float fullRadius;
        float startTime;

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
        }

        // 폭발 반경(월드 단위)으로 재생을 시작한다. Instantiate 직후(첫 Update 전)에 호출해야 한다.
        public void Play(float worldRadius)
        {
            if (worldRadius <= 0f)
                throw new System.Exception($"[PickShoot] ExplosionBurst.Play: worldRadius는 양수여야 합니다. 전달값={worldRadius}");
            if (texture != null)
                throw new System.Exception($"[PickShoot] ExplosionBurst.Play: '{name}'는 이미 재생 중. 폭발마다 새로 Instantiate해야 합니다.");

            texture = RasterizeDisc(discTexels, fillColor);
            // 만드는 시점의 알파로 메시를 깎는 기본(Tight) 대신 사각형 메시를 명시한다. PPU = 지름 텍셀 수라 스프라이트 지름이 1유닛.
            sprite = Sprite.Create(texture, new Rect(0f, 0f, discTexels, discTexels), new Vector2(0.5f, 0.5f), discTexels, 0, SpriteMeshType.FullRect);
            sprite.name = "ExplosionBurst";
            sr.sprite = sprite;
            fullRadius = worldRadius;
            startTime = Time.time;
            Refresh();
        }

        void Update()
        {
            if (texture == null)
                throw new System.Exception($"[PickShoot] ExplosionBurst '{name}': Play 없이 살아 있음. Bullet이 Instantiate 직후 Play(반경)를 호출해야 합니다.");
            Refresh();
        }

        void OnDestroy()
        {
            if (sprite != null) Destroy(sprite);
            if (texture != null) Destroy(texture);
        }

        void Refresh()
        {
            float elapsed = Time.time - startTime;
            if (elapsed >= expandDuration + fadeDuration)
            {
                Destroy(gameObject);
                return;
            }

            float u = Mathf.Clamp01(elapsed / expandDuration);
            float radius = Mathf.Lerp(fullRadius * startRadiusFraction, fullRadius, u);
            float scale = radius / DiscRadiusUnits;
            transform.localScale = new Vector3(scale, scale, 1f);

            // 다 퍼진 뒤에만 옅어진다. 색은 텍스처에 구워 두고 렌더러 알파로만 곱한다.
            float fade = Mathf.Clamp01((elapsed - expandDuration) / fadeDuration);
            sr.color = new Color(1f, 1f, 1f, 1f - fade);
        }

        // size×size 텍셀에 꽉 차는 원. 중심에서 size/2 미만 거리의 텍셀(중심점 기준)이 안쪽이다.
        static Texture2D RasterizeDisc(int size, Color color)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "ExplosionBurst",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color32[size * size];
            Color32 fill = color;
            var clear = new Color32(0, 0, 0, 0);
            float c = size * 0.5f;
            float edge = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - c;
                    float dy = y + 0.5f - c;
                    pixels[y * size + x] = dx * dx + dy * dy < edge * edge ? fill : clear;
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false);
            return tex;
        }
    }
}
