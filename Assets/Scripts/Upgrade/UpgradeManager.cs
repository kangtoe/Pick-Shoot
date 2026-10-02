using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PickShoot
{
    // 웨이브 클리어 시 3장 카드(획득/강화 혼합)를 제시하고 선택을 대기한다.
    // 게임 흐름 제어를 위해 Time.timeScale=0로 일시정지, 선택 시 원복. WaveManager가 코루틴으로 호출.
    // 카드는 IMGUI 위에 PixelUiSkin(Kenney Interface 시트)으로 그린다. 글꼴이 A-Z 0-9만 있어 UI 문구는 영문 대문자.
    // 카드 밑의 리롤 버튼은 러닝 누적 스코어(RunScore)가 문턱에 닿았을 때 3장을 다시 뽑는다 — 보이는 카드 중 한 장은 반드시 바뀐다 (Weapons §3).
    // 버튼에는 지금 스코어로 이어서 할 수 있는 횟수만 적고, 스코어는 HUD가 숨는 동안에도 HUD와 같은 자리에 보인다.
    // 카드는 (다시) 뽑힐 때마다 화면 아래 밖에서 차례로 튀어 올라 스프링처럼 흔들리며 멎는다 — 멎기 전에는 카드도 버튼도 입력을 받지 않는다.
    public class UpgradeManager : MonoBehaviour
    {
        [SerializeField] PlayerWeapons weapons;
        [SerializeField] List<WeaponData> weaponPool = new List<WeaponData>();
        [SerializeField] int cardsToShow = 3;
        [SerializeField] RunScore score;
        // 리롤 문턱 (Weapons §3): 러닝에서 k번째 리롤은 누적 스코어가 문턱 S_k 이상일 때만 할 수 있다. 횟수 제한은 따로 없다. 스코어는 줄지 않는다 —
        // 벌어들인 총점이 지금까지의 리롤 비용 합을 넘어야 한다는 뜻이라 차감 모델과 조건은 같지만, 우상단 숫자가 총점이라는 뜻을 잃지 않는다.
        // 앞 문턱은 여기 적고(둘 이상, 내려가지 않게), 목록이 끝나면 마지막 둘의 합(피보나치식)으로 이어 간다 — 처치 수는 웨이브마다 선형으로 늘지만
        // 문턱은 지수로 늘어 리롤이 점점 귀해진다. 0이면 공짜 리롤.
        [SerializeField] int[] rerollScoreThresholds = { 100, 200 };
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
        const int HoverLiftTexels = 4;       // 카드(UpgradeCardPainter)와 같은 텍셀 수. 버튼은 글자 배율이라 화면에서는 절반.
        static readonly Color DisabledTint = new Color(0.5f, 0.5f, 0.5f, 1f);

        // 이번 카드 화면의 후보 전체(획득 + 레벨업, 순서 고정). 처음 카드도 리롤도 여기서 뽑는다.
        List<UpgradeOption> candidates;
        UpgradeOption[] currentCards;
        int clearedWave;
        // 이 러닝에서 쓴 리롤 수 — 다음 문턱의 색인. 카드 화면마다 초기화하지 않는다.
        int rerollsUsed;
        // 지금 카드들이 뽑힌 실시간 — 등장 연출의 기준.
        float cardsShownAt;
        bool active;
        bool selected;

        public bool IsActive => active;

        // 후보가 카드 수 이하면 다시 뽑아도 같은 카드가 순서만 바뀌어 나오므로 리롤 버튼을 두지 않는다. 후보가 카드 수보다 많으면
        // 한 장을 빼도(Reroll) 카드를 채울 수 있다.
        bool RerollOffered => candidates.Count > currentCards.Length;
        int NextRerollThreshold => RerollThreshold(rerollsUsed);
        bool RerollAffordable => score.Total >= NextRerollThreshold;
        // 지금 스코어로 이어서 할 수 있는 리롤 수 — 다음 문턱부터 차례로 세어 스코어를 넘는 첫 문턱에서 멈춘다. 버튼에 적는 숫자.
        // 문턱이 자라므로(Awake에서 보장) 끝난다.
        int AffordableRerolls
        {
            get
            {
                int count = 0;
                while (score.Total >= RerollThreshold(rerollsUsed + count)) count++;
                return count;
            }
        }

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
            if (skin == null)
                throw new System.Exception("[PickShoot] UpgradeManager.skin 미할당. PixelUiSkin 자산(Assets/Data/UI) 필요.");
            skin.Validate();
            if (score == null)
                throw new System.Exception("[PickShoot] UpgradeManager.score 미할당. RunScore(GameManager) 참조 필요 (리롤 문턱 판정).");
            if (rerollScoreThresholds == null || rerollScoreThresholds.Length < 2)
                throw new System.Exception("[PickShoot] UpgradeManager.rerollScoreThresholds는 둘 이상이어야 함 — 그 뒤를 마지막 둘의 합으로 이어 간다.");
            for (int i = 0; i < rerollScoreThresholds.Length; i++)
            {
                if (rerollScoreThresholds[i] < 0)
                    throw new System.Exception($"[PickShoot] UpgradeManager.rerollScoreThresholds[{i}]가 음수 ({rerollScoreThresholds[i]}).");
                if (i > 0 && rerollScoreThresholds[i] < rerollScoreThresholds[i - 1])
                    throw new System.Exception($"[PickShoot] UpgradeManager.rerollScoreThresholds[{i}]({rerollScoreThresholds[i]})가 앞 값({rerollScoreThresholds[i - 1]})보다 작음 — 문턱은 내려가지 않아야 함.");
            }
            if (rerollScoreThresholds[rerollScoreThresholds.Length - 1] <= 0)
                throw new System.Exception("[PickShoot] UpgradeManager.rerollScoreThresholds의 마지막 값은 0보다 커야 함 — 전부 0이면 문턱이 자라지 않아 리롤이 무한.");
        }

        // clearedWave는 헤더("WAVE N CLEAR") 표기용.
        public IEnumerator PresentAndWait(int clearedWave)
        {
            if (clearedWave < 1)
                throw new System.Exception($"[PickShoot] UpgradeManager.PresentAndWait: clearedWave가 1 미만 ({clearedWave}).");
            this.clearedWave = clearedWave;
            candidates = BuildCandidates();
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

        List<UpgradeOption> BuildCandidates()
        {
            var result = new List<UpgradeOption>();

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
            currentCards[index].Apply(weapons);
            if (selectClip != null) SoundPlayer.Play(selectClip);
            selected = true;
        }

        // 지금 보이는 카드 중 무작위 한 장을 뺀 후보에서 다시 뽑는다 — 한 장은 반드시 바뀌고, 나머지는 독립 추첨이라 되돌아올 수
        // 있다. 보이는 카드를 전부 빼면 후보가 6장 이하인 초반에는 결과가 정해져 뽑기가 아니게 되기 때문 (Weapons §3). 후보에 같은
        // 카드는 둘 없으므로 동등성으로 빼면 정확하다.
        void Reroll()
        {
            if (!RerollAffordable)
                throw new System.Exception($"[PickShoot] UpgradeManager.Reroll: 스코어({score.Total})가 문턱({NextRerollThreshold})에 못 미치는데 호출됨.");
            rerollsUsed++;
            var pool = new List<UpgradeOption>(candidates);
            var excluded = currentCards[Random.Range(0, currentCards.Length)];
            if (!pool.Remove(excluded))
                throw new System.Exception("[PickShoot] UpgradeManager.Reroll: 보이는 카드가 후보에 없음. currentCards는 candidates에서 뽑혀야 함.");
            currentCards = Draw(pool);
            cardsShownAt = Time.unscaledTime;
            if (rerollClip != null) SoundPlayer.Play(rerollClip);
        }

        // 러닝에서 index번째(0부터) 리롤의 문턱. 목록 안은 그대로, 밖은 마지막 둘의 합으로 이어 간다. int를 넘으면 오류 — 그만큼 리롤할 일은 없다.
        int RerollThreshold(int index)
        {
            if (index < rerollScoreThresholds.Length) return rerollScoreThresholds[index];
            long a = rerollScoreThresholds[rerollScoreThresholds.Length - 2];
            long b = rerollScoreThresholds[rerollScoreThresholds.Length - 1];
            for (int i = rerollScoreThresholds.Length; i <= index; i++)
            {
                long next = a + b;
                if (next > int.MaxValue)
                    throw new System.Exception($"[PickShoot] UpgradeManager: {index}번째 리롤 문턱이 int 범위를 넘음.");
                a = b;
                b = next;
            }
            return (int)b;
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

            // 스코어는 HUD가 숨는 동안에도 같은 자리에 같은 모양으로 — 리롤 문턱과 견줄 수 있어야 한다.
            GameHud.DrawScoreLine(skin, canvas, score.Total);

            // 카드 크기는 고정 (UpgradeCardPainter). 어떤 조합이 나와도 세 장이 같은 크기·자리에 놓인다.
            var cardSize = UpgradeCardPainter.CardSize(skin, scale);

            string title = $"WAVE {clearedWave} CLEAR";
            const string subtitle = "CHOOSE A CARD";
            int titleW = PixelGui.MeasureText(skin.FontOutlined, title, scale);
            int bannerW = CeilToMultiple(titleW + 2 * tile, tile);
            int bannerH = 2 * tile;
            int lineH = skin.FontPlain.lineHeight * textScale;
            int gap = tile;

            // 리롤 버튼은 일시정지 화면의 안내 패널과 같은 결: 글자 배율 회색 패널에 한 줄. 지금 스코어로 이어서 할 수 있는 횟수를 괄호에 적는다 —
            // 문턱 값은 보이지 않고 횟수만 보인다 (문턱 전의 남은 횟수 표기와 같은 꼴).
            bool rerollOffered = RerollOffered;
            string rerollText = $"{RerollLabel} ({AffordableRerolls})";
            int textTile = skin.TileSize * textScale;
            int buttonPad = ButtonPaddingTexels * textScale;
            int buttonW = CeilToMultiple(PixelGui.MeasureText(skin.FontPlain, rerollText, textScale) + 2 * buttonPad, textTile);
            int buttonH = CeilToMultiple(lineH + 2 * buttonPad, textTile);

            // 배너·부제·카드·리롤 버튼 묶음을 화면 세로 중앙에 놓는다.
            int subtitleGap = SubtitleGapTexels * scale;
            int blockH = bannerH + subtitleGap + lineH + gap + cardSize.y;
            if (rerollOffered) blockH += gap + buttonH;
            int blockY = (canvas.Height - blockH) / 2;
            int centerX = canvas.Width / 2;

            var banner = new Rect(centerX - bannerW / 2, blockY, bannerW, bannerH);
            PixelGui.DrawNineSlice(skin.PanelRed, banner, skin.TileSize, scale);
            PixelGui.DrawTextCentered(skin.FontOutlined, title, centerX,
                blockY + (bannerH - skin.FontOutlined.lineHeight * scale) / 2, scale);
            PixelGui.DrawTextCentered(skin.FontPlain, subtitle, centerX, blockY + bannerH + subtitleGap, textScale);

            int cardsY = blockY + bannerH + subtitleGap + lineH + gap;
            int totalW = currentCards.Length * cardSize.x + (currentCards.Length - 1) * gap;
            int startX = centerX - totalW / 2;
            var mouse = Event.current.mousePosition;
            // 마지막 카드까지 멎기 전에는 카드도 버튼도 반응하지 않는다 — 날아가는 카드는 자리가 정해지지 않았고, 버튼 위를 지나간다.
            float sinceShown = Time.unscaledTime - cardsShownAt;
            bool interactable = sinceShown >= EnterTotalSeconds(currentCards.Length);

            // 리롤 버튼은 카드보다 먼저 그린다 — 화면 아래에서 올라오는 카드가 버튼 위를 지나가므로 카드가 앞에 보여야 한다.
            if (rerollOffered)
                DrawRerollButton(new Rect(centerX - buttonW / 2, cardsY + cardSize.y + gap, buttonW, buttonH),
                    rerollText, lineH, textScale, mouse, interactable);

            for (int i = 0; i < currentCards.Length; i++)
            {
                float t = sinceShown - i * enterStaggerSeconds;
                if (t < 0f) continue;   // 아직 출발 전
                var r = new Rect(startX + i * (cardSize.x + gap), cardsY, cardSize.x, cardSize.y);
                // 출발점은 카드 위쪽 끝이 화면 아래 끝에 닿는 곳 — 화면 밖에서 들어온다.
                r.y += EnterOffset(t, canvas.Height - cardsY, scale);
                bool hovered = interactable && r.Contains(mouse);
                UpgradeCardPainter.Draw(skin, currentCards[i], weapons, r, scale, textScale, hovered);
                // 보이지 않는 버튼으로 클릭만 받는다. 카드 그림은 위에서 직접 그렸다.
                if (interactable && GUI.Button(r, GUIContent.none, GUIStyle.none))
                    Select(i);
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
