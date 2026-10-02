using UnityEngine;

namespace PickShoot
{
    // 러닝의 경험치(XP)와 레벨 (GDD §3.3). 씬에 정확히 하나 존재해야 한다 (GameManager 오브젝트).
    // 플레이어가 적을 죽일 때마다 적별 값(EnemyData.ExpValue)이 더해지고, 그 자리에 "XP N"이 등속으로 위로 떠오르는 노란 플로팅 텍스트로 뜬다 (DamageNumbers의 Rise).
    // 누적 XP가 문턱에 닿을 때마다 레벨이 하나 오른다 (레벨 1 시작). 총량은 줄지 않고, 씬 재시작은 씬 리로드라 0으로 돌아간다.
    // 레벨 업의 효과는 여기서 주지 않는다 — 최대 체력·완충은 PlayerHealth가 LeveledUp을 받아서, 리롤 적립은 UpgradeManager가 Level에서 세어서.
    // 적립은 스폰된 적(EnemyHealth)이 정적 Award로 부르고, 읽는 쪽(HUD·일시정지 화면·카드 화면)은 직렬화 참조로 본다.
    public class PlayerExperience : MonoBehaviour
    {
        // 레벨 문턱: 누적 XP가 k번째(0부터) 값에 닿으면 k+1번째 레벨 업 (레벨 k+2). 앞 문턱은 여기 적고(둘 이상, 올라가게), 목록이 끝나면 마지막 둘의 합
        // (피보나치식)으로 이어 간다 — 처치 수는 웨이브마다 선형으로 늘지만 문턱은 지수로 늘어 레벨 업이 점점 귀해진다. 리롤 문턱이던 표를 그대로 옮긴 것이라
        // 레벨 업(= 리롤 적립) 시점은 그전과 같다 (Weapons §3).
        [SerializeField] int[] levelThresholds = { 10, 20 };
        // 처치 경험치 플로팅 텍스트의 색. 기본값은 시트의 노란 패널 색 (#FFB84C).
        [SerializeField] Color popupColor = new Color32(255, 184, 76, 255);
        // 플로팅 텍스트의 출발점을 전달받은 자리(죽은 적의 위쪽 가장자리)에서 올리는 거리 (월드 유닛). 죽는 타격의 데미지 숫자가 같은 프레임에
        // 같은 자리에서 뜬다 — 그쪽은 금방 멈춰 가라앉고 이쪽은 계속 올라 갈라지지만, 첫 프레임들이 겹치지 않게 한 줄 위에서 출발시킨다.
        // 글자 한 줄은 16텍셀 × 글자 배율 2 = 월드 8텍셀 = 0.5유닛.
        [SerializeField, Min(0f)] float popupRiseOffset = 0.5f;

        static PlayerExperience instance;

        public int Total { get; private set; }
        // 1부터. 레벨 업 횟수는 Level - 1.
        public int Level { get; private set; } = 1;
        // 지금 레벨 구간(앞 문턱 → 다음 문턱)에서 찬 비율 0~1. HUD 게이지가 쓴다. 문턱이 올라가므로(Awake에서 보장) 구간 폭은 0이 아니다.
        public float LevelProgress => (float)(Total - PreviousThreshold) / (NextThreshold - PreviousThreshold);
        // 레벨이 오를 때마다 — 한 번의 적립으로 여러 문턱을 넘으면 레벨마다 한 번씩, Level이 새 값으로 바뀐 뒤.
        public event System.Action LeveledUp;

        // 다음 레벨 업의 문턱과, 지금 레벨에 오른 문턱(레벨 1은 0).
        int NextThreshold => Threshold(Level - 1);
        int PreviousThreshold => Level == 1 ? 0 : Threshold(Level - 2);

        void Awake()
        {
            if (instance != null && instance != this)
                throw new System.Exception($"[PickShoot] PlayerExperience가 씬에 둘 이상 존재 ({instance.name}, {name}).");
            if (levelThresholds == null || levelThresholds.Length < 2)
                throw new System.Exception("[PickShoot] PlayerExperience.levelThresholds는 둘 이상이어야 함 — 그 뒤를 마지막 둘의 합으로 이어 간다.");
            if (levelThresholds[0] <= 0)
                throw new System.Exception($"[PickShoot] PlayerExperience.levelThresholds[0]이 0 이하 ({levelThresholds[0]}). 첫 레벨 업에도 경험치가 필요해야 함.");
            for (int i = 1; i < levelThresholds.Length; i++)
                if (levelThresholds[i] <= levelThresholds[i - 1])
                    throw new System.Exception($"[PickShoot] PlayerExperience.levelThresholds[{i}]({levelThresholds[i]})가 앞 값({levelThresholds[i - 1]}) 이하 — 문턱은 올라가야 함 (게이지 구간 폭이 0이 되지 않게).");
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
                throw new System.Exception("[PickShoot] 씬에 PlayerExperience가 없습니다. 처치 경험치 적립용 PlayerExperience 오브젝트(GameManager) 필요.");
            if (amount < 0)
                throw new System.Exception($"[PickShoot] PlayerExperience.Award: 경험치가 음수 ({amount}). EnemyData.expValue 확인.");
            if (amount == 0) return;
            instance.Add(amount);
            DamageNumbers.Show(worldPos + Vector3.up * instance.popupRiseOffset, $"XP{amount}", instance.popupColor, DamageNumbers.Motion.Rise);
        }

        // 플로팅 텍스트 없이 적립만 한다. Award와 디버그 도구(DebugToolsWindow)가 쓴다. 문턱을 넘은 만큼 레벨이 오르고 레벨마다 LeveledUp이 난다.
        public void Add(int amount)
        {
            if (amount <= 0)
                throw new System.Exception($"[PickShoot] PlayerExperience.Add: 더할 경험치가 0 이하 ({amount}).");
            Total += amount;
            while (Total >= NextThreshold)
            {
                Level++;
                LeveledUp?.Invoke();
            }
        }

        // index번째(0부터) 레벨 업의 문턱. 목록 안은 그대로, 밖은 마지막 둘의 합으로 이어 간다. int를 넘으면 오류 — 그만큼 레벨이 오를 일은 없다.
        int Threshold(int index)
        {
            if (index < levelThresholds.Length) return levelThresholds[index];
            long a = levelThresholds[levelThresholds.Length - 2];
            long b = levelThresholds[levelThresholds.Length - 1];
            for (int i = levelThresholds.Length; i <= index; i++)
            {
                long next = a + b;
                if (next > int.MaxValue)
                    throw new System.Exception($"[PickShoot] PlayerExperience: {index}번째 레벨 문턱이 int 범위를 넘음.");
                a = b;
                b = next;
            }
            return (int)b;
        }
    }
}
