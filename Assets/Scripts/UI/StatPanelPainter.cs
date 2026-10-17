using UnityEngine;

namespace PickShoot
{
    // 플레이어 스탯 총계 표 (Relics §7, 2026-10-14) — 캐릭터 + 보유 유물의 수정치 합(PlayerStats.Total)을 스탯마다 한 줄. 보상 화면과 일시정지 화면의
    // 상태 열(StatusColumnPainter) 맨 위에 2열로 둔다 (일시정지 화면은 2026-10-17까지 띠와 격자 사이에 4열). 열은 위에서 아래로 먼저 채운다 — 줄 순서(PlayerStat)가
    // 성장축끼리 둘씩이라 2열은 한 열이 두 축(왼쪽 공격·생존, 오른쪽 기동·회수), 4열이면 한 열이 한 축이다 (2026-10-17).
    // 줄은 라벨이 열 왼쪽, 값이 열 오른쪽 끝 — 카드의 표와 같은 꼴. 값은 총계 표기(PlayerStatModifiers.Format — 부호는 음수에만)이고 색은:
    //   흰색 — 0이 아닌 총계. 초록은 쓰지 않는다 — 초록은 화면 어디서나 "고르면 얻는 값"이다 (카드의 얻는 값과 아래 미리보기).
    //   빨강(PixelUiSkin.PenaltyText) — 음수 총계 (패널티가 있는 유물).
    //   흐림(MutedText) — 0. 라벨까지 흐리게 해 아직 오르지 않은 스탯이 덜 눈에 띈다. 줄은 0이어도 늘 그려 표가 움직이지 않는다.
    //   초록(UpgradeText) — 미리보기: 보상 화면에서 유물 카드에 마우스를 올리면 그 카드의 수정치를 더한 새 총계를 바뀌는 줄에만 보인다.
    // 처음엔 양수 총계를 초록으로 칠했다가 미리보기의 초록이 묻혀 흰색으로 바꿨다 (Recordings/reward_stats_mockup_2026-10-14_*.png).
    // 줄 맨 앞에 스탯 아이콘(PixelUiSkin.StatIcon, 16텍셀 타일을 글자 배율로 — 글리프 타일과 같은 꼴)을 두고 라벨은 그 바로 뒤에 붙인다 (2026-10-17) — 타일 안의
    // 빈 가장자리가 간격이다. 아이콘은 라벨과 같은 색을 곱해 0인 줄에선 함께 흐려진다.
    public static class StatPanelPainter
    {
        // 줄 수 — 스탯 수를 열 수로 나눠 올림.
        public static int Rows(int columns)
        {
            return (PlayerStatModifiers.Stats.Length + columns - 1) / columns;
        }

        public static int Height(PixelUiSkin skin, int columns)
        {
            return Rows(columns) * skin.FontPlain.lineHeight * PixelGui.TextScale;
        }

        // (x, y)는 표의 좌상단, width는 표 전체 폭(열 사이 gutter 포함). preview는 고르면 더해질 수정치 — 없으면 null.
        // 한 줄의 라벨과 가장 긴 값이 열 폭에 들지 않으면 예외.
        public static void Draw(PixelUiSkin skin, PlayerStatModifiers total, PlayerStatModifiers? preview, int x, int y, int width, int columns, int gutter)
        {
            int ts = PixelGui.TextScale;
            int lineH = skin.FontPlain.lineHeight * ts;
            int space = skin.FontPlain.spaceWidth * ts;
            int iconSize = skin.TileSize * ts;
            int colW = (width - (columns - 1) * gutter) / columns;
            int rows = Rows(columns);
            var stats = PlayerStatModifiers.Stats;
            for (int i = 0; i < stats.Length; i++)
            {
                var s = stats[i];
                int now = total.Get(s);
                bool changed = preview.HasValue && preview.Value.Get(s) != 0;
                int shown = changed ? now + preview.Value.Get(s) : now;
                string label = PlayerStatModifiers.Label(s);
                string value = PlayerStatModifiers.Format(s, shown);
                int cx = x + (i / rows) * (colW + gutter);
                int cy = y + (i % rows) * lineH;
                int need = iconSize + PixelGui.MeasureText(skin.FontPlain, label, ts) + space + PixelGui.MeasureText(skin.FontPlain, value, ts);
                if (need > colW)
                    throw new System.Exception($"[PickShoot] StatPanelPainter: '{label} {value}'의 폭 {need}px가 열 폭 {colW}px를 넘음. 열 수를 줄이거나 표를 넓힐 것.");

                Color valueTint;
                if (changed) valueTint = skin.UpgradeText;
                else if (shown < 0) valueTint = skin.PenaltyText;
                else if (shown == 0) valueTint = skin.MutedText;
                else valueTint = Color.white;
                var labelTint = !changed && shown == 0 ? skin.MutedText : Color.white;

                // 글리프의 흰 획에 색을 곱한다 (GUI.color) — 카드 제목과 같은 방식.
                var prev = GUI.color;
                GUI.color = prev * labelTint;
                PixelGui.DrawSprite(skin.StatIcon(s), new Rect(cx, cy, iconSize, iconSize));
                PixelGui.DrawText(skin.FontPlain, label, cx + iconSize, cy, ts);
                GUI.color = prev * valueTint;
                PixelGui.DrawTextRight(skin.FontPlain, value, cx + colW, cy, ts);
                GUI.color = prev;
            }
        }
    }
}
