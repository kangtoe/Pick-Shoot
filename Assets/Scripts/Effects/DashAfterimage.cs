using UnityEngine;

namespace PickShoot
{
    // 대시 궤적에 남는 잔상 한 장. 원본 스프라이트의 그 순간 모습(프레임·좌우 반전·크기)을 복사해 제자리에 두고 서서히 사라진다.
    // 복사할 그림이 런타임에 정해지므로 프리팹 없이 Spawn이 오브젝트를 직접 만든다. 색·수명은 호출자(PlayerMovement)가 정한다.
    // 원본과 같은 소팅 오더에 두고 z만 뒤로 물린다 — 같은 오더 안에서는 z가 작은 쪽이 앞이라(Renderer2D 정렬 축) 잔상이
    // 늘 원본 뒤에 깔리면서도 탄환·적보다는 위에 보인다.
    [RequireComponent(typeof(SpriteRenderer))]
    public class DashAfterimage : MonoBehaviour
    {
        const float BehindSourceZ = 0.01f;

        SpriteRenderer sr;
        Color baseColor;
        float duration;
        float startTime;

        public static void Spawn(SpriteRenderer source, Vector2 position, Color color, float duration)
        {
            if (duration <= 0f)
                throw new System.Exception($"[PickShoot] DashAfterimage.Spawn: 수명({duration})은 0보다 커야 함.");

            var go = new GameObject("DashAfterimage");
            var src = source.transform;
            go.transform.SetPositionAndRotation(new Vector3(position.x, position.y, src.position.z + BehindSourceZ), src.rotation);
            go.transform.localScale = src.lossyScale;

            var ghost = go.AddComponent<DashAfterimage>();
            ghost.sr = go.GetComponent<SpriteRenderer>();
            ghost.sr.sprite = source.sprite;
            ghost.sr.flipX = source.flipX;
            ghost.sr.flipY = source.flipY;
            ghost.sr.sharedMaterial = source.sharedMaterial;
            ghost.sr.sortingLayerID = source.sortingLayerID;
            ghost.sr.sortingOrder = source.sortingOrder;
            ghost.sr.color = color;
            ghost.baseColor = color;
            ghost.duration = duration;
            ghost.startTime = Time.time;
        }

        void Update()
        {
            float t = (Time.time - startTime) / duration;
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }

            var c = baseColor;
            c.a = baseColor.a * (1f - t);
            sr.color = c;
        }
    }
}
