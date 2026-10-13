using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace PickShoot
{
    // 녹화용 자동 플레이 봇 (PlayRecorder의 IPlayDriver). 가상 키보드·마우스 장치에 매 프레임 상태 이벤트를 넣는다.
    // 이동: 가까운 적·스폰 예고에서 멀어지는 힘 + 정착한 탄환으로 끌리는 힘 + 아레나 안쪽으로 미는 힘의 합을 8방향 키로 양자화.
    // 조준·사격: 가장 가까운 적에 커서를 두고 탄약이 있으면 좌클릭을 유지. 적이 붙으면 대시. 웨이브 사이 등 할 일이 없으면 중앙으로.
    // 카드 화면: IMGUI 버튼이라 가상 마우스로 못 누르므로 등장 연출이 끝난 뒤 UpgradeManager.Select를 reflection으로 부른다.
    // 러닝 시작 화면(startPick)도 같은 길이다 — characterPool에서 SelectCharacter로 무작위 캐릭터를 고르고 곧바로 currentCards(그 캐릭터의 시작 무기)에서 무작위
    // 무기를 Select한다 (Characters §5). 화면의 밀어내기 연출은 거치지 않는다.
    // 사망: 데스 화면이 뜬 뒤 R을 한 프레임 눌러 재시작한다. 난수는 고정 시드 — 카드 선택만 봇의 몫이고 스폰은 게임의 난수를 따른다.
    public class PickShootPlayDriver : IPlayDriver
    {
        const float EnemyAvoidRadius = 5f;
        const float ChargerAvoidRadius = 7f;      // 돌진형은 멀리서부터 피한다
        const float MarkerAvoidRadius = 3f;
        const float BulletSeekRadius = 14f;
        const float SafeHalfExtent = 11f;         // 아레나 안쪽 면(14)에서 여유를 둔 활동 범위
        const float DashDangerDistance = 1.8f;
        const float FireRange = 12f;
        const float CardWaitSeconds = 1.5f;       // 카드가 뽑힌 뒤 고르기까지 (실시간). 등장 연출(약 0.64초)보다 길어야 한다.
        const float RestartWaitSeconds = 3f;
        const int DashRepeatFrames = 15;
        const float KeyThreshold = 0.383f;        // sin 22.5° — 8방향 양자화
        const float MoveDeadZone = 0.15f;

        static readonly MethodInfo SelectMethod = typeof(UpgradeManager).GetMethod("Select", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo CardsShownAtField = typeof(UpgradeManager).GetField("cardsShownAt", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo CurrentCardsField = typeof(UpgradeManager).GetField("currentCards", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly MethodInfo SelectCharacterMethod = typeof(UpgradeManager).GetMethod("SelectCharacter", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo StartPickField = typeof(UpgradeManager).GetField("startPick", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo CharacterPoolField = typeof(UpgradeManager).GetField("characterPool", BindingFlags.Instance | BindingFlags.NonPublic);

        Keyboard keyboard;
        Mouse mouse;
        readonly System.Random rng = new System.Random(20260929);
        readonly List<Key> keys = new List<Key>(6);
        float selectedShownAt = -1f;
        float deathSeenAt = -1f;
        int lastDashFrame = -1000;
        Vector2 lastAimDirection = Vector2.right;
        Vector2 lastScreen;

        public string Status { get; private set; } = "";

        public void Begin()
        {
            if (SelectMethod == null || CardsShownAtField == null || CurrentCardsField == null
                || SelectCharacterMethod == null || StartPickField == null || CharacterPoolField == null)
                throw new System.Exception("[PickShoot] PickShootPlayDriver: UpgradeManager의 Select/cardsShownAt/currentCards/SelectCharacter/startPick/characterPool을 찾지 못함 — 이름이 바뀌었으면 봇도 고쳐야 한다.");
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            lastScreen = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        }

        public void End()
        {
            if (keyboard != null) { InputSystem.RemoveDevice(keyboard); keyboard = null; }
            if (mouse != null) { InputSystem.RemoveDevice(mouse); mouse = null; }
        }

        public void Tick(int frame)
        {
            keys.Clear();
            var upgrades = Object.FindFirstObjectByType<UpgradeManager>();
            if (upgrades != null && upgrades.IsActive)
            {
                TickCards(upgrades);
                Send(lastScreen, false);
                return;
            }
            var player = GameObject.FindWithTag("Player");
            if (player == null)
            {
                TickDead();
                Send(lastScreen, false);
                return;
            }
            deathSeenAt = -1f;
            TickPlay(player, frame);
        }

        void TickCards(UpgradeManager upgrades)
        {
            float shownAt = (float)CardsShownAtField.GetValue(upgrades);
            // Select 뒤 PresentAndWait가 다음 프레임에 닫으므로 한 화면에 두 번 고르지 않도록 뽑힌 시각으로 구분한다.
            if (shownAt == selectedShownAt) { Status = "card chosen, closing"; return; }
            if (Time.unscaledTime - shownAt < CardWaitSeconds) { Status = "cards shown"; return; }
            // 시작 화면 — 캐릭터를 고르면 currentCards가 그 캐릭터의 시작 무기로 바뀌므로 이어서 무기를 고른다.
            string character = "";
            if ((bool)StartPickField.GetValue(upgrades))
            {
                var pool = (System.Collections.IList)CharacterPoolField.GetValue(upgrades);
                int c = rng.Next(pool.Count);
                SelectCharacterMethod.Invoke(upgrades, new object[] { c });
                character = $"character {c}, ";
            }
            var cards = (System.Array)CurrentCardsField.GetValue(upgrades);
            int index = rng.Next(cards.Length);
            SelectMethod.Invoke(upgrades, new object[] { index });
            selectedShownAt = shownAt;
            Status = $"{character}card {index} chosen";
        }

        // 데스 화면은 사망 1초 뒤에 뜬다. 3초마다 R을 눌러 재시작을 시도한다 (화면이 뜨기 전의 R은 무시된다).
        void TickDead()
        {
            if (deathSeenAt < 0f) { deathSeenAt = Time.unscaledTime; Status = "dead"; return; }
            if (Time.unscaledTime - deathSeenAt < RestartWaitSeconds) return;
            keys.Add(Key.R);
            deathSeenAt = Time.unscaledTime;
            Status = "restart";
        }

        void TickPlay(GameObject player, int frame)
        {
            Vector2 position = player.transform.position;
            var movement = player.GetComponent<PlayerMovement>();
            var weapons = player.GetComponent<PlayerWeapons>();

            Vector2 avoid = Vector2.zero;
            Transform nearestEnemy = null;
            float nearestEnemyDistance = float.PositiveInfinity;
            foreach (var enemy in Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None))
            {
                Vector2 delta = (Vector2)enemy.transform.position - position;
                float distance = delta.magnitude;
                if (distance < nearestEnemyDistance) { nearestEnemyDistance = distance; nearestEnemy = enemy.transform; }
                float radius = enemy.GetComponent<EnemyCharger>() != null ? ChargerAvoidRadius : EnemyAvoidRadius;
                avoid += Repulsion(delta, distance, radius);
            }
            foreach (var marker in Object.FindObjectsByType<SpawnWarningMarker>(FindObjectsSortMode.None))
            {
                Vector2 delta = (Vector2)marker.transform.position - position;
                avoid += Repulsion(delta, delta.magnitude, MarkerAvoidRadius) * 0.5f;
            }

            Bullet nearestBullet = null;
            float nearestBulletDistance = float.PositiveInfinity;
            foreach (var bullet in Object.FindObjectsByType<Bullet>(FindObjectsSortMode.None))
            {
                if (!bullet.IsSettled || bullet.IsMagnetized) continue;
                float distance = ((Vector2)bullet.transform.position - position).magnitude;
                if (distance < nearestBulletDistance) { nearestBulletDistance = distance; nearestBullet = bullet; }
            }
            Vector2 seek = Vector2.zero;
            if (nearestBullet != null && nearestBulletDistance < BulletSeekRadius)
                seek = ((Vector2)nearestBullet.transform.position - position).normalized * 0.8f;

            Vector2 edge = Vector2.zero;
            if (position.x > SafeHalfExtent) edge.x = SafeHalfExtent - position.x;
            else if (position.x < -SafeHalfExtent) edge.x = -SafeHalfExtent - position.x;
            if (position.y > SafeHalfExtent) edge.y = SafeHalfExtent - position.y;
            else if (position.y < -SafeHalfExtent) edge.y = -SafeHalfExtent - position.y;

            Vector2 drift = Vector2.zero;
            if (avoid == Vector2.zero && seek == Vector2.zero && position.magnitude > 3f)
                drift = -position.normalized * 0.4f;

            Vector2 move = avoid * 2f + seek + edge * 1.5f + drift;
            if (move.magnitude >= MoveDeadZone)
            {
                Vector2 direction = move.normalized;
                if (direction.x > KeyThreshold) keys.Add(Key.D);
                else if (direction.x < -KeyThreshold) keys.Add(Key.A);
                if (direction.y > KeyThreshold) keys.Add(Key.W);
                else if (direction.y < -KeyThreshold) keys.Add(Key.S);
            }

            if (movement != null && nearestEnemyDistance < DashDangerDistance && movement.DashReady && !movement.IsDashing
                && frame - lastDashFrame > DashRepeatFrames)
            {
                keys.Add(Key.Space);
                lastDashFrame = frame;
            }

            Vector2 aimWorld;
            if (nearestEnemy != null) aimWorld = nearestEnemy.position;
            else if (nearestBullet != null) aimWorld = nearestBullet.transform.position;
            else aimWorld = position + lastAimDirection * 3f;
            Vector2 aimDelta = aimWorld - position;
            if (aimDelta.sqrMagnitude > 0.01f) lastAimDirection = aimDelta.normalized;

            bool hasAmmo = false;
            if (weapons != null)
                foreach (var weapon in weapons.Slots)
                    if (weapon.CurrentAmmo > 0) { hasAmmo = true; break; }
            bool fire = nearestEnemy != null && nearestEnemyDistance < FireRange && hasAmmo;

            var camera = Camera.main;
            Vector2 screen = camera != null ? (Vector2)camera.WorldToScreenPoint(aimWorld) : lastScreen;
            screen.x = Mathf.Clamp(screen.x, 0f, Screen.width - 1);
            screen.y = Mathf.Clamp(screen.y, 0f, Screen.height - 1);

            var waves = Object.FindFirstObjectByType<WaveManager>();
            Status = $"wave={(waves != null ? waves.CurrentWave : 0)} pos=({position.x:F1},{position.y:F1}) nearest={nearestEnemyDistance:F1} ammo={hasAmmo} fire={fire} keys={keys.Count}";
            Send(screen, fire);
        }

        // 반지름 안에서 거리가 가까울수록 세게 밀어내는 힘. 반지름 밖이면 0.
        static Vector2 Repulsion(Vector2 delta, float distance, float radius)
        {
            if (distance >= radius || distance < 0.001f) return Vector2.zero;
            float weight = 1f - distance / radius;
            return -delta / distance * (weight * weight);
        }

        // 눌린 키 집합 전체와 커서 위치·버튼을 매 프레임 보낸다 (빈 집합 = 모두 뗌). wasPressedThisFrame류는 한 프레임만 넣는다.
        void Send(Vector2 screen, bool fire)
        {
            lastScreen = screen;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys.ToArray()));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = screen }.WithButton(MouseButton.Left, fire));
        }
    }
}
