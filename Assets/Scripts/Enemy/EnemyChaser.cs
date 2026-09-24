using UnityEngine;

namespace PickShoot
{
    // 플레이어를 향해 등속으로 추적하는 이동 (기본 근접·빠른 근접·탱커). 항상 플레이어 쪽을 본다.
    public class EnemyChaser : EnemyMover
    {
        protected override Vector2 Steer()
        {
            if (Target == null) return Vector2.zero;
            Vector2 delta = (Vector2)Target.position - Body.position;
            Face(delta.x);
            if (delta.sqrMagnitude <= 0.0001f) return Vector2.zero;
            return delta.normalized * Data.MoveSpeed;
        }
    }
}
