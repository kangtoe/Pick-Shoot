using UnityEngine;

namespace PickShoot
{
    // 능력의 연출 (Characters §2). 한 인스턴스가 하나만 재생하고 끝나면 스스로 사라진다.
    //   회수 펄스(PlayPulse): 반경을 덮은 옅은 원판이 중심으로 좁아지고, 고리 여럿이 시차를 두고 반경에서 출발해 가속하며 중심으로 빨려 들고(좁아질수록 진하다),
    //                       줄무늬가 반경 근처에서 중심으로 날아든다(빨라질수록 길다). 끝에 중심에서 작은 고리가 터진다 — 거둬들였다는 마침표.
    //   충격파(PlayShockwave): 굵은 고리가 중심에서 반경까지 감속하며 퍼지면서 얇아지고, 안쪽에 가는 고리가 따라붙으며, 고리 뒤로 줄무늬가 튀고, 고리 안의 원판은
    //                       금방 걷힌다.
    //   무적 막(PlayShield): 능력 발동 무적(PlayerAbility) 동안 플레이어를 따라다니는 고리와 옅은 원판. 튀어나왔다 자리 잡고, 끝나기 직전에 깜빡인다.
    // 시트에 없는 형태라 런타임에 래스터라이즈한다. 폭발 원판(ExplosionBurst)처럼 한 장을 확대하면 고리 두께까지 커지므로, 반경이 바뀌는 고리를 매 프레임 텍셀
    // 단위로 다시 그린다. 펄스·충격파는 픽셀 하나가 월드 텍셀 waveTexelsPerPixel개 — 폭발 원판(반경 2에서 픽셀 하나가 월드 텍셀 4개)과 같은 굵은 계단이다.
    // 무적 막은 몸 크기라 월드 텍셀 그대로 그린다 (머리 위 원판·크로스헤어와 같다). 색은 한 가지이고 모양마다 불투명도만 다르다 — 겹친 곳은 진한 쪽을 쓴다.
    // 펄스·충격파는 발동한 자리에 머문다. 시간은 게임 시간이라 일시정지 중에는 멈춘다.
    [RequireComponent(typeof(SpriteRenderer))]
    public class AbilityEffect : MonoBehaviour
    {
        // 월드 스프라이트의 PPU — 월드 텍셀 하나가 1/16유닛.
        const float WorldTexelsPerUnit = 16f;
        // 줄무늬를 반경 방향으로 찍는 간격 (픽셀). 1보다 작아야 비스듬한 줄에 구멍이 나지 않는다.
        const float StreakStepPixels = 0.5f;
        // 무적 막이 튀어나왔다 자리 잡기까지(초)와 그때 더 큰 반경(텍셀), 끝나기 전 깜빡임의 반주기(초).
        const float ShieldPopSeconds = 0.08f;
        const int ShieldPopTexels = 4;
        // 무적 막 고리의 두께 (월드 텍셀). 1텍셀은 모래 바닥 위에서 거의 보이지 않았다 (Recordings/ability_fx_shield_mockup_2026-10-14.png).
        const float ShieldRingTexels = 2f;
        const float ShieldBlinkHalfPeriod = 0.05f;

        [Header("Pulse · Shockwave")]
        // 펄스·충격파의 픽셀 하나가 덮는 월드 텍셀 수.
        [SerializeField, Min(1)] int waveTexelsPerPixel = 4;

        [Header("Pulse")]
        [SerializeField, Min(0.05f)] float pulseDuration = 0.45f;
        // 빨려 드는 고리의 수와 고리 사이 시차 (초). 마지막 고리도 중심에 닿아야 하므로 시차의 합은 길이보다 짧아야 한다.
        [SerializeField, Min(1)] int pulseRings = 3;
        [SerializeField, Min(0f)] float pulseRingInterval = 0.08f;
        // 안쪽으로 날아드는 줄무늬 수.
        [SerializeField, Min(0)] int pulseStreaks = 20;
        // 반경을 덮는 원판의 불투명도.
        [SerializeField, Range(0f, 1f)] float pulseFillAlpha = 0.15f;

