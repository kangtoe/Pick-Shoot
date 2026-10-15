using UnityEngine;

namespace PickShoot
{
    // 글자 한 줄을 얹은 누르는 판 — 시작 화면의 `PLAY`(오른쪽 아래)와 일시정지 화면의 `MAIN MENU`(아래 가운데, 2026-10-15)가 같이 쓴다.
    // 시작 화면 타일과 같은 꼴: 바깥 흰 선 1텍셀(PixelGui.FillPlateShape) 안에 판(PixelGui.DrawWeaponPlate), 그 가운데 일반 글꼴 패널 배율 글자.
    // 판은 글자 줄 상자 둘레에 6텍셀(테두리 2 포함)이라 높이가 아이템 타일과 같은 28텍셀(112px), 흰 선까지 120px. 폭은 글자에 따라 정해진다.
    // 마우스를 올리면 카드 호버처럼 흰 판 모양을 바깥으로 1텍셀 더 크게 먼저 깐다. tint는 판·글자에 곱한다 — 누를 수 없을 때 어둡게.
    // 좌표는 기준 캔버스 픽셀 (PixelGui.BeginCanvas 안에서 부른다).
    public static class PlateButtonPainter
    {
        const int Scale = PixelGui.PanelScale;
        const int LineTexels = 1;
        const int PadTexels = 6;

        public static Vector2Int Size(PixelUiSkin skin, string label)
        {
            int line = LineTexels * Scale;
            int pad = PadTexels * Scale;
            int plateW = PixelGui.MeasureText(skin.FontPlain, label, Scale) + 2 * pad;
            int plateH = skin.FontPlain.lineHeight * Scale + 2 * pad;
            return new Vector2Int(plateW + 2 * line, plateH + 2 * line);
        }

        public static void Draw(PixelUiSkin skin, Rect button, string label, bool hovered, Color tint)
        {
            int line = LineTexels * Scale;
            int pad = PadTexels * Scale;
            if (hovered)
            {
                int o = UpgradeCardPainter.HoverOutlineTexels * Scale;
                PixelGui.FillPlateShape(new Rect(button.x - o, button.y - o, button.width + 2 * o, button.height + 2 * o), Scale, Color.white);
            }
            PixelGui.FillPlateShape(button, Scale, Color.white * tint);
            var plate = new Rect(button.x + line, button.y + line, button.width - 2 * line, button.height - 2 * line);
            PixelGui.DrawWeaponPlate(skin, plate, Scale, tint);
            var prevColor = GUI.color;
            GUI.color = prevColor * tint;
            PixelGui.DrawTextCentered(skin.FontPlain, label, (int)plate.center.x, (int)plate.y + pad, Scale);
            GUI.color = prevColor;
        }
    }
}
