using UnityEngine;

namespace PickShoot
{
    // 무기 한 자루·유물 한 종류를 한 칸으로 보이는 정사각 타일 — 브로타토식 인벤토리 표기 (2026-10-06). 일시정지 화면의 빌드 격자와 보상 화면의 빌드 줄
    // (BuildGridPainter), 카드의 전시 칸(UpgradeCardPainter)이 같은 그리기를 쓴다.
    // 판은 HUD 무기 판과 같은 단색(외곽선 색 2텍셀 테두리 + 회색 바탕, PixelGui.DrawWeaponPlate)이고 28×28텍셀 — HUD 판의 가로 28(저격소총 22 + 테두리 2·여백 1)을
    // 세로에도 쓴다. 그림(무기 스프라이트·유물 아이콘)은 가운데.
    // 상태 배지(무기 레벨 `L5`, 유물 보유 수 `X3`)는 타일 아래쪽을 좌우 끝까지 가로지르는 외곽선 색 띠에 가운데 맞춘 글자 배율(PixelGui.TextScale) 문구 — 타일
    // 배율과 무관하게 띠 높이는 글리프 상자 + 위아래 1텍셀 여백이고, 테두리와 같은 색이라 아래 테두리가 두꺼워진 꼴로 이어진다. 그림은 띠 위에 남은 칸의 가운데 —
    // 그림 위·라벨 아래의 캡션 꼴이라 넓은 총(23텍셀)도 덮이지 않고, 문구 길이(`L5`·`X12`)와 무관하게 타일이 같은 모양이다. 모서리 안쪽 배지 → 아래 변 걸침 →
    // 하단 가운데 라벨 → 띠 순으로 목업으로 비교해 골랐다 (2026-10-06, Recordings/tile_badge_*_mockup_2026-10-06.png). 글자 배율 타일(보상 화면 줄, 56px)에서는
    // 띠가 안쪽 높이의 절반이라 키 큰 총(14텍셀)이 위 테두리에 2텍셀쯤 걸친다 — 모서리 배지일 때 총이 통째로 가려지던 것보다 낫다.
    // 유물 1개도 `X1`을 보인다 — 배지가 빠진 타일이 섞이면 줄이 고르지 않다. 빈 무기 슬롯은 어둡게 깐 판만 — 남은 슬롯 수가 보인다.
    // HUD는 이 타일을 쓰지 않는다 — HUD 배율(2)에서는 글자 배율이 타일 배율과 같아 배지가 타일 높이의 40%를 차지해 총을 덮고, 정사각 판은 한쪽 6자루 때
    // 줄 묶음을 화면 높이의 2/3까지 올린다 (목업 Recordings/hud_mockup_2026-10-06.png, 글자 배율 1은 720p에서 획이 빠져 제외).
    public static class ItemTilePainter
    {
        public const int SizeTexels = 28;
        // 타일 아래 변 → 배지 띠 (타일 배율 텍셀). 판 테두리 두께와 같아 띠가 테두리에 붙는다.
        const int BadgeInsetTexels = 2;
        // 배지 글자(글리프 상자) 위아래의 띠 여백 (글자 배율 텍셀).
        const int BadgePadTexels = 1;
        // 글자를 띠 안에서 아래로 내리는 양 (글자 배율 텍셀). 띠와 아래 테두리가 같은 색이라 한 덩이로 보여, 띠 가운데에 두면 글자가 높아 보인다 (2026-10-06).
        // 글리프 상자 아래 변이 띠 아래 변에 닿지만 같은 색이라 경계가 없고, 흰 획은 상자 안쪽 여백만큼 띠 안에 남는다.
        const int BadgeTextDropTexels = 1;
        // 빈 슬롯 판의 밝기.
        static readonly Color EmptyTint = new Color(0.5f, 0.5f, 0.5f, 1f);

        public static int Size(int scale)
        {
            return SizeTexels * scale;
        }

        // 배지 띠의 높이 (캔버스 픽셀): 대문자·숫자 글리프 상자 높이 + 위아래 여백.
        static int BadgeHeight(PixelUiSkin skin)
        {
            int ts = PixelGui.TextScale;
            return skin.FontPlain.capHeight * ts + 2 * BadgePadTexels * ts;
        }

        // (x, y)는 타일 좌상단 (캔버스 픽셀). icon이 null이면 빈 슬롯 — 어두운 판만. badge가 null이면 배지 없음.
        public static void Draw(PixelUiSkin skin, Sprite icon, string badge, int x, int y, int scale)
        {
            int size = Size(scale);
            var tile = new Rect(x, y, size, size);
            if (icon == null)
            {
                PixelGui.DrawWeaponPlate(skin, tile, scale, EmptyTint);
                return;
            }
            PixelGui.DrawWeaponPlate(skin, tile, scale);
            int iw = Mathf.RoundToInt(icon.rect.width) * scale;
            int ih = Mathf.RoundToInt(icon.rect.height) * scale;
            // 배지가 있으면 그림은 테두리 안쪽에서 띠를 뺀 칸의 세로 가운데 — 그림 사각형이 칸보다 커도 그대로 가운데 (위아래 투명 여백이 테두리에 걸친다).
            int inset = BadgeInsetTexels * scale;
            int areaH = badge != null ? size - 2 * inset - BadgeHeight(skin) : size - 2 * inset;
            PixelGui.DrawSprite(icon, new Rect(x + (size - iw) / 2, y + inset + (areaH - ih) / 2, iw, ih));
            if (badge != null) DrawBadge(skin, tile, scale, badge);
        }

        static void DrawBadge(PixelUiSkin skin, Rect tile, int scale, string text)
        {
            var font = skin.FontPlain;
            int ts = PixelGui.TextScale;
            int pad = BadgePadTexels * ts;
            int bh = BadgeHeight(skin);
            int inset = BadgeInsetTexels * scale;
            // 띠는 아래 테두리 바로 위에서 타일 좌우 끝까지 (양옆 테두리 위를 덮지만 같은 색). 글자는 띠 가로 가운데 — 타일보다 넓은 문구(글자 배율 타일에
            // X10 이상)는 양쪽으로 고르게 나온다.
            int by = (int)tile.yMax - inset - bh;
            PixelGui.FillRect(new Rect(tile.x, by, tile.width, bh), skin.WeaponPlateOutline);
            // 글자 줄의 위쪽은 띠 안쪽 여백에서 글리프 상자 위 여백(capTop)만큼 위(상자가 띠 높이에 꼭 맞는 자리)에서 BadgeTextDropTexels만큼 아래.
            PixelGui.DrawTextCentered(font, text, (int)tile.center.x, by + pad - font.capTop * ts + BadgeTextDropTexels * ts, ts);
        }
    }
}
