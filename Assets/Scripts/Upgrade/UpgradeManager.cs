using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PickShoot
{
    // 웨이브 클리어 시 3장 카드(획득/강화 혼합)를 제시하고 선택을 대기한다.
    // 게임 흐름 제어를 위해 Time.timeScale=0로 일시정지, 선택 시 원복. WaveManager가 코루틴으로 호출.
    // 카드는 IMGUI 위에 PixelUiSkin(Kenney Interface 시트)으로 그린다. 글꼴이 A-Z 0-9만 있어 UI 문구는 영문 대문자.
    // 카드 밑의 리롤 버튼은 카드 화면마다 정해진 횟수만큼 3장을 다시 뽑는다 — 보이는 카드 중 한 장은 반드시 바뀐다 (Weapons §3).
    public class UpgradeManager : MonoBehaviour
    {
        [SerializeField] PlayerWeapons weapons;
        [SerializeField] List<WeaponData> weaponPool = new List<WeaponData>();
        [SerializeField] int cardsToShow = 3;
        // 카드 화면 한 번에 쓸 수 있는 리롤 횟수 (무료). 0이면 버튼을 두지 않는다.
        [SerializeField, Min(0)] int rerollsPerReward = 1;
        [SerializeField] AudioClip selectClip;
        [SerializeField] AudioClip rerollClip;

        [Header("Presentation")]
        [SerializeField] PixelUiSkin skin;
        [SerializeField, Range(0f, 1f)] float dimAlpha = 0.7f;

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
        int rerollsLeft;
        bool active;
        bool selected;

        public bool IsActive => active;

        // 후보가 카드 수 이하면 다시 뽑아도 같은 카드가 순서만 바뀌어 나오므로 리롤 버튼을 두지 않는다. 후보가 카드 수보다 많으면
        // 한 장을 빼도(Reroll) 카드를 채울 수 있다.
        bool RerollOffered => rerollsPerReward > 0 && candidates.Count > currentCards.Length;

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
            rerollsLeft = rerollsPerReward;

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
            if (rerollsLeft <= 0)
                throw new System.Exception("[PickShoot] UpgradeManager.Reroll: 남은 리롤이 없는데 호출됨.");
            rerollsLeft--;
            var pool = new List<UpgradeOption>(candidates);
            var excluded = currentCards[Random.Range(0, currentCards.Length)];
            if (!pool.Remove(excluded))
                throw new System.Exception("[PickShoot] UpgradeManager.Reroll: 보이는 카드가 후보에 없음. currentCards는 candidates에서 뽑혀야 함.");
            currentCards = Draw(pool);
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
            const string subtitle = "CHOOSE A CARD";
            int titleW = PixelGui.MeasureText(skin.FontOutlined, title, scale);
            int bannerW = CeilToMultiple(titleW + 2 * tile, tile);
            int bannerH = 2 * tile;
            int lineH = skin.FontPlain.lineHeight * textScale;
            int gap = tile;

            // 리롤 버튼은 일시정지 화면의 안내 패널과 같은 결: 글자 배율 회색 패널에 한 줄. 남은 횟수를 괄호에 넣어 함께 적는다.
            bool rerollOffered = RerollOffered;
            string rerollText = $"{RerollLabel} ({rerollsLeft})";
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

            for (int i = 0; i < currentCards.Length; i++)
            {
                var r = new Rect(startX + i * (cardSize.x + gap), cardsY, cardSize.x, cardSize.y);
                bool hovered = r.Contains(mouse);
                UpgradeCardPainter.Draw(skin, currentCards[i], weapons, r, scale, textScale, hovered);
                // 보이지 않는 버튼으로 클릭만 받는다. 카드 그림은 위에서 직접 그렸다.
                if (GUI.Button(r, GUIContent.none, GUIStyle.none))
                    Select(i);
            }

            if (!rerollOffered) return;

            // 다 쓴 뒤에도 어둡게 남겨 둔다 — 카드 묶음이 움직이지 않고, 리롤이 있었다는 것도 읽힌다.
            bool rerollEnabled = rerollsLeft > 0;
            var button = new Rect(centerX - buttonW / 2, cardsY + cardSize.y + gap, buttonW, buttonH);
            bool buttonHovered = rerollEnabled && button.Contains(mouse);
            var drawn = button;
            if (buttonHovered) drawn.y -= HoverLiftTexels * textScale;
            GUI.color = rerollEnabled ? Color.white : DisabledTint;
            PixelGui.DrawNineSlice(skin.PanelGrey, drawn, skin.TileSize, textScale);
            PixelGui.DrawTextCentered(skin.FontPlain, rerollText, centerX, (int)drawn.y + (buttonH - lineH) / 2, textScale);
            GUI.color = Color.white;
            if (buttonHovered)
                PixelGui.DrawNineSlice(skin.SelectionFrame, drawn, skin.TileSize, textScale);
            if (rerollEnabled && GUI.Button(button, GUIContent.none, GUIStyle.none))
                Reroll();
        }

        static int CeilToMultiple(int value, int multiple)
        {
            return ((value + multiple - 1) / multiple) * multiple;
        }
    }
}
