using System.Collections.Generic;
using UnityEngine;

namespace PickShoot
{
    // 적 등급의 몸 색 사본 짝 (Enemies §7, PixelArtUI §7) — 적이 쓰는 스프라이트마다 원본·2단계·3단계를 한 줄로 둔다.
    // 사본은 테두리(잉크색·흰 테두리)는 그대로 두고 몸 색만 밝기를 유지한 채 단계 색으로 바꾼 그림이며, 시트째 구워 Assets/Art/Sprites/EnemyTiers에 둔다.
    // WaveManager가 등급(EnemyRank)에 실어 보내고 EnemyHealth.ApplyRank가 스폰 직후 렌더러·걷기 프레임·그림을 쥔 컴포넌트(IEnemyTierSprites)의 그림을 바꿔 끼운다.
    [CreateAssetMenu(menuName = "PickShoot/Enemy Tier Art", fileName = "EnemyTierArt")]
    public class EnemyTierArt : ScriptableObject
    {
        [System.Serializable]
        public class Entry
        {
            public Sprite original;
            public Sprite tier2;
            public Sprite tier3;
        }

        [SerializeField] Entry[] entries;

        Dictionary<Sprite, Entry> lookup;

        void OnEnable() => lookup = null;

        // sprite는 원본이든 이미 바꾼 사본이든 된다 — 서펜트 몸통은 이미 입은 머리를 복제한 뒤 같은 단계를 다시 입으므로, 두 번 입혀도 그대로여야 한다.
        public Sprite For(Sprite sprite, int tier)
        {
            if (sprite == null)
                throw new System.Exception($"[PickShoot] EnemyTierArt.For: 스프라이트가 null ({name}).");
            if (lookup == null) BuildLookup();
            if (!lookup.TryGetValue(sprite, out var entry))
                throw new System.Exception($"[PickShoot] EnemyTierArt.For: {sprite.name}의 단계 사본이 없음 ({name}). 사본을 다시 굽고 짝을 더한다 (PixelArtUI §7).");
            switch (tier)
            {
                case 1: return entry.original;
                case 2: return entry.tier2;
                case 3: return entry.tier3;
                default: throw new System.Exception($"[PickShoot] EnemyTierArt.For: 단계 {tier} — 1~3만 있다.");
            }
        }

        public Sprite[] For(Sprite[] sprites, int tier)
        {
            var result = new Sprite[sprites.Length];
            for (int i = 0; i < sprites.Length; i++) result[i] = For(sprites[i], tier);
            return result;
        }

        void BuildLookup()
        {
            if (entries == null || entries.Length == 0)
                throw new System.Exception($"[PickShoot] EnemyTierArt.entries가 비어 있음 ({name}).");
            lookup = new Dictionary<Sprite, Entry>();
            for (int i = 0; i < entries.Length; i++)
            {
                var e = entries[i];
                if (e == null || e.original == null || e.tier2 == null || e.tier3 == null)
                    throw new System.Exception($"[PickShoot] EnemyTierArt.entries[{i}]에 빈 칸이 있음 ({name}).");
                lookup.Add(e.original, e);
                lookup.Add(e.tier2, e);
                lookup.Add(e.tier3, e);
            }
        }
    }

    // 그림을 필드로 쥐고 있다가 상태에 따라 렌더러에 끼우는 적 컴포넌트 (돌진형의 준비 포즈, 서펜트의 몸통).
    // 등급을 입을 때 EnemyHealth가 불러 쥔 그림을 그 단계의 사본으로 바꾼다 (Enemies §7).
    public interface IEnemyTierSprites
    {
        void UseTierSprites(EnemyTierArt art, int tier);
    }
}
