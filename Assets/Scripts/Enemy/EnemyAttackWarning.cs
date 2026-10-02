using UnityEngine;

namespace PickShoot
{
    // 적의 공격 임박 경고 (Enemies §1). 공격 직전 구간(돌진형의 준비 등)에 머리 위에 느낌표를 띄워 지금 비켜야 함을 알린다.
    // 표시는 자식 오브젝트의 SpriteRenderer(시트 글꼴의 '!' 글리프, 월드 텍셀 크기) 한 장을 켜고 끄는 것뿐이며 깜빡이지 않는다 —
    // 박자는 몸의 텔레그래프(포즈 교대)가 이미 알린다. 언제 켜고 끌지는 행동(EnemyCharger 등)이 정하고, 이 컴포넌트는 스스로 사라지지 않는다.
    // 자식 렌더러라 몸의 좌우 반전·히트 플래시(머티리얼 교체)를 타지 않는다. 높이는 정지 포즈의 머리 위로 프리팹에 고정한다 —
    // 포즈가 낮게 바뀌어도(돌진 준비의 액션 포즈) 느낌표가 따라 내려가지 않는다. 가로는 머리 위 가운데를 비워 두고 몸이 바라보는 쪽으로
    // 비켜 둔다 — 가운데에는 누적 피해 숫자(DamageNumbers)가 뜨므로 겹치지 않게. 몸이 좌우로 돌면 느낌표도 그쪽으로 옮긴다.
    public class EnemyAttackWarning : MonoBehaviour
    {
        [SerializeField] SpriteRenderer marker;
        // 머리 위 가운데에서 바라보는 쪽으로 비키는 거리 (월드 텍셀). 세 자리 피해 숫자(약 ±8텍셀)와 느낌표 획(±3텍셀)이 닿지 않는 값.
        [SerializeField, Min(0)] int sideOffsetTexels = 12;

        SpriteRenderer body;
        Vector3 markerBase;
        float unitsPerTexel;

        void Awake()
        {
            if (marker == null)
                throw new System.Exception($"[PickShoot] EnemyAttackWarning.marker 미설정 ({name}). 느낌표를 그릴 자식 SpriteRenderer 필요.");
            if (marker.sprite == null)
                throw new System.Exception($"[PickShoot] EnemyAttackWarning.marker에 스프라이트가 없음 ({name}).");
            body = GetComponent<SpriteRenderer>();
            if (body == null)
                throw new System.Exception($"[PickShoot] EnemyAttackWarning: 몸의 SpriteRenderer가 없음 ({name}). 바라보는 방향(flipX)을 읽는 데 필요.");
            markerBase = marker.transform.localPosition;
            unitsPerTexel = 1f / marker.sprite.pixelsPerUnit;
            marker.enabled = false;
        }

        // 몸의 방향은 이동 코드가 Update/FixedUpdate에서 바꾸므로 그 뒤에 따라간다.
        void LateUpdate()
        {
            if (!marker.enabled) return;
            float side = body.flipX ? -1f : 1f;
            marker.transform.localPosition = markerBase + Vector3.right * (side * sideOffsetTexels * unitsPerTexel);
        }

        public void Show() => marker.enabled = true;
        public void Hide() => marker.enabled = false;
    }
}
