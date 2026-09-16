using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PickShoot
{
    // 웨이브 클리어 시 3장 카드(획득/강화 혼합)를 제시하고 선택을 대기한다.
    // 게임 흐름 제어를 위해 Time.timeScale=0로 일시정지, 선택 시 원복. WaveManager가 코루틴으로 호출.
    // 카드는 IMGUI 위에 PixelUiSkin(Kenney Interface 시트)으로 그린다. 글꼴이 A-Z 0-9만 있어 UI 문구는 영문 대문자.
    public class UpgradeManager : MonoBehaviour
    {
        [SerializeField] PlayerWeapons weapons;
        [SerializeField] List<WeaponData> weaponPool = new List<WeaponData>();
        [SerializeField] int cardsToShow = 3;
        [SerializeField] AudioClip selectClip;

        [Header("Presentation")]
        [SerializeField] PixelUiSkin skin;
        // 픽셀 배율 기준. 화면 높이를 이 텍셀 수로 나눈 정수가 패널·아이콘 배율 (1080p → 4배), 글자는 그 절반.
        [SerializeField, Min(1)] int referenceHeightTexels = 270;
        [SerializeField, Range(0f, 1f)] float dimAlpha = 0.7f;

        UpgradeOption[] currentCards;
        int clearedWave;
        bool active;
        bool selected;

        public bool IsActive => active;

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
            currentCards = BuildCandidates();
            if (currentCards.Length == 0) yield break;

            active = true;
            selected = false;
            float prevScale = Time.timeScale;
            Time.timeScale = 0f;

            while (!selected) yield return null;

            Time.timeScale = prevScale;
            active = false;
        }

        UpgradeOption[] BuildCandidates()
        {
            var candidates = new List<UpgradeOption>();

            if (weapons.HasEmptySlot)
            {
                foreach (var d in weaponPool)
                    candidates.Add(UpgradeOption.Acquire(d));
            }

            for (int i = 0; i < weapons.Slots.Count; i++)
                candidates.Add(UpgradeOption.LevelUpSlot(i));

            // Fisher-Yates
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }

            int take = Mathf.Min(cardsToShow, candidates.Count);
            var result = new UpgradeOption[take];
            for (int i = 0; i < take; i++) result[i] = candidates[i];
            return result;
        }

        void Select(int index)
        {
            currentCards[index].Apply(weapons);
            if (selectClip != null) SoundPlayer.Play(selectClip);
            selected = true;
        }

        void OnGUI()
        {
            if (!active) return;

            int scale = PixelGui.ScaleForScreen(referenceHeightTexels);
            int textScale = Mathf.Max(1, Mathf.RoundToInt(scale * 0.5f));
            int tile = skin.TileSize * scale;

            GUI.color = new Color(0f, 0f, 0f, dimAlpha);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // 모든 카드를 같은 크기로: 내용상 가장 큰 카드에 맞춘다.
            var cardSize = Vector2Int.zero;
            for (int i = 0; i < currentCards.Length; i++)
            {
                var size = UpgradeCardPainter.Measure(skin, currentCards[i], weapons, scale, textScale);
                cardSize = Vector2Int.Max(cardSize, size);
            }

            string title = $"WAVE {clearedWave} CLEAR";
            const string subtitle = "CHOOSE A CARD";
            int titleW = PixelGui.MeasureText(skin.FontOutlined, title, scale);
            int bannerW = CeilToMultiple(titleW + 2 * tile, tile);
            int bannerH = 2 * tile;
            int subtitleH = skin.FontPlain.lineHeight * textScale;
            int gap = tile;

            // 배너·부제·카드 묶음을 화면 세로 중앙에 놓는다.
            int blockH = bannerH + subtitleH + gap + cardSize.y;
            int blockY = (Screen.height - blockH) / 2;
            int centerX = Screen.width / 2;

            var banner = new Rect(centerX - bannerW / 2, blockY, bannerW, bannerH);
            PixelGui.DrawNineSlice(skin.PanelRed, banner, skin.TileSize, scale);
            PixelGui.DrawTextCentered(skin.FontOutlined, title, centerX,
                blockY + (bannerH - skin.FontOutlined.lineHeight * scale) / 2, scale);
            PixelGui.DrawTextCentered(skin.FontPlain, subtitle, centerX, blockY + bannerH, textScale);

            int cardsY = blockY + bannerH + subtitleH + gap;
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
        }

        static int CeilToMultiple(int value, int multiple)
        {
            return ((value + multiple - 1) / multiple) * multiple;
        }
    }
}
