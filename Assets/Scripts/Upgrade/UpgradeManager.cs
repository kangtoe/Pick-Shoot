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
    // 카드 밑의 리롤 버튼은 레벨 업(PlayerExperience)으로 적립한 리롤이 남아 있을 때 3장을 다시 뽑는다 — 보이는 카드 중 한 장은 반드시 바뀐다 (Weapons §3).
    // 버튼에는 남은 적립 횟수를 적는다. 쓰지 않은 횟수는 다음 카드 화면으로 이어진다.
    // 카드는 (다시) 뽑힐 때마다 화면 아래 밖에서 차례로 튀어 올라 스프링처럼 흔들리며 멎는다 — 멎기 전에는 카드도 버튼도 입력을 받지 않는다.
    // 카드 밑(리롤 버튼 아래)에 이 화면 종류의 빌드 한 줄(BuildGridPainter — 무기 화면은 슬롯 12칸, 유물 화면은 보유 유물)을 글자 배율로 둔다. 타일과 카드 전시 칸에
    // 마우스를 올리면 툴팁(TooltipPainter)이 뜬다 — 카드·버튼을 다 그린 뒤 맨 마지막에 (2026-10-06).
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
        const string RerollLabel = "REROLL";
        const int ButtonPaddingTexels = 8;   // 버튼 패널 테두리 → 글자
        const int SubtitleGapTexels = 4;     // 배너 아래 안내 문구("CHOOSE A CARD")까지 간격 (패널 텍셀)
        // 부제 → 카드, 카드 → 리롤 버튼, 버튼(없으면 카드) → 빌드 줄 (패널 텍셀). 블록이 세로 1080 중 1016px를 쓴다 — 그전엔 앞 둘이 16(한 타일)이었는데
        // 빌드 줄(56px)을 넣으며 8로 줄였다.
        const int CardGapTexels = 8;
        const int ButtonGapTexels = 8;
        const int StripGapTexels = 4;
        const int HoverLiftTexels = 4;       // 카드(UpgradeCardPainter)와 같은 텍셀 수. 버튼은 글자 배율이라 화면에서는 절반.
        static readonly Color DisabledTint = new Color(0.5f, 0.5f, 0.5f, 1f);

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
                if (string.IsNullOrEmpty(relicPool[i].Description))
                    throw new System.Exception($"[PickShoot] UpgradeManager.relicPool[{i}] '{relicPool[i].DisplayName}'에 description이 비어 있음. 타일 툴팁의 설명 줄에 필요 (Relics §2).");
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

            string title = $"WAVE {clearedWave} CLEAR";
            // 부제가 이 화면의 종류를 알린다 — 유물 화면은 RELIC, 무기 화면은 획득·레벨업이 섞이므로 CARD.
            string subtitle = rewardKind == RewardKind.Relic ? "CHOOSE A RELIC" : "CHOOSE A CARD";
            int titleW = PixelGui.MeasureText(skin.FontOutlined, title, scale);
            int bannerW = CeilToMultiple(titleW + 2 * tile, tile);
            int bannerH = 2 * tile;
            int lineH = skin.FontPlain.lineHeight * textScale;
            int gap = tile;   // 카드 사이
            int cardGap = CardGapTexels * scale;
            int buttonGap = ButtonGapTexels * scale;
            int stripGap = StripGapTexels * scale;

            // 리롤 버튼은 일시정지 화면의 안내 패널과 같은 결: 글자 배율 회색 패널에 한 줄. 남은 적립 횟수를 괄호에 적는다.
            bool rerollOffered = RerollOffered;
            string rerollText = $"{RerollLabel} ({RerollsLeft})";
            int textTile = skin.TileSize * textScale;
            int buttonPad = ButtonPaddingTexels * textScale;
            int buttonW = CeilToMultiple(PixelGui.MeasureText(skin.FontPlain, rerollText, textScale) + 2 * buttonPad, textTile);
            int buttonH = CeilToMultiple(lineH + 2 * buttonPad, textTile);

            // 빌드 줄은 글자 배율 타일 한 줄 (BuildGridPainter).
            int stripScale = textScale;
            BuildGridPainter.RequireFits(stripScale, canvas.Width);
            int stripH = BuildGridPainter.Height(skin, stripScale, 1);

            // 배너·부제·카드·리롤 버튼·빌드 줄 묶음을 화면 세로 중앙에 놓는다.
            int subtitleGap = SubtitleGapTexels * scale;
            int blockH = bannerH + subtitleGap + lineH + cardGap + cardSize.y;
            if (rerollOffered) blockH += buttonGap + buttonH;
            blockH += stripGap + stripH;
            if (blockH > canvas.Height)
                throw new System.Exception($"[PickShoot] UpgradeManager: 보상 화면 블록 높이 {blockH}px가 캔버스 {canvas.Height}px를 넘음. 간격을 줄일 것.");
            int blockY = (canvas.Height - blockH) / 2;
            int centerX = canvas.Width / 2;

            var banner = new Rect(centerX - bannerW / 2, blockY, bannerW, bannerH);
            PixelGui.DrawNineSlice(skin.PanelRed, banner, skin.TileSize, scale);
            PixelGui.DrawTextCentered(skin.FontOutlined, title, centerX,
                blockY + (bannerH - skin.FontOutlined.lineHeight * scale) / 2, scale);
            PixelGui.DrawTextCentered(skin.FontPlain, subtitle, centerX, blockY + bannerH + subtitleGap, textScale);

            int cardsY = blockY + bannerH + subtitleGap + lineH + cardGap;
            int totalW = currentCards.Length * cardSize.x + (currentCards.Length - 1) * gap;
            int startX = centerX - totalW / 2;
            var mouse = Event.current.mousePosition;
            // 마지막 카드까지 멎기 전에는 카드도 버튼도 반응하지 않는다 — 날아가는 카드는 자리가 정해지지 않았고, 버튼 위를 지나간다.
            float sinceShown = Time.unscaledTime - cardsShownAt;
            bool interactable = sinceShown >= EnterTotalSeconds(currentCards.Length);

            // 리롤 버튼과 빌드 줄은 카드보다 먼저 그린다 — 화면 아래에서 올라오는 카드가 그 위를 지나가므로 카드가 앞에 보여야 한다.
            int belowCards = cardsY + cardSize.y;
            if (rerollOffered)
            {
                DrawRerollButton(new Rect(centerX - buttonW / 2, belowCards + buttonGap, buttonW, buttonH),
                    rerollText, lineH, textScale, mouse, interactable);
                belowCards += buttonGap + buttonH;
            }

            // 툴팁은 하나만, 다 그린 뒤 맨 마지막에. 멎기 전에는 카드처럼 호버에 반응하지 않는다.
            string tipTitle = null;
            Rect tipAnchor = default;
            tipFacts.Clear();
            tipNotes.Clear();
            int stripX = centerX - BuildGridPainter.Width(stripScale) / 2;
            int stripY = belowCards + stripGap;
            if (rewardKind == RewardKind.Weapon)
            {
                int slot = BuildGridPainter.DrawWeaponRow(skin, weapons, stripX, stripY, stripScale, mouse, out var tileRect);
                if (interactable && slot >= 0)
                {
                    var w = weapons.Slots[slot];
                    tipTitle = ItemInfoText.WeaponTitle(w.Data);
                    tipAnchor = tileRect;
                    ItemInfoText.WeaponFacts(w.Data, w.Level, slot, stats, tipFacts);
                    ItemInfoText.WeaponNotes(w.Data, tipNotes);
                }
            }
            else
            {
                var relic = BuildGridPainter.DrawRelicGrid(skin, stats, stripX, stripY, stripScale, 1, mouse, out var tileRect);
                if (interactable && relic != null)
                {
                    tipTitle = ItemInfoText.RelicTitle(relic);
                    tipAnchor = tileRect;
                    ItemInfoText.RelicFacts(relic, stats.CountOf(relic), tipFacts);
                    ItemInfoText.RelicNotes(relic, tipNotes);
                }
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

        // 카드가 가리키는 것의 툴팁 — 고른 뒤의 상태. 획득 카드는 슬롯이 아직 없어 NEW·LEVEL 1, 레벨업은 그 슬롯의 다음 레벨, 유물은 보유 수 + 1.
        string CardTooltip(UpgradeOption option, List<string> facts, List<string> notes)
        {
            facts.Clear();
            notes.Clear();
            switch (option.Kind)
            {
                case UpgradeKind.AcquireWeapon:
                    ItemInfoText.WeaponFacts(option.AcquireData, 1, -1, stats, facts);
                    ItemInfoText.WeaponNotes(option.AcquireData, notes);
                    return ItemInfoText.WeaponTitle(option.AcquireData);
                case UpgradeKind.LevelUp:
                {
                    var w = weapons.Slots[option.TargetSlotIndex];
                    ItemInfoText.WeaponFacts(w.Data, w.Level + 1, option.TargetSlotIndex, stats, facts);
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

        void DrawRerollButton(Rect button, string text, int lineH, int textScale, Vector2 mouse, bool interactable)
        {
            // 할 수 있는 리롤이 없으면(0) 어둡게 남겨 둔다 — 카드 묶음이 움직이지 않고, 리롤이 있다는 것도 읽힌다.
            bool affordable = RerollAffordable;
            bool enabled = interactable && affordable;
            bool hovered = enabled && button.Contains(mouse);
            var drawn = button;
            if (hovered) drawn.y -= HoverLiftTexels * textScale;
            GUI.color = affordable ? Color.white : DisabledTint;
            PixelGui.DrawNineSlice(skin.PanelGrey, drawn, skin.TileSize, textScale);
            PixelGui.DrawTextCentered(skin.FontPlain, text, (int)button.center.x, (int)drawn.y + ((int)button.height - lineH) / 2, textScale);
            GUI.color = Color.white;
            if (hovered)
                PixelGui.DrawNineSlice(skin.SelectionFrame, drawn, skin.TileSize, textScale);
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

        static int CeilToMultiple(int value, int multiple)
        {
            return ((value + multiple - 1) / multiple) * multiple;
        }
    }
}
