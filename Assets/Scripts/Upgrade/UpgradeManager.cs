using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PickShoot
{
    // 웨이브 클리어 보상의 종류 (GDD §3.4). 한 화면은 한 종류만.
    public enum RewardKind { Relic, Weapon }

    // 웨이브 클리어 시 3장 카드를 제시하고 선택을 대기한다. 보상은 유물 화면과 무기 화면(획득/강화 혼합)이 웨이브마다 교대한다 — 홀수 웨이브 클리어는
    // 유물, 짝수는 무기 (GDD §3.4). 한 화면에 섞지 않는다. 무기 개조(Relics §5 2단계)가 들어오면 다섯 주기로 바뀐다.
    // 게임 흐름 제어를 위해 Time.timeScale=0로 일시정지, 선택 시 원복. WaveManager가 코루틴으로 호출.
    // 카드는 IMGUI 위에 PixelUiSkin(Kenney Interface 시트)으로 그린다. 글꼴이 A-Z 0-9만 있어 UI 문구는 영문 대문자.
    // 배치 (2026-10-07): 제목 `WAVE N CLEAR`(아웃라인 글꼴, 패널 배율)와 그 아래 부제가 카드 묶음의 왼쪽 끝 위에, 리롤 버튼이 오른쪽 끝 위에 — 둘이 한 띠를 이루고
    // 그 아래 카드 세 장, 카드 밑에 빌드 두 줄(BuildGridPainter — `WEAPONS (7/12)` 보유 자루, `RELICS (5)` 보유 유물, 타일 배율 3)이 카드 왼쪽 끝에 맞춰 선다.
    // 그전에는 빨간 배너·부제·카드·리롤 버튼·빌드 한 줄을 세로로 쌓아 1060px를 썼고, 빌드는 그 화면 종류의 한 줄뿐이었다 — 제목과 버튼을 위 띠로 올려 번 자리에
    // 무기·유물 줄을 늘 함께 둔다 (유물 화면에서도 보유 자루를, 무기 화면에서도 보유 유물을 본다). 배너 패널은 뺐다 — 패널 배율 배너(128px)면 둘째 줄이 16px 넘친다.
    // 띠·줄을 캔버스 가장자리가 아니라 카드 묶음에 맞추므로 화면비가 달라도 한 덩어리로 읽힌다. 안은 실제 시트로 목업해 골랐다
    // (Recordings/reward_layout_mockup_2026-10-07.png, 절차는 Docs/Guides/PixelUiMockup.md).
    // 리롤 버튼은 레벨 업(PlayerExperience)으로 적립한 리롤이 남아 있을 때 3장을 다시 뽑는다 — 보이는 카드 중 한 장은 반드시 바뀐다 (Weapons §3).
    // 버튼에는 남은 적립 횟수를 적는다. 쓰지 않은 횟수는 다음 카드 화면으로 이어진다.
    // 카드는 (다시) 뽑힐 때마다 화면 아래 밖에서 차례로 튀어 올라 스프링처럼 흔들리며 멎는다 — 멎기 전에는 카드도 버튼도 입력을 받지 않는다.
    // 타일과 카드 전시 칸에 마우스를 올리면 툴팁(TooltipPainter)이 뜬다 — 카드·버튼을 다 그린 뒤 맨 마지막에 (2026-10-06).
    public class UpgradeManager : MonoBehaviour
    {
        [SerializeField] PlayerWeapons weapons;
        [SerializeField] List<WeaponData> weaponPool = new List<WeaponData>();
        // 유물 화면의 후보 (Relics §2)와 획득한 유물이 쌓이는 곳. 커먼 유물은 반복 획득이 가능하므로 보유 여부와 무관하게 풀 전체가 후보다.
        [SerializeField] PlayerStats stats;
        [SerializeField] List<RelicData> relicPool = new List<RelicData>();
        [SerializeField] int cardsToShow = 3;
        [SerializeField] PlayerExperience experience;
        [SerializeField] AudioClip selectClip;
        [SerializeField] AudioClip rerollClip;

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

        // 글꼴에 있는 문자(A-Z 0-9 % + - 공백)만 써야 한다. 없는 문자는 PixelGui가 예외로 알린다.
        // 리롤 버튼은 아이템 타일(ItemTilePainter)과 같은 꼴 — 단색 판 위에 글자 `R`(뒤에 리롤 아이콘으로 바꿀 자리), 아래쪽 띠에 남은 적립 횟수. 그림이 띠 위 칸의
        // 가운데(버튼 중앙보다 위)에 오는 것도 타일과 같다 (2026-10-08. 2026-10-07엔 회색 패널 `REROLL (2)` 한 줄 → `R` + 아래로 늘어뜨린 수 배지였다. 회색 패널에
        // 띠를 넣는 안은 테두리 6텍셀이 칸을 먹거나 띠가 테두리를 잘라 뺐다 — Recordings/reroll_band_mockup_2026-10-08.png).
        const string RerollLabel = "R";
        // 버튼의 배율 — 일시정지 화면 격자의 타일과 같은 패널 배율 4, 한 변은 타일과 같은 28텍셀(112px). 띠(96px)보다 커서 띠 세로 가운데에 두면 위아래로 8px씩
        // 나온다 — 아래쪽은 카드 간격(72px)으로. 글자 배율 64px → 배율 3·32텍셀 96px → 여기로 두 번 키웠다 (2026-10-08).
        // `R`은 일반 글꼴 버튼 배율(획 12텍셀 = 48px, 띠 위 칸 68px) — 아웃라인 글꼴로 잠시 바꿨다가 되돌렸고, 배율 5(획 60px, 칸에 드는 최대)로 키웠다가
        // 약간 작게 4로 (2026-10-08). 아이콘으로 바꿀 때도 버튼 배율 (16텍셀 = 64px).
        // 모서리는 다른 판과 같은 1텍셀 깎기 — 2단 깎기 안도 목업했으나 판을 다 바꿔야 해서 두었다 (Recordings/reroll_corner_mockup_2026-10-08.png).
        const int RerollButtonScale = PixelGui.PanelScale;
        const int RerollLabelScale = PixelGui.PanelScale;
        // 버튼 바깥을 두르는 흰 선의 두께(텍셀) — 카드(9-slice 패널)의 바깥 흰 선과 같은 꼴. 인벤토리 타일에는 없고 누르는 것에만 둔다 (2026-10-08).
        // 판은 그 안쪽 26텍셀(104px)이라 띠 위 칸은 60px — `R`(48px)이 든다.
        const int RerollWhiteLineTexels = 1;
        // 남은 리롤이 없을 때 판·띠·글자에 곱하는 색. 판·띠는 단색 사각형이라 GUI.color를 타지 않아 색을 직접 곱한다 — 검은 막으로 덮었더니 직사각형 막이
        // 네 귀의 깎인 자리까지 덮어 모서리가 각져 보였다 (2026-10-08).
        static readonly Color DisabledTint = new Color(0.5f, 0.5f, 0.5f, 1f);
        // 위 띠(제목·부제) → 카드, 카드 → 빌드 줄 (패널 텍셀), 무기 줄 → 유물 제목 (글자 텍셀, 일시정지 화면의 섹션 간격보다 좁다).
        // 띠 → 카드는 10텍셀(40px)이었다가 카드 위로 솟던 배지(32px)가 띠에 닿지 않게 18(72px)로 벌렸고, 2026-10-08 카드가 종류 배지를 머리의 글자 제목으로
        // 옮겨(UpgradeCardPainter) 카드 위 끝이 패널 변이 되며 10으로 되돌렸다.
        // 카드 → 빌드 줄은 2텍셀(8px)이었다가 6(24px)으로 — 줄이 카드에 붙어 보였고, 호버 테두리가 카드 밖으로 4px 나온다 (2026-10-08).
        // 블록 = 띠 96 + 40 + 카드 512 + 24 + 줄 124 × 2 + 16 = 936px — 세로 1080에서 위아래 72px 남는다 (카드가 9타일이던 때 1032px, 7타일이던 때 872px).
        const int CardGapTexels = 10;
        const int StripGapTexels = 6;
        const int SectionGapTexels = 8;
        // 빌드 줄의 타일 배율. 글자 배율(2, 56px)에서는 레벨 배지 `LV5`(글자 배율 2로 64px)가 타일보다 넓어 이웃 배지와 붙고, 패널 배율(4, 112px)은 세로가
        // 없다. 일시정지 배지와 같은 중간 배율 3(84px) — 배지가 양옆 10px를 남기고 들어간다 (카드의 무기 이름도 3이었다가 2026-10-08 머리 가로 배치로 2).
        const int StripTileScale = 3;

        // 이번 카드 화면의 후보 전체(유물 화면은 유물 풀 전체, 무기 화면은 획득 + 레벨업, 순서 고정). 처음 카드도 리롤도 여기서 뽑는다.
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
        // 툴팁 내용 버퍼 (프레임마다 비우고 채운다).
        readonly List<string> tipFacts = new List<string>();
        readonly List<string> tipNotes = new List<string>();

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
                throw new System.Exception("[PickShoot] UpgradeManager.weaponPool이 비어있음. 최소 1종의 WeaponData 필요 (획득 카드 후보).");
            for (int i = 0; i < weaponPool.Count; i++)
            {
                if (weaponPool[i] == null)
                    throw new System.Exception($"[PickShoot] UpgradeManager.weaponPool[{i}]가 null.");
                if (weaponPool[i].WeaponSprite == null)
                    throw new System.Exception($"[PickShoot] UpgradeManager.weaponPool[{i}] '{weaponPool[i].DisplayName}'에 WeaponSprite 미할당. 획득 카드 전시용 sprite 필요.");
            }
            if (stats == null)
                throw new System.Exception("[PickShoot] UpgradeManager.stats 미할당. 플레이어의 PlayerStats 참조 필요 (유물 획득·카드의 스탯 총계).");
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
            if (skin == null)
                throw new System.Exception("[PickShoot] UpgradeManager.skin 미할당. PixelUiSkin 자산(Assets/Data/UI) 필요.");
            skin.Validate();
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

                    // 최대 레벨(Weapon.MaxLevel) 자루는 레벨업 후보에서 뺀다. 슬롯이 가득 차고 전부 최대 레벨이면 후보가 없어 카드 화면을 건너뛴다.
                    for (int i = 0; i < weapons.Slots.Count; i++)
                        if (!weapons.Slots[i].IsMaxLevel)
                            result.Add(UpgradeOption.LevelUpSlot(i));
                    return result;

                default:
                    throw new System.Exception($"[PickShoot] UpgradeManager.BuildCandidates: 알 수 없는 보상 종류 {kind}");
            }
        }

        // pool을 섞어 앞에서 cardsToShow장을 뽑는다.
        UpgradeOption[] Draw(List<UpgradeOption> pool)
        {
            var order = new List<UpgradeOption>(pool);

            // Fisher-Yates
            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }

            int take = Mathf.Min(cardsToShow, order.Count);
            var result = new UpgradeOption[take];
            for (int i = 0; i < take; i++) result[i] = order[i];
            return result;
        }

        void Select(int index)
        {
            currentCards[index].Apply(weapons, stats);
            if (selectClip != null) SoundPlayer.Play(selectClip);
            selected = true;
        }

        // 지금 보이는 카드 중 무작위 한 장을 뺀 후보에서 다시 뽑는다 — 한 장은 반드시 바뀌고, 나머지는 독립 추첨이라 되돌아올 수
        // 있다. 보이는 카드를 전부 빼면 후보가 6장 이하인 초반에는 결과가 정해져 뽑기가 아니게 되기 때문 (Weapons §3). 후보에 같은
        // 카드는 둘 없으므로 동등성으로 빼면 정확하다.
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
            if (rerollClip != null) SoundPlayer.Play(rerollClip);
        }

        void OnGUI()
        {
            if (!active) return;

            // 패널·아이콘은 패널 배율, 글자는 그 절반인 글자 배율 (기준 캔버스 픽셀).
            int scale = PixelGui.PanelScale;
            int textScale = PixelGui.TextScale;
            int tile = skin.TileSize * scale;

            // 어둡게 덮는 막은 화면 픽셀 그대로 (캔버스를 열기 전).
            GUI.color = new Color(0f, 0f, 0f, dimAlpha);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // 클릭 판정(GUI.Button)도 캔버스 좌표로 하므로 Repaint뿐 아니라 모든 이벤트에서 캔버스를 연다.
            using var canvas = PixelGui.BeginCanvas();

            // 카드 크기는 고정 (UpgradeCardPainter). 어떤 조합이 나와도 세 장이 같은 크기·자리에 놓인다.
            var cardSize = UpgradeCardPainter.CardSize(skin, scale);
            int gap = tile;   // 카드 사이
            int totalW = currentCards.Length * cardSize.x + (currentCards.Length - 1) * gap;
            int centerX = canvas.Width / 2;
            // 카드 묶음의 좌우 끝 — 위 띠(제목·리롤 버튼)와 빌드 줄이 여기에 맞춰 선다.
            int startX = centerX - totalW / 2;
            int blockRight = startX + totalW;

            string title = $"WAVE {clearedWave} CLEAR";
            // 부제가 이 화면의 종류를 알린다 — 유물 화면은 RELIC, 무기 화면은 획득·레벨업이 섞이므로 CARD.
            string subtitle = rewardKind == RewardKind.Relic ? "CHOOSE A RELIC" : "CHOOSE A CARD";
            // 제목은 패널 없이 아웃라인 글꼴 패널 배율, 부제는 바로 아래 글자 배율 — 둘을 합친 것이 위 띠의 높이(96px)다.
            int titleH = skin.FontOutlined.lineHeight * scale;
            int lineH = skin.FontPlain.lineHeight * textScale;
            int headerH = titleH + lineH;
            int cardGap = CardGapTexels * scale;
            int stripGap = StripGapTexels * scale;
            int sectionGap = SectionGapTexels * textScale;

            // 리롤 버튼은 띠 오른쪽 끝의 정사각 타일 (일시정지 격자의 타일 크기).
            bool rerollOffered = RerollOffered;
            int buttonSize = ItemTilePainter.Size(RerollButtonScale);

            // 빌드는 배율 3 타일 두 줄 — 무기 줄과 유물 줄, 각각 제목 줄 + 타일 한 줄 (BuildGridPainter).
            int stripScale = StripTileScale;
            BuildGridPainter.RequireFits(stripScale, canvas.Width);
            int rowTitleH = BuildGridPainter.TitleHeight(skin);
            int rowH = rowTitleH + BuildGridPainter.Height(skin, stripScale, 1);
            int stripsH = rowH + sectionGap + rowH;

            // 위 띠·카드·빌드 두 줄 묶음을 화면 세로 중앙에 놓는다. 리롤 버튼은 띠 안이라 높이에 들지 않는다.
            int blockH = headerH + cardGap + cardSize.y + stripGap + stripsH;
            if (blockH > canvas.Height)
                throw new System.Exception($"[PickShoot] UpgradeManager: 보상 화면 블록 높이 {blockH}px가 캔버스 {canvas.Height}px를 넘음. 간격을 줄일 것.");
            int blockY = (canvas.Height - blockH) / 2;

            PixelGui.DrawText(skin.FontOutlined, title, startX, blockY, scale);
            PixelGui.DrawText(skin.FontPlain, subtitle, startX, blockY + titleH, textScale);

            int cardsY = blockY + headerH + cardGap;
            var mouse = Event.current.mousePosition;
            // 마지막 카드까지 멎기 전에는 카드도 버튼도 반응하지 않는다 — 날아가는 카드는 자리가 정해지지 않았고, 버튼 위를 지나간다.
            float sinceShown = Time.unscaledTime - cardsShownAt;
            bool interactable = sinceShown >= EnterTotalSeconds(currentCards.Length);

            // 리롤 버튼은 띠의 오른쪽 끝, 띠 세로 가운데 — 띠보다 커서 위아래로 나온다. 캔버스 위나 카드까지 닿으면 예외.
            if (rerollOffered)
            {
                var button = new Rect(blockRight - buttonSize, blockY + (headerH - buttonSize) / 2, buttonSize, buttonSize);
                if (button.y < 0 || button.yMax > cardsY)
                    throw new System.Exception($"[PickShoot] UpgradeManager: 리롤 버튼({button.y}~{button.yMax}px)이 캔버스 위 또는 카드 위 끝({cardsY}px)을 넘음. RerollButtonScale이나 CardGapTexels를 맞출 것.");
                DrawRerollButton(button, RerollButtonScale, mouse, interactable);
            }

            // 빌드 줄은 카드보다 먼저 그린다 — 화면 아래에서 올라오는 카드가 그 위를 지나가므로 카드가 앞에 보여야 한다.
            // 툴팁은 하나만, 다 그린 뒤 맨 마지막에. 멎기 전에는 카드처럼 호버에 반응하지 않는다.
            string tipTitle = null;
            Rect tipAnchor = default;
            tipFacts.Clear();
            tipNotes.Clear();
            int stripY = cardsY + cardSize.y + stripGap;
            BuildGridPainter.DrawTitle(skin, BuildGridPainter.WeaponTitle(weapons), startX, stripY);
            int slot = BuildGridPainter.DrawWeaponRow(skin, weapons, startX, stripY + rowTitleH, stripScale, mouse, out var weaponTile);
            stripY += rowH + sectionGap;
            BuildGridPainter.DrawTitle(skin, BuildGridPainter.RelicTitle(stats), startX, stripY);
            var relic = BuildGridPainter.DrawRelicGrid(skin, stats, startX, stripY + rowTitleH, stripScale, 1, mouse, out var relicTile);
            if (interactable && slot >= 0)
            {
                var w = weapons.Slots[slot];
                tipTitle = ItemInfoText.WeaponTitle(w.Data);
                tipAnchor = weaponTile;
                ItemInfoText.WeaponFacts(w.Data, w.Level, stats, tipFacts);
                ItemInfoText.WeaponNotes(w.Data, tipNotes);
            }
            else if (interactable && relic != null)
            {
                tipTitle = ItemInfoText.RelicTitle(relic);
                tipAnchor = relicTile;
                ItemInfoText.RelicFacts(relic, stats.CountOf(relic), tipFacts);
                ItemInfoText.RelicNotes(relic, tipNotes);
            }

            for (int i = 0; i < currentCards.Length; i++)
            {
                float t = sinceShown - i * enterStaggerSeconds;
                if (t < 0f) continue;   // 아직 출발 전
                var r = new Rect(startX + i * (cardSize.x + gap), cardsY, cardSize.x, cardSize.y);
                // 출발점은 카드 위쪽 끝이 화면 아래 끝에 닿는 곳 — 화면 밖에서 들어온다.
                r.y += EnterOffset(t, canvas.Height - cardsY, scale);
                bool hovered = interactable && r.Contains(mouse);
                var showcaseTile = UpgradeCardPainter.Draw(skin, currentCards[i], weapons, stats, r, scale, textScale, hovered);
                // 전시 타일 위의 툴팁은 고른 뒤의 상태 (타일 배지와 같다).
                if (hovered && showcaseTile.Contains(mouse))
                {
                    tipAnchor = showcaseTile;
                    tipTitle = CardTooltip(currentCards[i], tipFacts, tipNotes);
                }
                // 보이지 않는 버튼으로 클릭만 받는다. 카드 그림은 위에서 직접 그렸다.
                if (interactable && GUI.Button(r, GUIContent.none, GUIStyle.none))
                    Select(i);
            }

            if (tipTitle != null && Event.current.type == EventType.Repaint)
                TooltipPainter.Draw(skin, canvas, tipAnchor, tipTitle, tipFacts, tipNotes);
        }

        // 카드가 가리키는 것의 툴팁 — 고른 뒤의 상태. 획득 카드는 슬롯이 아직 없어 시작 레벨(UpgradeOption.Levels, 보통 1), 레벨업은 그 슬롯의 오른 뒤 레벨, 유물은 보유 수 + 1.
        string CardTooltip(UpgradeOption option, List<string> facts, List<string> notes)
        {
            facts.Clear();
            notes.Clear();
            switch (option.Kind)
            {
                case UpgradeKind.AcquireWeapon:
                    ItemInfoText.WeaponFacts(option.AcquireData, option.Levels, stats, facts);
                    ItemInfoText.WeaponNotes(option.AcquireData, notes);
                    return ItemInfoText.WeaponTitle(option.AcquireData);
                case UpgradeKind.LevelUp:
                {
                    var w = weapons.Slots[option.TargetSlotIndex];
                    ItemInfoText.WeaponFacts(w.Data, w.Level + option.Levels, stats, facts);
                    ItemInfoText.WeaponNotes(w.Data, notes);
                    return ItemInfoText.WeaponTitle(w.Data);
                }
                case UpgradeKind.Relic:
                    ItemInfoText.RelicFacts(option.RelicData, stats.CountOf(option.RelicData) + 1, facts);
                    ItemInfoText.RelicNotes(option.RelicData, notes);
                    return ItemInfoText.RelicTitle(option.RelicData);
                default:
                    throw new System.Exception($"[PickShoot] UpgradeManager.CardTooltip: 알 수 없는 Kind {option.Kind}");
            }
        }

        // scale은 버튼의 판·글자 배율. 띠의 숫자는 타일 배지와 같이 글자 배율이다.
        void DrawRerollButton(Rect button, int scale, Vector2 mouse, bool interactable)
        {
            bool affordable = RerollAffordable;
            bool enabled = interactable && affordable;
            bool hovered = enabled && button.Contains(mouse);
            // 할 수 있는 리롤이 없으면(0) 판·띠·글자를 어둡게 — 카드 묶음이 움직이지 않고, 리롤이 있다는 것도 읽힌다.
            var tint = affordable ? Color.white : DisabledTint;
            GUI.color = tint;
            // 호버 표시는 카드와 같이 테두리를 바깥으로 넓히기(UpgradeCardPainter.HoverOutlineTexels) — 버튼보다 한 텍셀 큰 흰 판 모양을 먼저 깐다. 선택 테두리(판보다
            // 작은 배율 3이라 외곽선에 2px 틈)·안쪽 흰 외곽선(띠 폭이 상태마다 달라짐)·비정수 배율 키우기(텍셀이 갈려 깨져 보임)·들림을 거쳤다 (2026-10-08).
            if (hovered)
            {
                int o = UpgradeCardPainter.HoverOutlineTexels * scale;
                PixelGui.FillPlateShape(new Rect(button.x - o, button.y - o, button.width + 2 * o, button.height + 2 * o), scale, Color.white);
            }
            // 흰 선을 버튼 영역 가득 깔고 그 안쪽에 타일과 같은 그리기 (ItemTilePainter.Draw의 그림 자리에 글자): 판, 띠 위 칸의 가운데에 글자, 아래쪽 띠에 횟수.
            int line = RerollWhiteLineTexels * scale;
            PixelGui.FillPlateShape(button, scale, Color.white * tint);
            var plate = new Rect(button.x + line, button.y + line, button.width - 2 * line, button.height - 2 * line);
            PixelGui.DrawWeaponPlate(skin, plate, scale, tint);
            int inset = ItemTilePainter.BadgeInsetTexels * scale;
            int areaH = (int)plate.height - 2 * inset - ItemTilePainter.BadgeHeight(skin);
            // 글자 줄 상자를 칸 가운데에 — 일반 글꼴은 글리프 상자가 줄 상자 가운데(위아래 capTop)라 획도 가운데다. 획이 칸을 넘으면 예외.
            if (skin.FontPlain.capHeight * RerollLabelScale > areaH)
                throw new System.Exception($"[PickShoot] UpgradeManager: 리롤 글자 획 {skin.FontPlain.capHeight * RerollLabelScale}px가 띠 위 칸 {areaH}px를 넘음. RerollLabelScale을 낮출 것.");
            int lineH = skin.FontPlain.lineHeight * RerollLabelScale;
            PixelGui.DrawTextCentered(skin.FontPlain, RerollLabel, (int)plate.center.x, (int)plate.y + inset + (areaH - lineH) / 2, RerollLabelScale);
            ItemTilePainter.DrawBadge(skin, plate, scale, RerollsLeft.ToString(), tint);
            GUI.color = Color.white;
            if (enabled && GUI.Button(button, GUIContent.none, GUIStyle.none))
                Reroll();
        }

        // 카드 등장의 세로 오프셋(캔버스 픽셀, 아래가 +). t는 그 카드가 출발한 뒤 지난 실시간(초), startOffset은 출발점.
        // 화면 밖에서 정지 상태로 출발해 가속하며 올라와 제자리에 최고 속도로 닿는다 — 이어지는 튐이 그 충격으로 읽힌다.
        // 닿은 뒤는 감쇠 사인 한 주기: 위로 튀었다(w=0.25) → 되돌아와(0.5) → 아래로 살짝 되밀렸다(0.75) → 제자리(1).
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
