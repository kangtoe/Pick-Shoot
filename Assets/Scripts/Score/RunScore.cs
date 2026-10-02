using UnityEngine;

namespace PickShoot
{
    // 러닝의 누적 스코어 (GDD §3.3). 씬에 정확히 하나 존재해야 한다 (GameManager 오브젝트).
    // 플레이어가 적을 죽일 때마다 적별 값(EnemyData.ScoreValue)이 더해지고, 그 자리에 "+N"이 등속으로 위로 떠오르는 노란 플로팅 텍스트로 뜬다 (DamageNumbers의 Rise).
    // 총점은 줄지 않는다 — 리롤 조건(UpgradeManager)은 차감이 아니라 총점이 문턱에 닿았는지로 본다. 씬 재시작은 씬 리로드라 0으로 돌아간다.
    // 적립은 스폰된 적(EnemyHealth)이 정적 Award로 부르고, 읽는 쪽(HUD·카드 화면)은 직렬화 참조로 Total을 본다.
    public class RunScore : MonoBehaviour
    {
        // 처치 스코어 플로팅 텍스트의 색. 기본값은 시트의 노란 패널 색 (#FFB84C).
        [SerializeField] Color popupColor = new Color32(255, 184, 76, 255);
        // 플로팅 텍스트의 출발점을 전달받은 자리(죽은 적의 위쪽 가장자리)에서 올리는 거리 (월드 유닛). 죽는 타격의 데미지 숫자가 같은 프레임에
        // 같은 자리에서 뜬다 — 그쪽은 금방 멈춰 가라앉고 이쪽은 계속 올라 갈라지지만, 첫 프레임들이 겹치지 않게 한 줄 위에서 출발시킨다.
        // 글자 한 줄은 16텍셀 × 글자 배율 2 = 월드 8텍셀 = 0.5유닛.
        [SerializeField, Min(0f)] float popupRiseOffset = 0.5f;

        static RunScore instance;

        public int Total { get; private set; }

        void Awake()
        {
            if (instance != null && instance != this)
                throw new System.Exception($"[PickShoot] RunScore가 씬에 둘 이상 존재 ({instance.name}, {name}).");
            instance = this;
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        // 적을 죽인 보상. worldPos는 플로팅 텍스트의 기준점 (보통 죽은 스프라이트의 위쪽 가장자리). 0은 보상 없는 적 — 적립도 표시도 없다.
        public static void Award(int amount, Vector3 worldPos)
        {
            if (instance == null)
                throw new System.Exception("[PickShoot] 씬에 RunScore가 없습니다. 처치 스코어 적립용 RunScore 오브젝트(GameManager) 필요.");
            if (amount < 0)
                throw new System.Exception($"[PickShoot] RunScore.Award: 스코어가 음수 ({amount}). EnemyData.scoreValue 확인.");
            if (amount == 0) return;
            instance.Add(amount);
            DamageNumbers.Show(worldPos + Vector3.up * instance.popupRiseOffset, $"+{amount}", instance.popupColor, DamageNumbers.Motion.Rise);
        }

        // 플로팅 텍스트 없이 총점만 올린다. Award와 디버그 도구(DebugToolsWindow)가 쓴다.
        public void Add(int amount)
        {
            if (amount <= 0)
                throw new System.Exception($"[PickShoot] RunScore.Add: 더할 스코어가 0 이하 ({amount}).");
            Total += amount;
        }
    }
}