        [Header("Shockwave")]
        [SerializeField, Min(0.05f)] float shockwaveDuration = 0.35f;
        // 고리 뒤로 튀는 줄무늬 수.
        [SerializeField, Min(0)] int shockwaveStreaks = 16;
        // 고리 안 원판의 처음 불투명도. 고리보다 빨리 걷힌다.
        [SerializeField, Range(0f, 1f)] float shockwaveFillAlpha = 0.35f;

        [Header("Shield")]
        // 무적 막의 반경 (월드 텍셀, 중심 텍셀에서 센 수). 헬멧 캐릭터 몸통(22텍셀)을 두른다.
        [SerializeField, Min(2)] int shieldRadiusTexels = 14;
        [SerializeField, Range(0f, 1f)] float shieldFillAlpha = 0.25f;
        // 끝나기 전 이 시간 동안 깜빡여 무적이 곧 풀림을 알린다 (초).
        [SerializeField, Min(0f)] float shieldBlinkSeconds = 0.2f;

        enum Mode { None, Pulse, Shockwave, Shield }

        SpriteRenderer sr;
        Texture2D texture;
        Sprite sprite;
        Color32[] pixels;
        // 이번 프레임의 픽셀별 불투명도(0~1)와 중심까지의 거리(픽셀). 텍스처 한 변은 홀수라 중심이 가운데 텍셀 한가운데에 놓인다.
        float[] alpha;
        float[] distance;
        int size;
        float center;
        Mode mode;
        Color color;
        float radiusPx;
        float duration;
        float startTime;
        // 무적 막이 따라다니는 대상. 사라지면(플레이어 사망) 막도 사라진다.
        Transform follow;
        // 줄무늬마다: 방향, 출발 반경(반경 비율), 출발 지연(길이 비율).
        Vector2[] streakDirs;
        float[] streakStart;
        float[] streakDelay;

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
        }

        // 펄스 — 반경(월드 유닛) 안을 중심으로 빨아들이는 모습. Instantiate 직후(첫 Update 전)에 호출한다.
        public void PlayPulse(float worldRadius, Color effectColor)
        {
            if (pulseDuration - (pulseRings - 1) * pulseRingInterval <= 0f)
                throw new System.Exception($"[PickShoot] AbilityEffect: 펄스 고리 시차의 합({(pulseRings - 1) * pulseRingInterval}초)이 길이({pulseDuration}초) 이상 — 마지막 고리가 중심에 닿지 못한다.");
            BeginWave(Mode.Pulse, worldRadius, effectColor, pulseDuration, pulseStreaks);
            // 줄무늬는 반경 근처(70~100%)에서 출발하고, 길이의 앞 35% 안에서 흩어져 출발한다.
            for (int k = 0; k < streakDirs.Length; k++)
            {
                streakStart[k] = Random.Range(0.7f, 1f);
                streakDelay[k] = Random.Range(0f, 0.35f);
            }
            Redraw();
        }

        // 충격파 — 중심에서 반경(월드 유닛)까지 밀어내는 모습. Instantiate 직후(첫 Update 전)에 호출한다.
        public void PlayShockwave(float worldRadius, Color effectColor)
        {
            BeginWave(Mode.Shockwave, worldRadius, effectColor, shockwaveDuration, shockwaveStreaks);
            // 줄무늬는 고리 안쪽(55~85% 자리)에서 고리를 따라 퍼진다.
            for (int k = 0; k < streakDirs.Length; k++)
                streakStart[k] = Random.Range(0.55f, 0.85f);
            Redraw();
        }

        // 무적 막 — target을 따라다니며 seconds 동안. Instantiate 직후(첫 Update 전)에 호출한다.
        public void PlayShield(Transform target, float seconds, Color effectColor)
        {
            if (target == null)
                throw new System.Exception("[PickShoot] AbilityEffect.PlayShield: 따라다닐 대상이 null.");
            follow = target;
            Begin(Mode.Shield, effectColor, seconds, 1, shieldRadiusTexels + ShieldPopTexels + 1f);
            transform.position = target.position;
            Redraw();
        }

