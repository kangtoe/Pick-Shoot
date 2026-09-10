using UnityEngine;

namespace PickShoot
{
    [RequireComponent(typeof(TextMesh))]
    public class WarningLabel : MonoBehaviour
    {
        [SerializeField] float duration = 0.6f;
        [SerializeField] float startScale = 0.9f;
        [SerializeField] float endScale = 1.3f;
        [SerializeField] float blinkFrequency = 8f;

        TextMesh tm;
        Color baseColor;
        Vector3 baseLocalScale;
        float startTime;

        void Awake()
        {
            tm = GetComponent<TextMesh>();
            baseColor = tm.color;
            baseLocalScale = transform.localScale;
            startTime = Time.time;
        }

        void Update()
        {
            float t = (Time.time - startTime) / duration;
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }

            float scale = Mathf.Lerp(startScale, endScale, t);
            transform.localScale = baseLocalScale * scale;

            float blink = 0.5f + 0.5f * Mathf.Sin(Time.time * blinkFrequency * Mathf.PI * 2f);
            var c = baseColor;
            c.a = baseColor.a * (1f - t) * blink;
            tm.color = c;
        }
    }
}
