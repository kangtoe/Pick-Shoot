using UnityEngine;

namespace PickShoot
{
    // 적 스폰 예고 마커. WaveManager가 스폰 지점에 만들어 Begin(duration)으로 예고 시간을 넘기고, 대기가 끝나면 직접 파괴한다.
    // 이 컴포넌트는 스스로 사라지지 않는다 — 스폰·취소 시점을 아는 쪽은 WaveManager 하나다.
    // 표시는 시트의 작은 X 아이콘(Interface ui_72) 한 장을 켜고 끄는 깜빡임뿐이다. 남은 시간을 게이지로 보여주지 않고,
    // 마지막 finalBlinkDuration 동안 깜빡임이 빨라지는 것으로만 "곧 나온다"를 알린다. 픽셀아트라 알파 페이드는 쓰지 않는다.
    [RequireComponent(typeof(SpriteRenderer))]
    public class SpawnWarningMarker : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] float blinkFrequency = 1f;
        [SerializeField, Min(0f)] float finalBlinkDuration = 0.6f;
        [SerializeField, Min(0.01f)] float finalBlinkFrequency = 4f;

        SpriteRenderer sr;
        float duration = -1f;
        float startTime;

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            if (sr.sprite == null)
                throw new System.Exception($"[PickShoot] SpawnWarningMarker '{name}': SpriteRenderer에 스프라이트가 없음.");
        }

        // 예고 시작. warningDuration 뒤가 스폰(또는 취소) 시점이며, 파괴는 호출자가 한다.
        public void Begin(float warningDuration)
        {
            if (warningDuration <= 0f)
                throw new System.Exception($"[PickShoot] SpawnWarningMarker.Begin: 예고 시간이 0 이하 ({warningDuration}).");
            duration = warningDuration;
            startTime = Time.time;
            sr.enabled = true;
        }

        void Update()
        {
            if (duration <= 0f)
                throw new System.Exception($"[PickShoot] SpawnWarningMarker '{name}': Begin 호출 없이 활성화됨.");
            float elapsed = Time.time - startTime;
            float finalStart = duration - finalBlinkDuration;
            // 켜짐·꺼짐 같은 길이. 빠른 구간은 그 구간의 시작 시각부터 세어 켜진 상태로 들어간다.
            bool visible = elapsed < finalStart
                ? (Mathf.FloorToInt(elapsed * blinkFrequency * 2f) & 1) == 0
                : (Mathf.FloorToInt((elapsed - finalStart) * finalBlinkFrequency * 2f) & 1) == 0;
            sr.enabled = visible;
        }
    }
}
