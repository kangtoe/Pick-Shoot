using UnityEngine;

namespace PickShoot
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class Impact : MonoBehaviour
    {
        [SerializeField] float duration = 0.25f;
        [SerializeField] float startScale = 0.6f;
        [SerializeField] float endScale = 1.8f;

        SpriteRenderer sr;
        Color baseColor;
        float startTime;

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            baseColor = sr.color;
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
            transform.localScale = new Vector3(scale, scale, 1f);
            var c = baseColor;
            c.a = baseColor.a * (1f - t);
            sr.color = c;
        }
    }
}