        void BeginWave(Mode waveMode, float worldRadius, Color effectColor, float seconds, int streaks)
        {
            if (worldRadius <= 0f)
                throw new System.Exception($"[PickShoot] AbilityEffect: 반경은 양수여야 함 ({worldRadius}).");
            radiusPx = worldRadius * WorldTexelsPerUnit / waveTexelsPerPixel;
            Begin(waveMode, effectColor, seconds, waveTexelsPerPixel, radiusPx);
            streakDirs = new Vector2[streaks];
            streakStart = new float[streaks];
            streakDelay = new float[streaks];
            // 고르게 나눈 각도에 흔들림을 얹는다 — 몰리거나 빈 쪽이 생기지 않게.
            float step = 2f * Mathf.PI / Mathf.Max(1, streaks);
            for (int k = 0; k < streaks; k++)
            {
                float a = (k + Random.Range(-0.35f, 0.35f)) * step;
                streakDirs[k] = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            }
        }

        void Begin(Mode playMode, Color effectColor, float seconds, int texelsPerPixel, float maxRadiusPx)
        {
            if (mode != Mode.None)
                throw new System.Exception($"[PickShoot] AbilityEffect '{name}'는 이미 재생 중. 연출마다 새로 Instantiate해야 함.");
            if (seconds <= 0f)
                throw new System.Exception($"[PickShoot] AbilityEffect: 길이는 양수여야 함 ({seconds}).");
            mode = playMode;
            color = effectColor;
            duration = seconds;
            startTime = Time.time;

            size = 2 * Mathf.CeilToInt(maxRadiusPx + 1f) + 1;
            center = size * 0.5f;
            pixels = new Color32[size * size];
            alpha = new float[size * size];
            distance = new float[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - center;
                    float dy = y + 0.5f - center;
                    distance[y * size + x] = Mathf.Sqrt(dx * dx + dy * dy);
                }
            texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "AbilityEffect",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            // 만드는 시점의 알파로 메시를 깎는 기본(Tight) 대신 사각형 메시를 명시한다 — 그림이 매 프레임 바뀐다.
            sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), WorldTexelsPerUnit / texelsPerPixel, 0, SpriteMeshType.FullRect);
            sprite.name = "AbilityEffect";
            sr.sprite = sprite;
        }

        void LateUpdate()
        {
            if (mode == Mode.None)
                throw new System.Exception($"[PickShoot] AbilityEffect '{name}': Play 없이 살아 있음. Instantiate한 쪽(PlayerAbility)이 직후 Play…를 호출해야 함.");
            if (Time.time - startTime >= duration || (mode == Mode.Shield && follow == null))
            {
                Destroy(gameObject);
                return;
            }
            // 따라다니는 위치는 이동이 끝난 뒤(보간된 플레이어 위치)에 맞춘다.
            if (follow != null) transform.position = follow.position;
            Redraw();
        }

        void OnDestroy()
        {
            if (sprite != null) Destroy(sprite);
            if (texture != null) Destroy(texture);
        }

        void Redraw()
        {
            System.Array.Clear(alpha, 0, alpha.Length);
            float t = Time.time - startTime;
            switch (mode)
            {
                case Mode.Pulse: DrawPulse(t); break;
                case Mode.Shockwave: DrawShockwave(t); break;
                case Mode.Shield: DrawShield(t); break;
            }
            Color32 c = color;
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(c.r, c.g, c.b, (byte)(alpha[i] * c.a));
            texture.SetPixels32(pixels);
            texture.Apply(false);
        }

        void DrawPulse(float t)
        {
            float u = Mathf.Clamp01(t / duration);
            // 원판 — 반경을 덮고 시작해 가속하며 중심으로 좁아진다.
            Disc(radiusPx * (1f - u * u), pulseFillAlpha);

            // 고리 — 반경에서 출발해 가속하며(1 − v²) 중심으로 좁아지고, 좁아질수록 진해진다.
            float ringLife = duration - (pulseRings - 1) * pulseRingInterval;
            for (int i = 0; i < pulseRings; i++)
            {
                float v = (t - i * pulseRingInterval) / ringLife;
                if (v < 0f || v >= 1f) continue;
                Ring(radiusPx * (1f - v * v), 1f, Mathf.Lerp(0.4f, 1f, v));
            }

            // 줄무늬 — 머리가 중심 쪽, 꼬리가 바깥쪽. 길이의 60% 동안 날아든다.
            for (int k = 0; k < streakDirs.Length; k++)
            {
                float v = (u - streakDelay[k]) / 0.6f;
                if (v < 0f || v >= 1f) continue;
                float head = streakStart[k] * radiusPx * (1f - v * v);
                if (head < 1f) continue;
                Streak(streakDirs[k], head, Mathf.Min(radiusPx, head + 1f + 5f * v), 0.9f);
            }

            // 끝의 20% — 중심에서 작은 고리가 터진다.
            float p = (u - 0.8f) / 0.2f;
            if (p >= 0f) Ring(1.5f + 2.5f * p, 1f, 1f - p);
        }

        void DrawShockwave(float t)
        {
            float u = Mathf.Clamp01(t / duration);
            float spread = 1f - (1f - u) * (1f - u) * (1f - u);    // 감속하며 퍼진다
            float r = Mathf.Lerp(1f, radiusPx, spread);
            float fade = 1f - u * u;                               // 끝 무렵에 빠르게 옅어진다

            Disc(r, shockwaveFillAlpha * (1f - u) * (1f - u));
            Ring(r, Mathf.Lerp(3f, 1f, u), fade);
            Ring(r * 0.8f, 1f, 0.5f * fade);
            for (int k = 0; k < streakDirs.Length; k++)
            {
                float from = r * streakStart[k];
                Streak(streakDirs[k], from, from + Mathf.Lerp(4f, 1f, u), 0.8f * fade);
            }
        }

        void DrawShield(float t)
        {
            float remaining = duration - t;
            // 끝나기 전 깜빡임 — 꺼진 반주기에는 아무것도 그리지 않는다.
            if (remaining < shieldBlinkSeconds && Mathf.FloorToInt(remaining / ShieldBlinkHalfPeriod) % 2 == 1) return;
            float pop = Mathf.Clamp01(t / ShieldPopSeconds);
            // 반경은 중심 텍셀에서 센 수 — 축 위 그 거리의 텍셀까지 고리에 든다 (RadialFillTexture와 같은 규칙).
            float r = shieldRadiusTexels + ShieldPopTexels * (1f - pop) + 0.5f;
            Disc(r, shieldFillAlpha);
            Ring(r, ShieldRingTexels, Mathf.Lerp(0.5f, 1f, pop));
        }

        // 중심에서 r 미만.
        void Disc(float r, float a)
        {
            if (a <= 0f) return;
            for (int i = 0; i < distance.Length; i++)
                if (distance[i] < r) Plot(i, a);
        }

        // 바깥 가장자리 r, 두께 thickness (픽셀). r이 두께보다 작으면 원판이 된다.
        void Ring(float r, float thickness, float a)
        {
            if (a <= 0f || r <= 0f) return;
            float inner = r - thickness;
            for (int i = 0; i < distance.Length; i++)
                if (distance[i] < r && distance[i] >= inner) Plot(i, a);
        }

        // 중심에서 dir 방향으로 from부터 to까지의 한 픽셀 굵기 선.
        void Streak(Vector2 dir, float from, float to, float a)
        {
            if (a <= 0f) return;
            for (float s = from; s <= to; s += StreakStepPixels)
            {
                int x = Mathf.FloorToInt(center + dir.x * s);
                int y = Mathf.FloorToInt(center + dir.y * s);
                if (x < 0 || y < 0 || x >= size || y >= size) continue;
                Plot(y * size + x, a);
            }
        }

        void Plot(int i, float a)
        {
            if (a > alpha[i]) alpha[i] = Mathf.Min(1f, a);
        }
    }
}
