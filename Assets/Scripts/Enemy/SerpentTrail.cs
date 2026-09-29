using System.Collections.Generic;
using UnityEngine;

namespace PickShoot
{
    // 서펜트 머리가 지나온 궤적 (Enemies §5). 머리가 Resolution만큼 옮길 때마다 점을 하나 남기므로 이웃한 점은 늘 정확히 Resolution 간격이고,
    // 마지막 점은 머리 자리(Head)에서 Resolution 미만 뒤에 있다. 몸통 마디는 머리에서 궤적을 따라 거슬러 올라간 거리로 자리를 얻는다(PointBehind).
    // 궤적이 그 거리보다 짧으면(갓 스폰돼 아직 풀려 나오는 중) 가장 오래된 점에 머문다.
    // 분열(Split)은 어떤 거리에서 궤적을 잘라 그 뒤쪽을 새 머리의 궤적으로 넘긴다. Unity 오브젝트에 기대지 않아 에디터 밖에서도 검증할 수 있다.
    public class SerpentTrail
    {
        public const float Resolution = 0.05f; // 점 간격 (월드 유닛, 1텍셀 미만)
        const int TrimSlack = 64;              // 이만큼 남아돌 때만 앞을 잘라 낸다 (매 스텝 목록을 밀지 않도록)

        readonly List<Vector2> points; // 오래된 점이 앞
        Vector2 head;

        public Vector2 Head => head;
        public int PointCount => points.Count;

        public SerpentTrail(Vector2 start)
        {
            points = new List<Vector2> { start };
            head = start;
        }

        SerpentTrail(List<Vector2> points, Vector2 head)
        {
            this.points = points;
            this.head = head;
        }

        // 머리가 to로 옮겼다. 지나온 길에 Resolution 간격으로 점을 남긴다.
        public void Record(Vector2 to)
        {
            Vector2 last = points[points.Count - 1];
            while (Vector2.Distance(last, to) >= Resolution)
            {
                last = Vector2.MoveTowards(last, to, Resolution);
                points.Add(last);
            }
            head = to;
        }

        // 머리에서 궤적을 따라 distance만큼 거슬러 올라간 점.
        public Vector2 PointBehind(float distance) => Locate(distance, out _);

        // 뒤에서 neededLength만큼만 남기고 앞을 잘라 낸다.
        public void Trim(float neededLength)
        {
            int needed = Mathf.CeilToInt(neededLength / Resolution) + 2;
            if (points.Count > needed + TrimSlack) points.RemoveRange(0, points.Count - needed);
        }

        // distance 뒤에서 궤적을 자른다. 그 자리(position)와 그보다 뒤의 점들로 새 궤적을 만들어 돌려주고,
        // direction은 그 자리에서 궤적이 향하던 방향(정규화 안 함, 뒤에 점이 없으면 zero).
        public SerpentTrail Split(float distance, out Vector2 position, out Vector2 direction)
        {
            position = Locate(distance, out int cut);
            var behind = points.GetRange(0, cut);
            direction = behind.Count > 0 ? position - behind[behind.Count - 1] : Vector2.zero;
            if (behind.Count == 0) behind.Add(position);
            return new SerpentTrail(behind, position);
        }

        // distance 뒤의 점. cut은 그 점보다 뒤(오래된 쪽)에 있는 점의 수.
        Vector2 Locate(float distance, out int cut)
        {
            int last = points.Count - 1;
            float toLast = Vector2.Distance(head, points[last]);
            if (distance <= toLast)
            {
                cut = points.Count;
                return Vector2.MoveTowards(head, points[last], distance);
            }
            float steps = (distance - toLast) / Resolution;
            int whole = Mathf.FloorToInt(steps);
            int a = last - whole; // 찾는 점은 points[a]와 points[a-1] 사이
            if (a <= 0)
            {
                cut = 0;
                return points[0];
            }
            cut = a;
            return Vector2.Lerp(points[a], points[a - 1], steps - whole);
        }
    }
}
