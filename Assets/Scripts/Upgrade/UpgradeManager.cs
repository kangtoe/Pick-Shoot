using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PickShoot
{
    // 웨이브 클리어 시 3장 카드(획득/강화 혼합)를 제시하고 선택을 대기한다.
    // 게임 흐름 제어를 위해 Time.timeScale=0로 일시정지, 선택 시 원복. WaveManager가 코루틴으로 호출.
    public class UpgradeManager : MonoBehaviour
    {
        [SerializeField] PlayerWeapons weapons;
        [SerializeField] List<WeaponData> weaponPool = new List<WeaponData>();
        [SerializeField] int cardsToShow = 3;
        [SerializeField] AudioClip selectClip;

        UpgradeOption[] currentCards;
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
                if (weaponPool[i] == null)
                    throw new System.Exception($"[PickShoot] UpgradeManager.weaponPool[{i}]가 null.");
        }

        public IEnumerator PresentAndWait()
        {
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

        void OnGUI()
        {
            if (!active) return;

            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            var headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 28,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white },
            };
            GUI.Label(new Rect(0, Screen.height * 0.15f, Screen.width, 40f),
                "웨이브 클리어 — 카드를 선택하세요", headerStyle);

            const float cardW = 260f;
            const float cardH = 160f;
            const float gap = 30f;
            float totalW = currentCards.Length * cardW + (currentCards.Length - 1) * gap;
            float startX = (Screen.width - totalW) * 0.5f;
            float cardY = (Screen.height - cardH) * 0.5f;

            var buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 18,
                wordWrap = true,
            };

            for (int i = 0; i < currentCards.Length; i++)
            {
                var r = new Rect(startX + i * (cardW + gap), cardY, cardW, cardH);
                if (GUI.Button(r, currentCards[i].BuildLabel(weapons), buttonStyle))
                {
                    currentCards[i].Apply(weapons);
                    if (selectClip != null) SoundPlayer.Play(selectClip);
                    selected = true;
                }
            }
        }
    }
}
