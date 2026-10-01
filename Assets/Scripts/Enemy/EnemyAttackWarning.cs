using UnityEngine;

namespace PickShoot
{
    // 적의 공격 임박 경고 (Enemies §1). 공격 직전 구간(돌진형의 준비 등)에 머리 위에 느낌표를 띄워 지금 비켜야 함을 알린다.
    // 표시는 자식 오브젝트의 SpriteRenderer(시트 글꼴의 '!' 글리프, 월드 텍셀 크기) 한 장을 켜고 끄는 것뿐이며 깜빡이지 않는다 —
    // 박자는 몸의 텔레그래프(포즈 교대)가 이미 알린다. 언제 켜고 끌지는 행동(EnemyCharger 등)이 정하고, 이 컴포넌트는 스스로 사라지지 않는다.
    // 자식 렌더러라 몸의 좌우 반전·히트 플래시(머티리얼 교체)를 타지 않는다. 위치는 정지 포즈의 머리 위로 프리팹에 고정한다 —
    // 포즈가 낮게 바뀌어도(돌진 준비의 액션 포즈) 느낌표가 따라 내려가지 않는다.
    public class EnemyAttackWarning : MonoBehaviour
    {
        [SerializeField] SpriteRenderer marker;

        void Awake()
        {
            if (marker == null)
                throw new System.Exception($"[PickShoot] EnemyAttackWarning.marker 미설정 ({name}). 느낌표를 그릴 자식 SpriteRenderer 필요.");
            if (marker.sprite == null)
                throw new System.Exception($"[PickShoot] EnemyAttackWarning.marker에 스프라이트가 없음 ({name}).");
            marker.enabled = false;
        }

        public void Show() => marker.enabled = true;
        public void Hide() => marker.enabled = false;
    }
}
