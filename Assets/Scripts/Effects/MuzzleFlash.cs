using UnityEngine;

namespace PickShoot
{
    // 총구 화염 단발 연출. 무기 비주얼의 자식으로 생성되어 링 회전·반동을 그대로 따라가며,
    // 발사 방향(local +x)으로 길쭉한 형태로 터졌다가 줄어들며 사라진다.
    // 매번 크기·각도·상하를 조금씩 흔들어 연사 시 같은 그림이 반복되는 느낌을 피한다.
    [RequireComponent(typeof(SpriteRenderer))]
    public class MuzzleFlash : MonoBehaviour
    {
        [SerializeField] float duration = 0.1f;
        [SerializeField] float startScale = 2f;
        [SerializeField] float endScale = 1f;
        [SerializeField] Vector2 aspect = new Vector2(1.6f, 0.9f);
        [SerializeField, Range(0f, 1f)] float scaleJitter = 0.2f;
        [SerializeField, Min(0f)] float angleJitterDegrees = 12f;

        SpriteRenderer sr;
        Color baseColor;
        float startTime;
        float sizeMultiplier;

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            baseColor = sr.color;
            startTime = Time.time;
            sizeMultiplier = 1f + Random.Range(-scaleJitter, scaleJitter);
            transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-angleJitterDegrees, angleJitterDegrees));
            sr.flipY = Random.value < 0.5f;
            ApplyScale(0f);
        }

        // 발사한 무기의 색을 입힌다 (탄환 색과 맞춤). 프리팹의 알파는 유지.
        public void Tint(Color color)
        {
            color.a = baseColor.a;
            baseColor = color;
            sr.color = color;
        }

        void Update()
        {
            float t = (Time.time - startTime) / duration;
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }

            ApplyScale(t);
            var c = baseColor;
            c.a = baseColor.a * (1f - t);
            sr.color = c;
        }

        void ApplyScale(float t)
        {
            float s = Mathf.Lerp(startScale, endScale, t) * sizeMultiplier;
            transform.localScale = new Vector3(s * aspect.x, s * aspect.y, 1f);
        }
    }
}
