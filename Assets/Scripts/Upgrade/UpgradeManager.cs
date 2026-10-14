using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PickShoot
{
    // 웨이브 클리어 보상의 종류 (GDD §3.4). 한 화면은 한 종류만.
    public enum RewardKind { Relic, Weapon }

    // 웨이브 클리어 시 3장 카드를 제시하고 선택을 대기한다. 보상은 유물 화면과 무기 화면(획득·레벨업·개조 혼합)이 웨이브마다 교대한다 — 홀수 웨이브 클리어는
    // 유물, 짝수는 무기 (GDD §3.4). 한 화면에 섞지 않는다. 개조는 전용 화면 없이 무기 화면의 세 번째 종류다 (Mods §3, 2026-10-09 — 그전 계획은 다섯 주기).
    // 무기 화면은 카드 한 장마다 종류(획득·레벨업·개조)를 먼저 뽑고 그 안에서 대상을 고른다 (Draw) — 모든 후보를 한 줄에 두면 개조 후보(자루 수 × 개조 수)가
    // 화면을 채운다.
    // 게임 흐름 제어를 위해 Time.timeScale=0로 일시정지, 선택 시 원복. WaveManager가 코루틴으로 호출.
    // 카드는 IMGUI 위에 PixelUiSkin(Kenney Interface 시트)으로 그린다. 글꼴이 A-Z 0-9만 있어 UI 문구는 영문 대문자.
    // 배치 (2026-10-14): 두 열 — 왼쪽 상태 열에 제목 `WAVE N CLEAR`(아웃라인 글꼴, 패널 배율)와 부제, `STATS` 스탯 총계 표(StatPanelPainter, 2열),
    // `WEAPONS (7/12)`·`RELICS (5)` 빌드 격자(BuildGridPainter, 패널 배율·6열), 오른쪽 선택지 열에 가로 카드 세 장(UpgradeCardPainter.DrawLandscape)을
    // 세로로 쌓고 그 아래 회색 리롤 칸. 유물 카드에 마우스를 올리면 그 수정치를 더한 새 총계가 스탯 표의 바뀌는 줄에 초록으로 뜬다(미리보기).
    // 16:9 고정(ScreenLetterbox)으로 캔버스가 늘 1920px라 화면 가장자리까지 쓴다. 가로 카드는 세 장의 표가 같은 x에 서서 값을 위아래로 비교하고, 고르는 것과
    // 그 결과(스탯 표)가 좌우로 붙는다. 선택지·상태 좌우를 바꾼 안, 제목을 카드 열 위에 둔 안, 리롤 칸의 색·자리·아이콘을 목업으로 비교해 골랐다
    // (Recordings/reward_landscape_mockup_2026-10-14_*.png·reward_reroll_*_mockup_2026-10-14*.png).
    // 그전(2026-10-07 ~ 10-14)엔 위 띠(왼쪽 제목·부제, 오른쪽 정사각 리롤 버튼) 아래 세로 카드 세 장을 가로로 놓고 카드 밑에 빌드 두 줄(12열 한 줄씩)이었고,
    // 그보다 전엔 빨간 배너·부제·카드·리롤 버튼·빌드 한 줄을 세로로 쌓았다 (Recordings/reward_layout_mockup_2026-10-07.png).
    // 리롤은 레벨 업(PlayerExperience)으로 적립한 리롤이 남아 있을 때 3장을 다시 뽑는다 — 보이는 카드 중 한 장은 반드시 바뀐다 (Weapons §3).
    // 리롤 칸에는 남은 적립 횟수를 적는다. 쓰지 않은 횟수는 다음 카드 화면으로 이어진다.
    // 카드는 (다시) 뽑힐 때마다 화면 오른쪽 밖에서 차례로 들어와 스프링처럼 흔들리며 멎는다 — 멎기 전에는 카드도 리롤 칸도 입력을 받지 않는다.
    // 빌드 격자의 타일에 마우스를 올리면 그 자루·유물·캐릭터의 보기 카드(UpgradeCardPainter.DrawOwnedWeapon·DrawOwnedRelic·DrawOwnedCharacter)가 뜬다 — 카드·리롤 칸을 다 그린 뒤 맨 마지막에
    // (2026-10-12. 그전 2026-10-06부터는 툴팁이었고 카드 전시 칸에도 떴다 — 카드가 다 담게 되어 전시 칸 호버는 걷었다).
    // 러닝 시작 화면(PresentStartAndWait, 2026-10-12)도 여기서 맡는다 — 고르기·멈춤·IsActive를 카드 화면과 같이 써서 HUD 숨김·커서·ESC 차단·녹화 봇(PickShootPlayDriver)이
    // 그대로 따른다. 캐릭터와 시작 무기를 **한 화면**에서 고른다 (Characters §4, 2026-10-13 저녁): 위에 `CHARACTER` 제목과 초상 타일 줄, 아래에 `WEAPON` 제목과
    // 그 캐릭터의 시작 무기 타일 줄, 카드는 하나만 — 캐릭터 타일을 누르면 그 캐릭터가 플레이어 껍데기에 채워지고(SelectCharacter) 캐릭터 카드가 캐릭터 줄 아래에 열리며
    // 무기 구역은 그 아래로 밀려난다. 무기 타일을 누르면 캐릭터 카드가 닫히고 무기 구역(제목·줄·카드)이 캐릭터 줄 바로 아래로 미끄러져 올라오며 무기 카드가 무기 줄
    // 아래에 열린다. 시작은 오른쪽 아래 `PLAY` 버튼 — 무기 카드가 열려 있을 때만 눌린다 (Lv.1 첫 자루). 마우스를 올리는 것은 테두리만 넓히고 카드를 바꾸지 않는다 —
    // 호버로 카드가 바뀌면 밀려나는 무기 줄이 커서에서 달아난다. 미끄러지는 동안(pushSeconds)은 누를 수 없고, 닫히는 캐릭터 카드는 같은 시간에 투명해진다(열릴 땐 반대).
    // 배경은 아레나 바닥 타일 무늬(startTheme — 땅과 잔무늬 변형, 월드 텍셀 배율) 위에 어두운 막(startTint)을 덮은 불투명한 면 — 러닝 전이라 세계를 비칠 이유가 없고,
    // 보상·일시정지 화면의 막(멈춘 바닥 위 70%)과 같은 결이다 (2026-10-13. 검정 불투명은 밋밋했다).
    // 같은 날 낮의 캐릭터 → 무기 두 단계(ESC로 되돌아감), 두 열 안, 아코디언 안, 카드 자리 고정 안을 목업으로 비교해 골랐다 (Characters §4).
    // 카드 세 장 꼴은 풀 전체(6종 이상)가 한 화면에 들지 않는다 (7타일 카드 여섯 장이면 2.9천 px).
    public class UpgradeManager : MonoBehaviour
    {
        [SerializeField] PlayerWeapons weapons;
        [SerializeField] List<WeaponData> weaponPool = new List<WeaponData>();
        // 시작 화면 캐릭터 단계의 후보 (Characters §3, Assets/Data/Characters). 순서가 타일 순서다.
        [SerializeField] List<CharacterData> characterPool = new List<CharacterData>();
        // 유물 화면의 후보 (Relics §2)와 획득한 유물이 쌓이는 곳. 커먼 유물은 반복 획득이 가능하므로 보유 여부와 무관하게 풀 전체가 후보다.
        [SerializeField] PlayerStats stats;
        [SerializeField] List<RelicData> relicPool = new List<RelicData>();
        // 개조 카드의 후보 (Mods §2, Assets/Data/Mods). 무기 화면에서 개조가 없는 자루 × 이 풀의 개조가 후보다.
        [SerializeField] List<ModData> modPool = new List<ModData>();
        [SerializeField] int cardsToShow = 3;
        // 무기 화면에서 카드 한 장의 종류를 뽑는 가중치 (Mods §3). 처음엔 1:1:1, 튜닝 대상. 후보가 없는 종류는 뽑지 않는다.
        [SerializeField, Min(0f)] float acquireWeight = 1f;
        [SerializeField, Min(0f)] float levelUpWeight = 1f;
        [SerializeField, Min(0f)] float modWeight = 1f;
        [SerializeField] PlayerExperience experience;
        [SerializeField] AudioClip selectClip;
        [SerializeField, Range(0f, 1f)] float selectVolume = 0.6f;
        [SerializeField] AudioClip rerollClip;
        [SerializeField, Range(0f, 1f)] float rerollVolume = 0.8f;

        [Header("Presentation")]
        [SerializeField] PixelUiSkin skin;
        [SerializeField, Range(0f, 1f)] float dimAlpha = 0.7f;

        [Header("Card Entrance")]
        // 등장 연출은 위치만 움직이고 크기는 바꾸지 않는다 — 픽셀 UI의 정수 배율을 지킨다. 카드 화면은 timeScale 0이라 실시간 기준.
        [SerializeField, Min(0.01f)] float enterTravelSeconds = 0.15f;   // 화면 아래 밖에서 제자리까지 (가속)
        [SerializeField, Min(0.01f)] float enterWobbleSeconds = 0.35f;   // 제자리에 닿은 뒤 튀었다 멎기까지
        [SerializeField, Min(0f)] float enterOvershootTexels = 12f;      // 첫 튐의 높이 (패널 텍셀)
        [SerializeField, Min(0f)] float enterStaggerSeconds = 0.07f;     // 다음 장이 출발하기까지
        // 흔들림의 감쇠. 되밀림(두 번째 튐)은 첫 튐의 e^(-WobbleDecay/2) — 2.5면 약 29%.
        const float WobbleDecay = 2.5f;

        [Header("Start Screen")]
        // 시작 화면에서 카드가 바뀔 때 무기 구역이 두 자리 사이를 미끄러지는 시간 (실시간 초, 감속). 그동안 입력을 받지 않고 캐릭터 카드가 투명해지거나 나타난다.
        [SerializeField, Min(0.01f)] float pushSeconds = 0.2f;
        // 시작 화면 배경의 바닥 타일 (아레나와 같은 ArenaTheme, Assets/Data/Arena). 땅 타일을 캔버스 가득 깔고 잔무늬 변형을 startVariantRarity칸에 하나꼴로 덧놓는다.
        [SerializeField] ArenaTheme startTheme;
        [SerializeField, Min(1)] int startVariantRarity = 12;
        // 바닥 무늬 위에 덮는 막. 기본은 보상·일시정지 화면과 같은 검정 70% — 무늬가 어두운 갈색으로 비친다.
        [SerializeField] Color startTint = new Color(0f, 0f, 0f, 0.7f);

        // 글꼴에 있는 문자(A-Z 0-9 % + - 공백)만 써야 한다. 없는 문자는 PixelGui가 예외로 알린다.
        // 리롤 칸 (DrawRerollRow, 2026-10-14) — 카드 열 아래의 회색 가로 칸: 되돌이 아이콘(PixelUiSkin.IconReroll), 제목·설명, 오른쪽 끝에 남은 횟수 `2 LEFT`.
        // 그전(2026-10-08 ~ 10-14)엔 위 띠 오른쪽 끝의 정사각 버튼(타일 꼴 판 + 글자 `R` + 아래 띠의 횟수)이었다.
        const string RerollTitle = "REROLL";
        const string RerollDescription = "DRAW NEW CARDS";
        // 리롤 칸 높이 (패널 타일) — 카드(4타일)의 절반이고 9-slice가 그릴 수 있는 최소. 높이와 회색 패널로 선택지가 아님을 알린다.
        const int RerollRowTiles = 2;
        // 남은 리롤이 없을 때(리롤 칸)와 누를 수 없는 PLAY 버튼에 곱하는 색. 판은 단색 사각형이라 GUI.color를 타지 않아 색을 직접 곱한다 — 검은 막으로 덮었더니
        // 직사각형 막이 네 귀의 깎인 자리까지 덮어 모서리가 각져 보였다 (2026-10-08).
        static readonly Color DisabledTint = new Color(0.5f, 0.5f, 0.5f, 1f);
        // 보상 화면 배치 (패널 텍셀, 2026-10-14): 캔버스 좌우 끝 → 두 열 24(96px), 상태 열 → 선택지 열 16(64px), 가로 카드 사이·카드 → 리롤 칸 6(24px).
        // 선택지 열 = 카드 256 × 3 + 24 × 3 + 리롤 칸 128 = 968px — 세로 1080에서 위아래 56px.
        const int SideMarginTexels = 24;
        const int ColumnGapTexels = 16;
        const int CardStackGapTexels = 6;
        // 제목 띠 → 다음 구역 (패널 텍셀, 40px). 보상 화면은 제목 → STATS, 시작 화면은 줄·카드 → 다음 구역. 보상 화면에선 2026-10-08부터 띠 → 카드 간격이었다.
        const int CardGapTexels = 10;
        // 상태 열의 섹션 사이 (STATS → WEAPONS → RELICS, 패널 텍셀 24px).
        const int StatusSectionGapTexels = 6;
        // 상태 열의 스탯 표 (StatPanelPainter) — 2열, 열 사이 패널 텍셀 12(48px). 8개가 4줄.
        const string StatsTitle = "STATS";
        const int StatColumns = 2;
        const int StatGutterTexels = 12;
        // 상태 열 빌드 격자의 타일 배율 — 일시정지 화면 격자·카드 전시 타일과 같은 패널 배율(112px), 상태 열 폭(768px)에 6열 (2026-10-14).
        // 무기 두 줄(12자루)을 잡고 나면 유물 격자는 두 줄 — 12칸, 캐릭터 빼고 유물 11종이 한계다. 넘치면 BuildGridPainter가 예외로 알린다.
        // 그전엔 중간 배율 3(84px, 8열 × 유물 3줄 24칸)이었다 — 카드 밑에 12칸 한 줄씩 두던 때(2026-10-07~14)는 세로가 없어 패널 배율이 들지 않았다.
        // 글자 배율(2, 56px)은 레벨 배지 `LV5`(64px)가 타일보다 넓어 이웃 배지와 붙는다.
        const int StripTileScale = PixelGui.PanelScale;

        // 시작 화면 (PresentStartAndWait) 두 구역의 제목. 보상 화면 제목과 같은 아웃라인 글꼴 패널 배율, 각 줄 위 제 줄에.
        const string CharacterTitle = "CHARACTER";
        const string WeaponTitle = "WEAPON";
        // 무기 타일은 일시정지 격자·카드 전시 칸과 같은 패널 배율 타일(112px)에 리롤 버튼처럼 바깥 흰 선 1텍셀을 두른 누르는 타일(120px) — 인벤토리 타일과 가른다.
        // 캐릭터 타일은 같은 꼴의 초상 타일(ItemTilePainter.DrawPortrait, 144 + 8 = 152px) — 32×32 그림이 테두리 안에 든다.
        // 고른 캐릭터·초점 무기·마우스를 올린 타일은 카드 호버처럼 흰 테두리가 바깥으로 1텍셀 더 넓다. 타일 사이는 그 테두리가 이웃 흰 선에 닿지 않게 6텍셀(24px).
        const int StartTileScale = PixelGui.PanelScale;
        const int StartTileLineTexels = 1;
        const int StartTileGapTexels = 6;
        // 시작 화면의 카드는 8타일 높이 — 보상 카드(9타일)보다 한 타일 낮다. 개조 줄이 없어 가장 긴 획득 카드(수치 특성 둘)가 496px, 캐릭터 카드가 456px로 들고,
        // 9타일이면 제목 둘·타일 줄 둘·카드 하나가 1080을 넘는다 (2026-10-13).
        const int StartCardHeightTiles = 8;
        // 제목 → 타일 줄 (패널 텍셀, 16px). 줄·카드 → 다음 구역은 CardGapTexels(40px).
        const int StartTitleTileGapTexels = 4;
        // 오른쪽 아래 PLAY 버튼 — 시작 화면 타일과 같은 꼴(바깥 흰 선 1텍셀 + 판)에 일반 글꼴 패널 배율 글자. 판은 글자 줄 상자 둘레에 6텍셀(테두리 2 포함)이라
        // 높이가 타일과 같은 28텍셀(112px). 캔버스 오른쪽·아래 끝에서 8텍셀(32px). 무기 카드가 열려 있을 때만 눌리고, 아니면 리롤 버튼의 0회처럼 어둡다.
        const string PlayLabel = "PLAY";
        const int PlayLabelScale = PixelGui.PanelScale;
        const int PlayPadTexels = 6;
        const int PlayMarginTexels = 8;

        // 시작 화면에서 열려 있는 카드. 캐릭터 카드는 캐릭터 줄 아래, 무기 카드는 무기 줄 아래.
        enum StartCard { Character, Weapon }

        // 이번 카드 화면의 후보 전체(유물 화면은 유물 풀 전체, 무기 화면은 획득 + 레벨업 + 개조, 순서 고정). 처음 카드도 리롤도 여기서 뽑는다.
        static readonly UpgradeKind[] Kinds = (UpgradeKind[])System.Enum.GetValues(typeof(UpgradeKind));
        List<UpgradeOption> candidates;
        UpgradeOption[] currentCards;
        int clearedWave;
        RewardKind rewardKind;
        // 이 러닝에서 쓴 리롤 수. 카드 화면마다 초기화하지 않는다 — 적립(레벨 업 횟수)에서 이것을 뺀 만큼이 남은 리롤이다.
        int rerollsUsed;
        // 지금 카드들이 뽑힌 실시간 — 등장 연출의 기준.
        float cardsShownAt;
        bool active;
        bool selected;
        // 시작 화면인가. OnGUI가 시작 화면 배치로 그린다 — characterPool의 캐릭터 줄과 currentCards(고른 캐릭터의 시작 무기 획득 카드)의 무기 줄.
        bool startPick;
        // 고른 캐릭터(플레이어 껍데기에 채워진 것)와 무기 카드가 보이는 무기 — 둘 다 눌러서 정한다.
        int selectedCharacter;
        int focusedWeapon;
        StartCard openCard;
        // 무기 구역이 미끄러지기 시작한 실시간. 음수면 연출 중이 아니다.
        float pushStartedAt = -1f;
        // 두 줄의 타일 그림 (프레임마다 새로 만들지 않는다). 캐릭터는 Awake에서, 무기는 캐릭터를 고를 때.
        Sprite[] characterTiles;
        Sprite[] startTiles;
        // 플레이어 껍데기에 캐릭터를 채우는 곳 — stats와 같은 오브젝트 (Characters §5).
        SpriteWalkCycle playerWalkCycle;
        PlayerAbility playerAbility;

        public bool IsActive => active;

        // 후보가 카드 수 이하면 다시 뽑아도 같은 카드가 순서만 바뀌어 나오므로 리롤 버튼을 두지 않는다. 후보가 카드 수보다 많으면
        // 한 장을 빼도(Reroll) 카드를 채울 수 있다.
        bool RerollOffered => candidates.Count > currentCards.Length;
        // 남은 리롤 = 레벨 업 횟수(레벨 - 1) − 쓴 횟수 (Weapons §3). 버튼에 적는 숫자.
        int RerollsLeft => experience.Level - 1 - rerollsUsed;
        bool RerollAffordable => RerollsLeft > 0;

        void Awake()
        {
            if (weapons == null)
                throw new System.Exception("[PickShoot] UpgradeManager.weapons 미할당.");
            if (weaponPool.Count == 0)
                throw new System.Exception("[PickShoot] UpgradeManager.weaponPool이 비어있음. 최소 1종의 WeaponData 필요 (시작 무기·획득 카드 후보).");
            for (int i = 0; i < weaponPool.Count; i++)
            {
                if (weaponPool[i] == null)
                    throw new System.Exception($"[PickShoot] UpgradeManager.weaponPool[{i}]가 null.");
                if (weaponPool[i].WeaponSprite == null)
                    throw new System.Exception($"[PickShoot] UpgradeManager.weaponPool[{i}] '{weaponPool[i].DisplayName}'에 WeaponSprite 미할당. 시작 화면 타일·획득 카드 전시용 sprite 필요.");
            }
            if (stats == null)
                throw new System.Exception("[PickShoot] UpgradeManager.stats 미할당. 플레이어의 PlayerStats 참조 필요 (유물 획득·카드의 스탯 총계·캐릭터 채우기).");
            playerWalkCycle = stats.GetComponent<SpriteWalkCycle>();
            if (playerWalkCycle == null)
                throw new System.Exception("[PickShoot] UpgradeManager: 플레이어에 SpriteWalkCycle 없음 (캐릭터 그림을 채우는 데 필요).");
            playerAbility = stats.GetComponent<PlayerAbility>();
            if (playerAbility == null)
                throw new System.Exception("[PickShoot] UpgradeManager: 플레이어에 PlayerAbility 없음 (캐릭터 능력을 채우는 데 필요).");
            if (characterPool.Count == 0)
                throw new System.Exception("[PickShoot] UpgradeManager.characterPool이 비어있음. 최소 1종의 CharacterData 필요 (시작 화면 캐릭터 후보, Assets/Data/Characters).");
            characterTiles = new Sprite[characterPool.Count];
            for (int i = 0; i < characterPool.Count; i++)
            {
                if (characterPool[i] == null)
                    throw new System.Exception($"[PickShoot] UpgradeManager.characterPool[{i}]가 null.");
                characterPool[i].Validate(weaponPool);
                characterTiles[i] = characterPool[i].IdleSprite;
            }
            if (relicPool.Count == 0)
                throw new System.Exception("[PickShoot] UpgradeManager.relicPool이 비어있음. 최소 1종의 RelicData 필요 (유물 카드 후보, Assets/Data/Relics).");
            for (int i = 0; i < relicPool.Count; i++)
            {
                if (relicPool[i] == null)
                    throw new System.Exception($"[PickShoot] UpgradeManager.relicPool[{i}]가 null.");
                if (relicPool[i].Icon == null)
                    throw new System.Exception($"[PickShoot] UpgradeManager.relicPool[{i}] '{relicPool[i].DisplayName}'에 Icon 미할당. 유물 카드 전시용 sprite 필요.");
                if (relicPool[i].Modifiers.IsZero)
                    throw new System.Exception($"[PickShoot] UpgradeManager.relicPool[{i}] '{relicPool[i].DisplayName}'의 수정치가 전부 0 — 효과 없는 유물.");
            }
            if (modPool.Count == 0)
                throw new System.Exception("[PickShoot] UpgradeManager.modPool이 비어있음. 최소 1종의 ModData 필요 (개조 카드 후보, Assets/Data/Mods).");
            var marks = new Dictionary<string, ModData>();
            for (int i = 0; i < modPool.Count; i++)
            {
                if (modPool[i] == null)
                    throw new System.Exception($"[PickShoot] UpgradeManager.modPool[{i}]가 null.");
                modPool[i].Validate();
                // 빌드 타일 배지의 머리글자(`LV5H`)로 개조를 가르므로 겹치면 안 된다 (Mods §4.2).
                if (marks.TryGetValue(modPool[i].BadgeMark, out var other))
                    throw new System.Exception($"[PickShoot] UpgradeManager.modPool: '{other.DisplayName}'와 '{modPool[i].DisplayName}'의 머리글자 '{modPool[i].BadgeMark}'가 겹침. 타일 배지에서 구분되지 않는다.");
                marks.Add(modPool[i].BadgeMark, modPool[i]);
            }
            if (acquireWeight <= 0f || levelUpWeight <= 0f || modWeight <= 0f)
                throw new System.Exception($"[PickShoot] UpgradeManager: 카드 종류 가중치는 양수여야 함 (획득 {acquireWeight}·레벨업 {levelUpWeight}·개조 {modWeight}). 종류를 빼려면 후보를 만들지 않는 쪽으로.");
            if (skin == null)
                throw new System.Exception("[PickShoot] UpgradeManager.skin 미할당. PixelUiSkin 자산(Assets/Data/UI) 필요.");
            skin.Validate();
            if (startTheme == null)
                throw new System.Exception("[PickShoot] UpgradeManager.startTheme 미할당. 시작 화면 배경의 바닥 타일 — ArenaTheme 자산(Assets/Data/Arena) 필요.");
            startTheme.Validate();
            if (experience == null)
                throw new System.Exception("[PickShoot] UpgradeManager.experience 미할당. PlayerExperience(GameManager) 참조 필요 (리롤 적립 횟수).");
        }

        // 클리어한 웨이브 번호로 보상 종류를 정한다 (GDD §3.4): 1은 유물, 2는 무기, 다시 유물…. 종류별로 번갈아 오므로 무기 보상은 그전의 절반 빈도.
        public static RewardKind RewardKindForWave(int clearedWave)
        {
            return clearedWave % 2 == 1 ? RewardKind.Relic : RewardKind.Weapon;
        }

        // clearedWave는 헤더("WAVE N CLEAR") 표기와 보상 종류(RewardKindForWave)의 기준.
        public IEnumerator PresentAndWait(int clearedWave)
        {
            if (clearedWave < 1)
                throw new System.Exception($"[PickShoot] UpgradeManager.PresentAndWait: clearedWave가 1 미만 ({clearedWave}).");
            this.clearedWave = clearedWave;
            rewardKind = RewardKindForWave(clearedWave);
            candidates = BuildCandidates(rewardKind);
            if (candidates.Count == 0) yield break;
            currentCards = Draw(candidates);
            yield return WaitForSelection();
        }

        // 러닝 시작 화면 — 캐릭터와 시작 무기를 한 화면에서 (Characters §4, Weapons §2). 고른 무기가 Lv.1 첫 자루로 든다 — 획득 카드를 고르는 것과 같다.
        // 열리면서 첫 캐릭터를 플레이어 껍데기에 채운다 — 어두운 막 뒤의 플레이어가 바로 그 캐릭터다. 고르기 전에는 자루가 없다 (PlayerWeapons). 첫 웨이브 전에
        // WaveManager가 부른다.
        public IEnumerator PresentStartAndWait()
        {
            if (weapons.Slots.Count != 0)
                throw new System.Exception($"[PickShoot] UpgradeManager.PresentStartAndWait: 이미 자루가 {weapons.Slots.Count}개 있음. 시작 무기는 빈 링의 첫 자루로 든다.");
            startPick = true;
            openCard = StartCard.Character;
            pushStartedAt = -1f;
            SelectCharacter(0);
            yield return WaitForSelection();
            startPick = false;
        }

        // 캐릭터를 고른다 — 플레이어 껍데기에 그림·스탯 수정치·능력을 채우고(Characters §5) 무기 줄을 그 캐릭터의 시작 무기로 바꾼다. 다른 캐릭터를 다시 고르면
        // 다시 채운다 — 스탯 총계는 캐릭터 몫이 갈아끼워지고(PlayerStats.SetCharacter), 최대 체력은 PlayerHealth가 총계 변화를 따라간다.
        void SelectCharacter(int index)
        {
            var character = characterPool[index];
            stats.SetCharacter(character);
            playerWalkCycle.SetFrames(character.Frames);
            playerAbility.SetCharacter(character);

            var startWeapons = character.StartWeapons;
            currentCards = new UpgradeOption[startWeapons.Count];
            startTiles = new Sprite[startWeapons.Count];
            for (int i = 0; i < startWeapons.Count; i++)
            {
                currentCards[i] = UpgradeOption.Acquire(startWeapons[i]);
                startTiles[i] = startWeapons[i].WeaponSprite;
            }
            selectedCharacter = index;
            focusedWeapon = 0;
        }

        // 캐릭터 타일 — 다른 캐릭터면 고르고, 무기 카드가 열려 있으면 캐릭터 카드로 바꾼다 (무기 구역이 아래로 밀려난다).
        void ClickCharacter(int index)
        {
            if (index != selectedCharacter)
            {
                SelectCharacter(index);
                if (selectClip != null) SoundPlayer.Play(selectClip, selectVolume);
            }
            if (openCard == StartCard.Weapon) BeginPush(StartCard.Character);
        }

        // 무기 타일 — 카드를 그 무기로 하고, 캐릭터 카드가 열려 있었으면 무기 카드로 바꾼다 (무기 구역이 위로 올라온다). 시작은 PLAY 버튼.
        void ClickWeapon(int index)
        {
            focusedWeapon = index;
            if (openCard == StartCard.Character) BeginPush(StartCard.Weapon);
        }

        void BeginPush(StartCard target)
        {
            openCard = target;
            pushStartedAt = Time.unscaledTime;
        }

        // 무기 구역이 두 자리 사이를 얼마나 왔나 (0 = 떠난 자리, 1 = 도착). 감속 — 처음 빠르고 끝에서 멎는다. 연출 중이 아니면 1.
        float PushProgress()
        {
            if (pushStartedAt < 0f) return 1f;
            float s = (Time.unscaledTime - pushStartedAt) / pushSeconds;
            if (s >= 1f)
            {
                pushStartedAt = -1f;
                return 1f;
            }
            float back = 1f - s;
            return 1f - back * back * back;
        }

        // currentCards 중 하나를 고를 때까지 게임을 멈춘다 (Time.timeScale 0). 카드 화면과 시작 화면이 같이 쓴다.
        IEnumerator WaitForSelection()
        {
            cardsShownAt = Time.unscaledTime;
            active = true;
            selected = false;
            float prevScale = Time.timeScale;
            Time.timeScale = 0f;

            while (!selected) yield return null;

            Time.timeScale = prevScale;
            active = false;
        }

        List<UpgradeOption> BuildCandidates(RewardKind kind)
        {
            var result = new List<UpgradeOption>();
            switch (kind)
            {
                case RewardKind.Relic:
                    // 유물 후보는 늘 풀 전체 — 커먼 유물은 반복 획득이 가능해 빠지는 것이 없고, 유물 화면은 건너뛰는 일이 없다.
                    foreach (var r in relicPool)
                        result.Add(UpgradeOption.Relic(r));
                    return result;

                case RewardKind.Weapon:
                    if (weapons.HasEmptySlot)
                    {
                        foreach (var d in weaponPool)
                            result.Add(UpgradeOption.Acquire(d));
                    }

                    // 최대 레벨(Weapon.MaxLevel) 자루는 레벨업 후보에서 뺀다.
                    for (int i = 0; i < weapons.Slots.Count; i++)
                        if (!weapons.Slots[i].IsMaxLevel)
                            result.Add(UpgradeOption.LevelUpSlot(i));

                    // 개조는 개조가 없는 자루 × 개조 풀 전체 (지금 무기·개조 조합은 모두 유효, Mods §2.5). 개조가 붙은 자루는 레벨업 후보로만 남는다.
                    // 슬롯이 가득 차고 모든 자루가 최대 레벨이며 개조됐으면 후보가 없어 카드 화면을 건너뛴다.
                    for (int i = 0; i < weapons.Slots.Count; i++)
                    {
                        if (weapons.Slots[i].Mod != null) continue;
                        foreach (var m in modPool)
                            result.Add(UpgradeOption.ModSlot(i, m));
                    }
                    return result;

                default:
                    throw new System.Exception($"[PickShoot] UpgradeManager.BuildCandidates: 알 수 없는 보상 종류 {kind}");
            }
        }

        // pool에서 cardsToShow장을 뽑는다. 한 장마다 종류를 먼저 뽑고(KindWeight 가중, 후보가 남은 종류만) 그 종류의 후보 중 하나를 균등하게 고른다 —
        // 개조면 자루와 개조가 함께 정해진다 (Mods §3). 뽑힌 후보는 빠지므로 같은 카드는 두 번 나오지 않고, 종류의 후보가 다 빠지면 그 종류는 더 뽑지 않는다.
        // 유물 화면은 종류가 하나라 풀에서 균등하게 뽑는 것과 같다.
        UpgradeOption[] Draw(List<UpgradeOption> pool)
        {
            var remaining = new List<UpgradeOption>(pool);
            int take = Mathf.Min(cardsToShow, remaining.Count);
            var result = new UpgradeOption[take];
            var ofKind = new List<UpgradeOption>();
            for (int i = 0; i < take; i++)
            {
                var kind = DrawKind(remaining);
                ofKind.Clear();
                foreach (var o in remaining)
                    if (o.Kind == kind) ofKind.Add(o);
                var pick = ofKind[Random.Range(0, ofKind.Count)];
                remaining.Remove(pick);
                result[i] = pick;
            }
            return result;
        }

        // 후보가 남은 종류 중 하나를 가중치로 뽑는다. remaining은 비어 있지 않아야 한다.
        UpgradeKind DrawKind(List<UpgradeOption> remaining)
        {
            float total = 0f;
            foreach (var k in Kinds)
                if (HasKind(remaining, k)) total += KindWeight(k);
            if (total <= 0f)
                throw new System.Exception("[PickShoot] UpgradeManager.DrawKind: 남은 후보가 없음. 호출자가 뽑을 장 수를 후보 수 이하로 맞춰야 함.");
            float r = Random.value * total;
            UpgradeKind last = Kinds[0];
            foreach (var k in Kinds)
            {
                if (!HasKind(remaining, k)) continue;
                last = k;
                r -= KindWeight(k);
                if (r < 0f) return k;
            }
            // Random.value가 1이면 끝까지 남는다 — 마지막 종류.
            return last;
        }

        static bool HasKind(List<UpgradeOption> options, UpgradeKind kind)
        {
            foreach (var o in options)
                if (o.Kind == kind) return true;
            return false;
        }

        // 카드 종류의 가중치. 유물 화면은 유물 한 종류뿐이라 값이 뜻을 갖지 않는다.
        float KindWeight(UpgradeKind kind)
        {
            switch (kind)
            {
                case UpgradeKind.AcquireWeapon: return acquireWeight;
                case UpgradeKind.LevelUp: return levelUpWeight;
                case UpgradeKind.Mod: return modWeight;
                case UpgradeKind.Relic: return 1f;
                default:
                    throw new System.Exception($"[PickShoot] UpgradeManager.KindWeight: 알 수 없는 Kind {kind}");
            }
        }

        void Select(int index)
        {
            currentCards[index].Apply(weapons, stats);
            if (selectClip != null) SoundPlayer.Play(selectClip, selectVolume);
            selected = true;
        }

        // 지금 보이는 카드 중 무작위 한 장을 뺀 후보에서 다시 뽑는다 — 한 장은 반드시 바뀌고, 나머지는 독립 추첨이라 되돌아올 수
        // 있다. 보이는 카드를 전부 빼면 후보가 6장 이하인 초반에는 결과가 정해져 뽑기가 아니게 되기 때문 (Weapons §3). 후보에 같은
        // 카드는 둘 없으므로 동등성으로 빼면 정확하다. 뺀 카드가 그 종류의 마지막 후보였으면 그 종류는 이번 리롤에서 뽑히지 않는다 (Mods §3).
        void Reroll()
        {
            if (!RerollAffordable)
                throw new System.Exception($"[PickShoot] UpgradeManager.Reroll: 남은 리롤이 없는데 호출됨 (레벨 {experience.Level}, 쓴 횟수 {rerollsUsed}).");
            rerollsUsed++;
            var pool = new List<UpgradeOption>(candidates);
            var excluded = currentCards[Random.Range(0, currentCards.Length)];
            if (!pool.Remove(excluded))
                throw new System.Exception("[PickShoot] UpgradeManager.Reroll: 보이는 카드가 후보에 없음. currentCards는 candidates에서 뽑혀야 함.");
            currentCards = Draw(pool);
            cardsShownAt = Time.unscaledTime;
            if (rerollClip != null) SoundPlayer.Play(rerollClip, rerollVolume);
        }

        void OnGUI()
        {
            if (!active) return;

            // 패널·아이콘은 패널 배율, 글자는 그 절반인 글자 배율 (기준 캔버스 픽셀).
            int scale = PixelGui.PanelScale;
            int textScale = PixelGui.TextScale;
            int tile = skin.TileSize * scale;

            // 어둡게 덮는 막은 화면 픽셀 그대로 (캔버스를 열기 전). 시작 화면은 막 대신 제 배경(바닥 무늬)을 캔버스 안에 깐다.
            if (!startPick)
            {
                PixelGui.FillScreen(new Color(0f, 0f, 0f, dimAlpha));
            }

            // 클릭 판정(GUI.Button)도 캔버스 좌표로 하므로 Repaint뿐 아니라 모든 이벤트에서 캔버스를 연다.
            using var canvas = PixelGui.BeginCanvas();

            if (startPick)
            {
                DrawStartBackdrop(canvas);
                DrawStartScreen(canvas);
                return;
            }

            // 오른쪽 선택지 열 — 가로 카드(UpgradeCardPainter.DrawLandscape, 크기 고정)를 세로로 쌓고 그 아래 리롤 칸. 열 전체를 화면 세로 가운데에 두고, 왼쪽 상태 열의
            // 제목이 같은 위 끝에서 시작한다.
            var cardSize = UpgradeCardPainter.LandscapeSize(skin, scale);
            int stackGap = CardStackGapTexels * scale;
            int rerollH = RerollRowTiles * tile;
            int cardCount = currentCards.Length;
            int columnH = cardCount * cardSize.y + cardCount * stackGap + rerollH;
            if (columnH > canvas.Height)
                throw new System.Exception($"[PickShoot] UpgradeManager: 선택지 열 높이 {columnH}px가 캔버스 {canvas.Height}px를 넘음. 카드 수·간격을 줄일 것.");
            int topY = (canvas.Height - columnH) / 2;
            int sideMargin = SideMarginTexels * scale;
            int cardsX = canvas.Width - sideMargin - cardSize.x;
            int statusX = sideMargin;
            int statusW = cardsX - ColumnGapTexels * scale - statusX;

            string title = $"WAVE {clearedWave} CLEAR";
            // 부제가 이 화면의 종류를 알린다 — 유물 화면은 RELIC, 무기 화면은 획득·레벨업·개조가 섞이므로 CARD.
            string subtitle = rewardKind == RewardKind.Relic ? "CHOOSE A RELIC" : "CHOOSE A CARD";
            // 제목은 패널 없이 아웃라인 글꼴 패널 배율, 부제는 바로 아래 글자 배율 (96px).
            int titleH = skin.FontOutlined.lineHeight * scale;
            int lineH = skin.FontPlain.lineHeight * textScale;
            int headerH = titleH + lineH;
            if (PixelGui.MeasureText(skin.FontOutlined, title, scale) > statusW)
                throw new System.Exception($"[PickShoot] UpgradeManager: 제목 '{title}'이 상태 열 폭 {statusW}px를 넘음.");

            var mouse = Event.current.mousePosition;
            // 마지막 카드까지 멎기 전에는 카드도 리롤 칸도 반응하지 않는다 — 날아가는 카드는 자리가 정해지지 않았다.
            float sinceShown = Time.unscaledTime - cardsShownAt;
            bool interactable = sinceShown >= EnterTotalSeconds(cardCount);

            // 카드 자리를 먼저 정한다 — 마우스가 올라간 유물 카드의 수정치를 왼쪽 스탯 표가 미리 보인다. 카드는 화면 오른쪽 밖에서 차례로 들어온다.
            var cardRects = new Rect[cardCount];
            var cardVisible = new bool[cardCount];
            int hoveredCard = -1;
            for (int i = 0; i < cardCount; i++)
            {
                float t = sinceShown - i * enterStaggerSeconds;
                cardVisible[i] = t >= 0f;   // 아직 출발 전이면 그리지 않는다
                var r = new Rect(cardsX, topY + i * (cardSize.y + stackGap), cardSize.x, cardSize.y);
                if (cardVisible[i]) r.x += EnterOffset(t, canvas.Width - cardsX, scale);
                cardRects[i] = r;
                if (interactable && r.Contains(mouse)) hoveredCard = i;
            }
            PlayerStatModifiers? preview = null;
            if (hoveredCard >= 0 && currentCards[hoveredCard].Kind == UpgradeKind.Relic)
                preview = currentCards[hoveredCard].RelicData.Modifiers;

            // 왼쪽 상태 열 — 제목·부제, STATS(2열), WEAPONS·RELICS 격자(패널 배율, 열 폭에 드는 6열). 무기 격자는 최대 슬롯만큼 줄을 잡아 유물 구역이 움직이지 않는다.
            PixelGui.DrawText(skin.FontOutlined, title, statusX, topY, scale);
            PixelGui.DrawText(skin.FontPlain, subtitle, statusX, topY + titleH, textScale);
            int rowTitleH = BuildGridPainter.TitleHeight(skin);
            int sectionGap = StatusSectionGapTexels * scale;
            int y = topY + headerH + CardGapTexels * scale;
            BuildGridPainter.DrawTitle(skin, StatsTitle, statusX, y);
            y += rowTitleH;
            StatPanelPainter.Draw(skin, stats.Total, preview, statusX, y, statusW, StatColumns, StatGutterTexels * scale);
            y += StatPanelPainter.Height(skin, StatColumns) + sectionGap;

            int stripScale = StripTileScale;
            int columns = BuildGridPainter.ColumnsFitting(stripScale, statusW);
            BuildGridPainter.DrawTitle(skin, BuildGridPainter.WeaponTitle(weapons), statusX, y);
            int slot = BuildGridPainter.DrawWeaponGrid(skin, weapons, statusX, y + rowTitleH, stripScale, columns, mouse, out var weaponTile);
            y += rowTitleH + BuildGridPainter.Height(skin, stripScale, BuildGridPainter.WeaponRows(columns)) + sectionGap;
            BuildGridPainter.DrawTitle(skin, BuildGridPainter.RelicTitle(stats), statusX, y);
            int relicTop = y + rowTitleH;
            int relicRows = BuildGridPainter.RowsFitting(skin, stripScale, topY + columnH - relicTop);
            var relic = BuildGridPainter.DrawRelicGrid(skin, stats, statusX, relicTop, stripScale, columns, relicRows, mouse, out bool characterHovered, out var relicTile);

            // 리롤 칸 — 카드 열 아래. 리롤이 뜻 없는 화면(후보가 카드 수 이하)에는 그리지 않는다 (자리는 비워 둔다).
            if (RerollOffered)
            {
                var row = new Rect(cardsX, topY + cardCount * (cardSize.y + stackGap), cardSize.x, rerollH);
                DrawRerollRow(row, mouse, interactable);
            }

            for (int i = 0; i < cardCount; i++)
            {
                if (!cardVisible[i]) continue;
                UpgradeCardPainter.DrawLandscape(skin, currentCards[i], weapons, stats, cardRects[i], scale, textScale, i == hoveredCard);
                // 보이지 않는 버튼으로 클릭만 받는다. 카드 그림은 위에서 직접 그렸다.
                if (interactable && GUI.Button(cardRects[i], GUIContent.none, GUIStyle.none))
                    Select(i);
            }

            // 보기 카드는 하나만, 다 그린 뒤 맨 마지막에 — 카드 위로 뜬다. 멎기 전에는 카드처럼 호버에 반응하지 않는다.
            if (interactable && Event.current.type == EventType.Repaint)
            {
                if (slot >= 0)
                    UpgradeCardPainter.DrawOwnedWeapon(skin, canvas, weapons.Slots[slot], stats, weaponTile);
                else if (characterHovered)
                    UpgradeCardPainter.DrawOwnedCharacter(skin, canvas, stats.Character, relicTile);
                else if (relic != null)
                    UpgradeCardPainter.DrawOwnedRelic(skin, canvas, relic, stats.CountOf(relic), relicTile);
            }
        }

        // 시작 화면 배경 — 땅 타일을 월드 텍셀 배율(패널 배율)로 캔버스 가득 깔고, 칸마다 정해진 해시로 startVariantRarity분의 1꼴로 잔무늬 변형을 덧놓은 뒤 막을 덮는다.
        // 아레나(ArenaDecor)의 흩뿌림과 같은 꼴이지만 난수가 아니라 칸 좌표의 해시라 프레임마다 같다. 타일은 변을 화면 픽셀에 맞춰(PixelGui.FillTiles·DrawTileSnapped)
        // 720p에서도 틈이 없다.
        void DrawStartBackdrop(PixelGui.CanvasScope canvas)
        {
            int tile = skin.TileSize * PixelGui.PanelScale;
            var area = new Rect(0, 0, canvas.Width, canvas.Height);
            PixelGui.FillTiles(startTheme.Floor, area, tile);
            var variants = startTheme.FloorVariants;
            if (variants.Length > 0)
            {
                int columns = (canvas.Width + tile - 1) / tile;
                int rows = (canvas.Height + tile - 1) / tile;
                for (int y = 0; y < rows; y++)
                    for (int x = 0; x < columns; x++)
                    {
                        // 칸 좌표를 섞은 정수 해시 — 같은 칸은 늘 같은 값.
                        uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663);
                        h ^= h >> 13; h *= 0x5bd1e995u; h ^= h >> 15;
                        if (h % (uint)startVariantRarity != 0) continue;
                        PixelGui.DrawTileSnapped(variants[(int)((h / (uint)startVariantRarity) % (uint)variants.Length)], new Rect(x * tile, y * tile, tile, tile));
                    }
            }
            PixelGui.FillRect(area, startTint);
        }

        // 시작 화면 — 위에서부터 `CHARACTER` 제목·초상 타일 줄, 카드 하나, `WEAPON` 제목·무기 타일 줄을 한 열로 화면 가운데에 (헤더 참조). 블록 높이는 카드가 어느 쪽에
        // 열려 있든 같다. 무기 구역(제목·줄·카드)의 자리는 둘 — 캐릭터 카드가 열렸을 때는 그 카드 아래(무기 카드 없음), 무기 카드가 열렸을 때는 캐릭터 줄 바로 아래
        // (그 아래 무기 카드) — 이고, 카드가 바뀌면 두 자리 사이를 미끄러진다. 캐릭터 카드는 연출 중에도 그려 두고 무기 구역이 그 위를 덮거나 걷는다.
        // 등장 연출은 없다 — 러닝이 시작하는 화면이라 처음부터 누를 수 있다.
        void DrawStartScreen(PixelGui.CanvasScope canvas)
        {
            int scale = PixelGui.PanelScale;
            int textScale = PixelGui.TextScale;
            var cardSize = UpgradeCardPainter.CardSize(skin, scale, StartCardHeightTiles);
            int line = StartTileLineTexels * StartTileScale;
            int characterButton = ItemTilePainter.PortraitSize(StartTileScale) + 2 * line;
            int weaponButton = ItemTilePainter.Size(StartTileScale) + 2 * line;
            int tileGap = StartTileGapTexels * StartTileScale;
            int titleH = skin.FontOutlined.lineHeight * scale;
            int titleGap = StartTitleTileGapTexels * scale;
            int sectionGap = CardGapTexels * scale;
            int characters = characterTiles.Length;
            int weaponsCount = startTiles.Length;
            int characterRowW = characters * characterButton + (characters - 1) * tileGap;
            int weaponRowW = weaponsCount * weaponButton + (weaponsCount - 1) * tileGap;
            if (characterRowW > canvas.Width || weaponRowW > canvas.Width)
                throw new System.Exception($"[PickShoot] UpgradeManager: 시작 화면 타일 줄(캐릭터 {characterRowW}px·무기 {weaponRowW}px)이 캔버스 폭 {canvas.Width}px를 넘음. 줄을 접는 방식을 정할 것.");
            int blockH = titleH + titleGap + characterButton + sectionGap + cardSize.y + sectionGap + titleH + titleGap + weaponButton;
            if (blockH > canvas.Height)
                throw new System.Exception($"[PickShoot] UpgradeManager: 시작 화면 블록 높이 {blockH}px가 캔버스 {canvas.Height}px를 넘음. 간격을 줄이거나 StartCardHeightTiles를 낮출 것.");

            int centerX = canvas.Width / 2;
            int y0 = (canvas.Height - blockH) / 2;
            int characterRowY = y0 + titleH + titleGap;
            int characterCardY = characterRowY + characterButton + sectionGap;
            // 무기 구역의 두 자리. 연출 중엔 떠난 자리에서 도착할 자리로.
            int weaponBlockLow = characterCardY + cardSize.y + sectionGap;
            int weaponBlockHigh = characterRowY + characterButton + sectionGap;
            float push = PushProgress();
            bool pushing = push < 1f;
            int weaponBlockY = openCard == StartCard.Weapon
                ? Mathf.RoundToInt(Mathf.Lerp(weaponBlockLow, weaponBlockHigh, push))
                : Mathf.RoundToInt(Mathf.Lerp(weaponBlockHigh, weaponBlockLow, push));
            int weaponRowY = weaponBlockY + titleH + titleGap;
            int weaponCardY = weaponRowY + weaponButton + sectionGap;
            // 미끄러지는 동안은 아무것도 누를 수 없다 — 보상 카드의 등장과 같다. 테두리 넓히기도 멈춘다.
            bool interactable = !pushing;
            var mouse = Event.current.mousePosition;

            PixelGui.DrawTextCentered(skin.FontOutlined, CharacterTitle, centerX, y0, scale);
            int characterX = centerX - characterRowW / 2;
            for (int i = 0; i < characters; i++)
            {
                var r = new Rect(characterX + i * (characterButton + tileGap), characterRowY, characterButton, characterButton);
                bool hovered = interactable && r.Contains(mouse);
                DrawStartTile(r, characterTiles[i], i == selectedCharacter || hovered, line, portrait: true);
                if (interactable && GUI.Button(r, GUIContent.none, GUIStyle.none))
                {
                    // 캐릭터가 바뀌면 무기 줄이 새 배열이라 이 이벤트는 여기서 끝낸다.
                    ClickCharacter(i);
                    return;
                }
            }
            // 캐릭터 카드는 누를 수 없다 — 캐릭터는 타일로 고른다. 무기 카드로 바뀌는 동안은 투명해지며 사라지고 돌아올 때는 나타난다 — 무기 구역이 그 위를 지나는 동안
            // 겹쳐 보이지 않게. 판·패널·글자가 모두 GUI.color의 알파를 따르고 판은 겹쳐 칠하지 않는다 (PixelGui.FillRect·DrawWeaponPlate). 처음엔 연출 중에도 그대로
            // 그려 두고 무기 구역이 덮게 했는데 끝에서 뚝 사라졌다 (2026-10-13).
            if (openCard == StartCard.Character || pushing)
            {
                float alpha = !pushing ? 1f : openCard == StartCard.Weapon ? 1f - push : push;
                var prevColor = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, alpha);
                UpgradeCardPainter.DrawCharacter(skin, characterPool[selectedCharacter], new Rect(centerX - cardSize.x / 2, characterCardY, cardSize.x, cardSize.y), scale, textScale, false);
                GUI.color = prevColor;
            }

            PixelGui.DrawTextCentered(skin.FontOutlined, WeaponTitle, centerX, weaponBlockY, scale);
            int weaponX = centerX - weaponRowW / 2;
            for (int i = 0; i < weaponsCount; i++)
            {
                var r = new Rect(weaponX + i * (weaponButton + tileGap), weaponRowY, weaponButton, weaponButton);
                bool hovered = interactable && r.Contains(mouse);
                DrawStartTile(r, startTiles[i], (openCard == StartCard.Weapon && i == focusedWeapon) || hovered, line, portrait: false);
                if (interactable && GUI.Button(r, GUIContent.none, GUIStyle.none))
                {
                    ClickWeapon(i);
                    return;
                }
            }
            // 무기 카드는 무기 구역과 함께 미끄러진다 — 올라올 땐 화면 아래 밖에서 들어오고 내려갈 땐 밖으로 나간다. 누를 수 없다 — 시작은 PLAY 버튼.
            if (openCard == StartCard.Weapon || pushing)
                UpgradeCardPainter.Draw(skin, currentCards[focusedWeapon], weapons, stats, new Rect(centerX - cardSize.x / 2, weaponCardY, cardSize.x, cardSize.y), scale, textScale, false);

            DrawPlayButton(canvas, interactable && openCard == StartCard.Weapon, mouse);
        }

        // 오른쪽 아래 PLAY 버튼 (헤더·상수 참조). 눌리면 초점 무기로 시작한다.
        void DrawPlayButton(PixelGui.CanvasScope canvas, bool enabled, Vector2 mouse)
        {
            int scale = PixelGui.PanelScale;
            int line = StartTileLineTexels * scale;
            int pad = PlayPadTexels * scale;
            int plateW = PixelGui.MeasureText(skin.FontPlain, PlayLabel, PlayLabelScale) + 2 * pad;
            int plateH = skin.FontPlain.lineHeight * PlayLabelScale + 2 * pad;
            int margin = PlayMarginTexels * scale;
            var button = new Rect(canvas.Width - margin - plateW - 2 * line, canvas.Height - margin - plateH - 2 * line, plateW + 2 * line, plateH + 2 * line);
            bool hovered = enabled && button.Contains(mouse);
            var tint = enabled ? Color.white : DisabledTint;
            if (hovered)
            {
                int o = UpgradeCardPainter.HoverOutlineTexels * scale;
                PixelGui.FillPlateShape(new Rect(button.x - o, button.y - o, button.width + 2 * o, button.height + 2 * o), scale, Color.white);
            }
            PixelGui.FillPlateShape(button, scale, Color.white * tint);
            var plate = new Rect(button.x + line, button.y + line, plateW, plateH);
            PixelGui.DrawWeaponPlate(skin, plate, scale, tint);
            var prevColor = GUI.color;
            GUI.color = prevColor * tint;
            PixelGui.DrawTextCentered(skin.FontPlain, PlayLabel, (int)plate.center.x, (int)plate.y + pad, PlayLabelScale);
            GUI.color = prevColor;
            if (enabled && GUI.Button(button, GUIContent.none, GUIStyle.none))
                Select(focusedWeapon);
        }

        // 시작 화면의 타일 — 리롤 버튼과 같이 누르는 것임을 바깥 흰 선으로 보인다: button 가득 흰 판 모양, 그 안쪽에 배지 없는 타일(무기는 아이템 타일, 캐릭터는 초상
        // 타일 — 이름은 카드가 말한다). 강조(고른 캐릭터·초점 무기·마우스를 올린 타일)면 카드 호버처럼 흰 판 모양을 한 텍셀 더 크게 먼저 깐다.
        void DrawStartTile(Rect button, Sprite sprite, bool highlighted, int line, bool portrait)
        {
            if (highlighted)
            {
                int o = UpgradeCardPainter.HoverOutlineTexels * StartTileScale;
                PixelGui.FillPlateShape(new Rect(button.x - o, button.y - o, button.width + 2 * o, button.height + 2 * o), StartTileScale, Color.white);
            }
            PixelGui.FillPlateShape(button, StartTileScale, Color.white);
            if (portrait) ItemTilePainter.DrawPortrait(skin, sprite, (int)button.x + line, (int)button.y + line, StartTileScale);
            else ItemTilePainter.Draw(skin, sprite, null, (int)button.x + line, (int)button.y + line, StartTileScale);
        }

        // 리롤 칸 (UpgradeCardPainter.DrawLandscapeAction) — 레벨 업으로 적립한 리롤이 남아 있으면 누를 수 있다. 0회면 칸 전체를 어둡게 하고 `0 LEFT`를 빨강으로 —
        // 카드 열이 움직이지 않고, 리롤이 있다는 것도 읽힌다. 호버는 카드와 같은 테두리 넓히기.
        void DrawRerollRow(Rect row, Vector2 mouse, bool interactable)
        {
            bool affordable = RerollAffordable;
            bool enabled = interactable && affordable;
            bool hovered = enabled && row.Contains(mouse);
            UpgradeCardPainter.DrawLandscapeAction(skin, row, skin.IconReroll, RerollTitle, RerollDescription, $"{RerollsLeft} LEFT",
                affordable ? Color.white : skin.PenaltyText, affordable ? Color.white : DisabledTint, hovered, PixelGui.PanelScale, PixelGui.TextScale);
            if (enabled && GUI.Button(row, GUIContent.none, GUIStyle.none))
                Reroll();
        }

        // 카드 등장의 오프셋(캔버스 픽셀, 출발점 쪽이 +). 보상 화면은 가로(화면 오른쪽 밖에서), 2026-10-14 전 세로 카드 때는 세로(화면 아래 밖에서)였다.
        // t는 그 카드가 출발한 뒤 지난 실시간(초), startOffset은 출발점.
        // 화면 밖에서 정지 상태로 출발해 가속하며 올라와 제자리에 최고 속도로 닿는다 — 이어지는 튐이 그 충격으로 읽힌다.
        // 닿은 뒤는 감쇠 사인 한 주기: 지나쳐 튀었다(w=0.25) → 되돌아와(0.5) → 반대로 살짝 되밀렸다(0.75) → 제자리(1).
        // 끝에서 정확히 0이라 멎을 때 튀지 않는다. 첫 튐의 높이가 enterOvershootTexels가 되도록 진폭을 맞춘다.
        int EnterOffset(float t, int startOffset, int scale)
        {
            if (t < enterTravelSeconds)
            {
                float s = t / enterTravelSeconds;
                return Mathf.RoundToInt(startOffset * (1f - s * s));
            }
            float w = (t - enterTravelSeconds) / enterWobbleSeconds;
            if (w >= 1f) return 0;
            float amplitude = enterOvershootTexels * scale / Mathf.Exp(-WobbleDecay * 0.25f);
            return -Mathf.RoundToInt(amplitude * Mathf.Exp(-WobbleDecay * w) * Mathf.Sin(2f * Mathf.PI * w));
        }

        // 마지막 카드가 멎기까지 걸리는 실시간(초).
        float EnterTotalSeconds(int cardCount)
        {
            return (cardCount - 1) * enterStaggerSeconds + enterTravelSeconds + enterWobbleSeconds;
        }
    }
}
