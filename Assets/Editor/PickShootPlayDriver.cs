using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace PickShoot
{
    // 녹화용 자동 플레이 봇 (PlayRecorder의 IPlayDriver). 가상 키보드·마우스 장치에 매 프레임 상태 이벤트를 넣는다.
    // 이동: 가까운 적·스폰 예고에서 멀어지는 힘 + 정착한 탄환으로 끌리는 힘 + 아레나 안쪽으로 미는 힘의 합을 8방향 키로 양자화.
    // 조준·사격: 가장 가까운 적에 커서를 두고 탄약이 있으면 좌클릭을 유지. 적이 붙으면 능력(대시·펄스·충격파 — 캐릭터마다 하나). 웨이브 사이 등 할 일이 없으면 중앙으로.
    // 카드 화면: IMGUI 버튼이라 가상 마우스로 못 누르므로 등장 연출이 끝난 뒤 UpgradeManager.Select를 reflection으로 부른다.
    // 러닝 시작 화면(startPick)도 같은 길이다 — characterPool에서 SelectCharacter로 캐릭터를 고르고(기본 무작위) 곧바로 currentCards(그 캐릭터의 시작 무기)에서 무작위
    // 무기를 Select한다 (Characters §5). 화면의 밀어내기 연출은 거치지 않는다.
    // 사망: 데스 화면이 뜬 뒤 R을 한 프레임 눌러 재시작한다. 난수는 고정 시드 — 카드 선택만 봇의 몫이고 스폰은 게임의 난수를 따른다.
    // 녹화 요청(record.active)의 봇 옵션 — 소개 영상의 장면 연출용 (2026-10-14, 2026-10-15 확장). 없으면 평소 플레이(scenario=run).
    //   scenario=late — 시작 무기를 고른 뒤 무기를 weapons자루까지 채우고(종류를 돌아가며), 모두 Lv.level로 올리고, mods=1이면 개조를 돌아가며 붙이고,
    //                   유물 몇 개를 얻은 다음 웨이브를 skip_to까지 즉시 끝내며 건너뛴다(카드는 평소처럼 고른다). 죽어서 재시작하면 다시 붙인다.
    //   scenario=pickup — 적 없이(spawns=0을 함께 건다) "쏜다 → 탄이 남는다 → 걸어가 줍는다"를 되풀이한다. aim_deg 방향(도, 0 = 오른쪽)을 중심으로
    //                   흔들며 shoot_seconds 동안 쏘고, 정착한 탄환을 다 주울 때까지 걷는다. 다음 차례는 반대 방향으로 쏜다.
    //                   pickup_delay초 동안은 쏘지 않고 서 있는다(빈 필드에 혼자 선 장면). aim=spin이면 쏘는 동안 흔드는 대신 커서를 돌려 사방으로 쏜다.
    //                   pickup_ability=1이면 줍는 동안 ability_range 안에 정착 탄환이 PickupAbilityMinBullets개 이상일 때 능력을 쓴다 (Collector의 탄환 끌어오기).
    //   build=late — scenario와 무관하게 late의 빌드(weapons·level·mods·유물)를 붙인다. scenario=late는 늘 붙인다.
    //   card_wait — 카드 화면이 뜬 뒤 고르기까지 초 (기본 CardWaitSeconds).
    //   hud=0 — HUD와 피해·경험치 숫자(GameHud·DamageNumbers)를 끄고 찍는다. 영상에 자막을 따로 얹을 때 게임 글자가 겹치지 않게.
    //   character=<CharacterData 자산 이름> — 시작 화면에서 그 캐릭터를 고른다 (예: Scout·Hunter·Collector). 없으면 무작위.
    //   start_weapon=<WeaponData 자산 이름> — 시작 무기 카드 중 그 무기를 고른다 (그 캐릭터의 시작 무기에 없으면 예외). 없으면 무작위.
    //   relics=<RelicData 자산 이름>,… — 시작 무기를 고른 뒤 한 번 이 유물들을 더 얻는다 (같은 이름을 여러 번 — 예: MagnetCore를 겹쳐 자석 반경을 키운다).
    //   seed=N — 봇 난수(카드·캐릭터)와 UnityEngine.Random의 시드. 같은 요청을 다른 판으로 다시 찍을 때.
    //   spawns=0 — 첫 웨이브 카운트다운을 끝나지 않게 늘려 적이 나오지 않게 한다 (WaveManager.betweenWaveDelay — 시작 화면 동안 바꿔야 먹는다).
    //   aim=spin — 커서를 플레이어 둘레로 spin_speed(도/초)로 돌리며 탄약이 있으면 계속 쏜다 (사방 사격).
    //   serpents=N — 시작 무기를 고른 뒤(late면 skip_to 웨이브가 시작된 뒤) 서펜트(WaveManager의 보스 프리팹) N마리를 한 번에 소환한다.
    //   spawn=<적 프리팹 이름>:N — 같은 때 그 적을 N마리 WaveManager.SpawnNow로 소환한다 (예: Enemy_Collector:6). 등급 없는 노멀.
    //   ring=<적 프리팹 이름>:N — 같은 때 그 적 N마리를 플레이어 둘레 ring_radius(기본 10) 원 위에 고르게 놓는다 — 사방에서 몰려오는 장면.
    //   line=<적>:N — 플레이어 앞(layout_deg, 기본 0 = 오른쪽) layout_dist(기본 4)부터 layout_gap(기본 1.5) 간격의 한 줄. block=<적>:N — 같은 방향 layout_dist에 중심을 둔 덩어리.
    //   place_rank=normal|champion|ultra — ring·line·block으로 놓은 적의 등급 (크기·HP). freeze=1 — ring·line·block으로 놓은 적의 이동(EnemyMover)을 끄고 몸을 고정한다 (넉백에도 안 밀림). 관통·폭발·도탄·유도를 보여 주는 장면 연출용.
    //   aim_sweep — pickup이 쏘는 동안 aim_deg 둘레로 흔드는 폭(도, 기본 35). 0이면 한 점을 겨눈다.
    //   spawn_rank=normal|champion|ultra — spawn의 등급. 챔피언·울트라는 등급 크기의 예고를 거쳐 웨이브 강화와 함께 나온다(WaveManager.SpawnNow(prefab, kind)).
    //   spawn_delay=초 — serpents·spawn·ring을 그만큼 늦게 낸다 (탄을 먼저 깔아 두고 수집형을 부르는 장면). pickup_collect=0이면 pickup이 쏜 뒤 줍지 않고 서 있는다.
    //   build_weapons=<WeaponData 이름>,… · build_mods=<ModData 이름>,… — late 빌드가 채울 무기·붙일 개조 (돌아가며). 하나만 주면 모두 같은 무기·개조.
    //   ability_range — 이 거리 안에 적이 있으면 능력을 쓴다 (기본 AbilityDangerDistance). 캐릭터 소개 장면에서 능력을 자주 보이려고 키운다.
    //   track=1 — 매 프레임 플레이어의 화면 좌표(Game view 픽셀, 좌하단 원점)를 <output>.track.csv로 남긴다. 편집에서 플레이어를 따라가는 확대 크롭용.
    //             프레임 번호는 영상 프레임과 같다(Tick은 그 프레임이 캡처되기 전 Update에서 불린다). 카메라는 LateUpdate에서 움직이므로 한 프레임 늦은 카메라 기준이다.
    public class PickShootPlayDriver : IPlayDriver
    {
        static readonly string[] LateWeapons = { "SMG", "Shotgun", "Launcher", "SniperRifle", "AssaultRifle", "MachineGun", "Pistol" };
        static readonly string[] LateMods = { "Explosive", "Ricochet", "Homing", "Drill" };
        static readonly string[] LateRelics = { "PowerCore", "PowerCore", "ArmorPlate", "ArmorPlate", "ArmorPlate", "RegenUnit", "LightFrame" };

        const float EnemyAvoidRadius = 5f;
        const float ChargerAvoidRadius = 7f;      // 돌진형은 멀리서부터 피한다
        const float MarkerAvoidRadius = 3f;
        const float BulletSeekRadius = 14f;
        const float SafeHalfExtent = 11f;         // 아레나 안쪽 면(14)에서 여유를 둔 활동 범위
        const float AbilityDangerDistance = 1.8f;
        const float FireRange = 12f;
        const float CardWaitSeconds = 1.5f;       // 카드가 뽑힌 뒤 고르기까지 (실시간). 등장 연출(약 0.64초)보다 길어야 한다.
        const float RestartWaitSeconds = 3f;
        const int AbilityRepeatFrames = 15;
        const float KeyThreshold = 0.383f;        // sin 22.5° — 8방향 양자화
        const float MoveDeadZone = 0.15f;
        const int DefaultSeed = 20260929;
        const float PausedWaveDelay = 100000f;    // spawns=0 — 녹화 길이보다 충분히 긴 카운트다운
        const float AimDistance = 4f;             // 사방 사격·줍기 장면에서 커서를 둘 거리
        const float PickupSweepDegrees = 35f;     // 줍기 장면의 사격 방향 흔들기 폭(±)
        const float PickupSweepRate = 5f;         // 흔들기 각속도 (rad/s)
        const float PickupCollectTimeout = 8f;    // 다 못 주워도 이 시간이 지나면 다시 쏜다
        const int PickupAbilityMinBullets = 5;

        static readonly MethodInfo SelectMethod = typeof(UpgradeManager).GetMethod("Select", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo CardsShownAtField = typeof(UpgradeManager).GetField("cardsShownAt", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo CurrentCardsField = typeof(UpgradeManager).GetField("currentCards", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly MethodInfo SelectCharacterMethod = typeof(UpgradeManager).GetMethod("SelectCharacter", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo StartPickField = typeof(UpgradeManager).GetField("startPick", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo CharacterPoolField = typeof(UpgradeManager).GetField("characterPool", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo BetweenWaveDelayField = typeof(WaveManager).GetField("betweenWaveDelay", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo BossPrefabField = typeof(WaveManager).GetField("bossPrefab", BindingFlags.Instance | BindingFlags.NonPublic);

        enum PickupPhase { Shoot, Collect }

        Keyboard keyboard;
        Mouse mouse;
        System.Random rng = new System.Random(DefaultSeed);
        readonly List<Key> keys = new List<Key>(6);
        float selectedShownAt = -1f;
        float deathSeenAt = -1f;
        int lastAbilityFrame = -1000;
        Vector2 lastAimDirection = Vector2.right;
        Vector2 lastScreen;

        string scenario = "run";
        int skipTo = 5;
        int lateLevel = 6;
        int lateWeapons = 12;
        bool lateMods = true;
        bool lateBuild;
        float cardWait = CardWaitSeconds;
        bool hideHud;
        string characterName;
        string startWeapon;
        string[] extraRelics;
        bool pauseSpawns;
        bool spinAim;
        float spinSpeed = 300f;
        int serpentCount;
        string[] buildWeapons = LateWeapons;
        string[] buildMods = LateMods;
        string spawnName, ringName, lineName, blockName;
        int spawnCount, ringCount, lineCount, blockCount;
        float ringRadius = 10f;
        float layoutDegrees, layoutDistance = 4f, layoutGap = 1.5f;
        bool freezeEnemies;
        EnemyRankKind placeRank = EnemyRankKind.Normal;
        float aimSweep = PickupSweepDegrees;
        float abilityRange = AbilityDangerDistance;
        bool track;
        string output;
        float pickupAimDegrees;
        float shootSeconds = 1.5f;
        bool pickupAbility;
        bool pickupCollect = true;
        float spawnDelay;
        EnemyRankKind spawnRank = EnemyRankKind.Normal;
        float extrasReadyAt = -1f;
        float pickupDelay;
        float pickupStartedAt = -1f;

        GameObject setupFor;
        GameObject serpentsFor;
        GameObject relicsFor;
        WaveManager pausedWaves;
        PickupPhase pickupPhase = PickupPhase.Shoot;
        float pickupPhaseStart = -1f;
        readonly System.Text.StringBuilder trackLog = new System.Text.StringBuilder();

        public string Status { get; private set; } = "";

        public void Begin()
        {
            if (SelectMethod == null || CardsShownAtField == null || CurrentCardsField == null
                || SelectCharacterMethod == null || StartPickField == null || CharacterPoolField == null)
                throw new System.Exception("[PickShoot] PickShootPlayDriver: UpgradeManager의 Select/cardsShownAt/currentCards/SelectCharacter/startPick/characterPool을 찾지 못함 — 이름이 바뀌었으면 봇도 고쳐야 한다.");
            if (BetweenWaveDelayField == null || BossPrefabField == null)
                throw new System.Exception("[PickShoot] PickShootPlayDriver: WaveManager의 betweenWaveDelay/bossPrefab을 찾지 못함 — 이름이 바뀌었으면 봇도 고쳐야 한다.");
            ReadOptions();
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            lastScreen = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            if (track) trackLog.Append("frame,x,y\n");
        }

        public void End()
        {
            if (keyboard != null) { InputSystem.RemoveDevice(keyboard); keyboard = null; }
            if (mouse != null) { InputSystem.RemoveDevice(mouse); mouse = null; }
            if (track) System.IO.File.WriteAllText(output + ".track.csv", trackLog.ToString());
        }

        public void Tick(int frame)
        {
            keys.Clear();
            if (hideHud) HideHud();
            if (pauseSpawns) PauseSpawns();
            var player = GameObject.FindWithTag("Player");
            if (track) Track(frame, player);
            var upgrades = Object.FindFirstObjectByType<UpgradeManager>();
            if (upgrades != null && upgrades.IsActive)
            {
                TickCards(upgrades);
                Send(lastScreen, false);
                return;
            }
            if (player == null)
            {
                TickDead();
                Send(lastScreen, false);
                return;
            }
            deathSeenAt = -1f;
            bool built = !lateBuild || EnsureLateBuild(player);
            if (built && scenario == "late") SkipWaves();
            if (built && extraRelics != null) GiveRelics(player);
            if (built && (serpentCount > 0 || spawnName != null || ringName != null || lineName != null || blockName != null)) SpawnExtras(player);
            if (scenario == "pickup") TickPickup(player, frame);
            else TickPlay(player, frame);
        }

        void ReadOptions()
        {
            foreach (var raw in System.IO.File.ReadAllLines(PlayRecorderTrigger.ActivePath))
            {
                string line = raw.Trim();
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string key = line.Substring(0, eq).Trim();
                string value = line.Substring(eq + 1).Trim();
                switch (key)
                {
                    case "scenario": scenario = value; break;
                    case "skip_to": skipTo = int.Parse(value); break;
                    case "level": lateLevel = int.Parse(value); break;
                    case "weapons": lateWeapons = int.Parse(value); break;
                    case "mods": lateMods = int.Parse(value) != 0; break;
                    case "build": lateBuild = ParseBuild(value); break;
                    case "card_wait": cardWait = ParseFloat(value); break;
                    case "hud": hideHud = int.Parse(value) == 0; break;
                    case "character": characterName = value; break;
                    case "start_weapon": startWeapon = value; break;
                    case "relics": extraRelics = value.Split(','); break;
                    case "seed":
                        int seed = int.Parse(value);
                        rng = new System.Random(seed);
                        Random.InitState(seed);
                        break;
                    case "spawns": pauseSpawns = int.Parse(value) == 0; break;
                    case "aim": spinAim = ParseAim(value); break;
                    case "spin_speed": spinSpeed = ParseFloat(value); break;
                    case "serpents": serpentCount = int.Parse(value); break;
                    case "spawn": (spawnName, spawnCount) = ParseSpawn(key, value); break;
                    case "ring": (ringName, ringCount) = ParseSpawn(key, value); break;
                    case "ring_radius": ringRadius = ParseFloat(value); break;
                    case "line": (lineName, lineCount) = ParseSpawn(key, value); break;
                    case "block": (blockName, blockCount) = ParseSpawn(key, value); break;
                    case "layout_deg": layoutDegrees = ParseFloat(value); break;
                    case "layout_dist": layoutDistance = ParseFloat(value); break;
                    case "layout_gap": layoutGap = ParseFloat(value); break;
                    case "freeze": freezeEnemies = int.Parse(value) != 0; break;
                    case "place_rank": placeRank = (EnemyRankKind)System.Enum.Parse(typeof(EnemyRankKind), value, true); break;
                    case "aim_sweep": aimSweep = ParseFloat(value); break;
                    case "build_weapons": buildWeapons = value.Split(','); break;
                    case "build_mods": buildMods = value.Split(','); break;
                    case "ability_range": abilityRange = ParseFloat(value); break;
                    case "track": track = int.Parse(value) != 0; break;
                    case "output": output = value; break;
                    case "aim_deg": pickupAimDegrees = ParseFloat(value); break;
                    case "shoot_seconds": shootSeconds = ParseFloat(value); break;
                    case "pickup_ability": pickupAbility = int.Parse(value) != 0; break;
                    case "pickup_delay": pickupDelay = ParseFloat(value); break;
                    case "pickup_collect": pickupCollect = int.Parse(value) != 0; break;
                    case "spawn_delay": spawnDelay = ParseFloat(value); break;
                    case "spawn_rank": spawnRank = (EnemyRankKind)System.Enum.Parse(typeof(EnemyRankKind), value, true); break;
                }
            }
            if (scenario != "run" && scenario != "late" && scenario != "pickup")
                throw new System.Exception($"[PickShoot] PickShootPlayDriver: scenario={scenario} — run·late·pickup만 된다.");
            if (scenario == "late") lateBuild = true;
            if (scenario == "pickup") pauseSpawns = true;
            if (serpentCount < 0)
                throw new System.Exception($"[PickShoot] PickShootPlayDriver: serpents={serpentCount} — 0 이상이어야 한다.");
            if (shootSeconds <= 0f)
                throw new System.Exception($"[PickShoot] PickShootPlayDriver: shoot_seconds={shootSeconds} — 양수여야 한다.");
            if (track && (string.IsNullOrEmpty(output) || !System.IO.Path.IsPathRooted(output)))
                throw new System.Exception($"[PickShoot] PickShootPlayDriver: track=1은 output이 절대 경로여야 한다 (지금 '{output}').");
        }

        static float ParseFloat(string value) => float.Parse(value, CultureInfo.InvariantCulture);

        static (string, int) ParseSpawn(string key, string value)
        {
            int colon = value.LastIndexOf(':');
            if (colon <= 0)
                throw new System.Exception($"[PickShoot] PickShootPlayDriver: {key}={value} — <적 프리팹 이름>:<수> 꼴이어야 한다.");
            int count = int.Parse(value.Substring(colon + 1));
            if (count <= 0)
                throw new System.Exception($"[PickShoot] PickShootPlayDriver: {key}={value} — 수는 양수여야 한다.");
            return (value.Substring(0, colon).Trim(), count);
        }

        static bool ParseBuild(string value)
        {
            if (value == "late") return true;
            if (value == "start") return false;
            throw new System.Exception($"[PickShoot] PickShootPlayDriver: build={value} — late·start만 된다.");
        }

        static bool ParseAim(string value)
        {
            if (value == "spin") return true;
            if (value == "enemy") return false;
            throw new System.Exception($"[PickShoot] PickShootPlayDriver: aim={value} — enemy·spin만 된다.");
        }

        // GameHud(HUD·머리 위 게이지·크로스헤어)와 DamageNumbers(피해·경험치 숫자, LV UP!)는 OnGUI로 그리므로 컴포넌트를 끄면 그리지 않는다.
        // 재시작으로 씬이 다시 불리면 새 인스턴스가 생기므로 매 프레임 확인한다.
        static void HideHud()
        {
            var hud = Object.FindFirstObjectByType<GameHud>();
            if (hud != null && hud.enabled) hud.enabled = false;
            var numbers = Object.FindFirstObjectByType<DamageNumbers>();
            if (numbers != null && numbers.enabled) numbers.enabled = false;
        }

        // 첫 웨이브의 카운트다운(WaitForSeconds(betweenWaveDelay))은 시작 무기를 고른 직후에 만들어지므로 그 전(Idle)에 늘려 둔다.
        void PauseSpawns()
        {
            var waves = Object.FindFirstObjectByType<WaveManager>();
            if (waves == null || waves == pausedWaves) return;
            if (waves.Phase != WavePhase.Idle)
                throw new System.Exception($"[PickShoot] PickShootPlayDriver: spawns=0은 첫 웨이브 전에 걸어야 한다 (지금 phase {waves.Phase}).");
            BetweenWaveDelayField.SetValue(waves, PausedWaveDelay);
            pausedWaves = waves;
        }

        void Track(int frame, GameObject player)
        {
            var camera = Camera.main;
            if (player == null || camera == null)
            {
                trackLog.Append(frame).Append(",,\n");
                return;
            }
            Vector3 s = camera.WorldToScreenPoint(player.transform.position);
            trackLog.Append(frame).Append(',')
                .Append(s.x.ToString("F1", CultureInfo.InvariantCulture)).Append(',')
                .Append(s.y.ToString("F1", CultureInfo.InvariantCulture)).Append('\n');
        }

        // 시작 무기를 고른 뒤 한 번 후반 빌드를 붙인다(재시작하면 새 플레이어에 다시). 붙였으면 true.
        bool EnsureLateBuild(GameObject player)
        {
            if (setupFor == player) return true;
            var weapons = player.GetComponent<PlayerWeapons>();
            if (weapons.Slots.Count == 0) return false;   // 시작 무기 고르기 전
            SetupLateBuild(player, weapons);
            setupFor = player;
            return true;
        }

        void SkipWaves()
        {
            var waves = Object.FindFirstObjectByType<WaveManager>();
            // EndWaveNow는 요청 플래그만 세우므로 웨이브가 실제로 끝나기 전 몇 프레임 더 불러도 같다.
            if (waves != null && waves.Phase == WavePhase.Active && waves.CurrentWave < skipTo)
                waves.EndWaveNow();
        }

        void SetupLateBuild(GameObject player, PlayerWeapons weapons)
        {
            for (int i = 0; weapons.Slots.Count < lateWeapons && weapons.HasEmptySlot; i++)
                weapons.AddWeapon(LoadAsset<WeaponData>($"Assets/Data/Weapons/{buildWeapons[i % buildWeapons.Length].Trim()}.asset"));
            var mods = new ModData[buildMods.Length];
            for (int i = 0; i < mods.Length; i++)
            {
                mods[i] = LoadAsset<ModData>($"Assets/Data/Mods/{buildMods[i].Trim()}.asset");
                mods[i].Validate();
            }
            var slots = weapons.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                var w = slots[i];
                while (w.Level < lateLevel && !w.IsMaxLevel) w.LevelUp();
                if (lateMods && w.Mod == null) w.AttachMod(mods[i % mods.Length]);
            }
            var stats = player.GetComponent<PlayerStats>();
            foreach (var name in LateRelics)
                stats.AddRelic(LoadAsset<RelicData>($"Assets/Data/Relics/{name}.asset"));
            Status = $"late build: {slots.Count} weapons Lv{lateLevel}";
        }

        void GiveRelics(GameObject player)
        {
            if (relicsFor == player) return;
            if (player.GetComponent<PlayerWeapons>().Slots.Count == 0) return;   // 시작 무기 고르기 전
            var stats = player.GetComponent<PlayerStats>();
            foreach (var name in extraRelics)
                stats.AddRelic(LoadAsset<RelicData>($"Assets/Data/Relics/{name.Trim()}.asset"));
            relicsFor = player;
        }

        // 장면용 추가 소환 (serpents·spawn·ring) — SpawnNow는 웨이브 상태와 무관하게 플레이어 주변 스폰 거리에 놓고 웨이브 종료 정리를 받는다.
        // ring은 같은 일을 플레이어 둘레 원 위의 정해진 자리에 한다 — 씬의 적은 FindObjectsByType로 세므로 목록 등록이 따로 없다.
        // late는 건너뛰기가 끝난 웨이브(skip_to)에서 내야 건너뛰기의 웨이브 종료 정리에 지워지지 않는다.
        void SpawnExtras(GameObject player)
        {
            if (serpentsFor == player) return;
            if (player.GetComponent<PlayerWeapons>().Slots.Count == 0) return;   // 시작 무기 고르기 전
            var waves = Object.FindFirstObjectByType<WaveManager>();
            if (waves == null) return;
            if (scenario == "late" && (waves.Phase != WavePhase.Active || waves.CurrentWave < skipTo)) return;
            if (extrasReadyAt < 0f) extrasReadyAt = Time.time + spawnDelay;
            if (Time.time < extrasReadyAt) return;
            if (serpentCount > 0)
            {
                var boss = (GameObject)BossPrefabField.GetValue(waves);
                for (int i = 0; i < serpentCount; i++) waves.SpawnNow(boss);
            }
            if (spawnName != null)
            {
                var prefab = LoadAsset<GameObject>($"Assets/Prefabs/Enemies/{spawnName}.prefab");
                for (int i = 0; i < spawnCount; i++)
                {
                    if (spawnRank == EnemyRankKind.Normal) waves.SpawnNow(prefab);
                    else waves.SpawnNow(prefab, spawnRank);
                }
            }
            Vector2 center = player.transform.position;
            if (ringName != null)
            {
                var prefab = LoadAsset<GameObject>($"Assets/Prefabs/Enemies/{ringName}.prefab");
                for (int i = 0; i < ringCount; i++)
                    Place(prefab, center + Direction(360f * i / ringCount) * ringRadius);
            }
            if (lineName != null)
            {
                // 플레이어 앞(layout_deg 방향) layout_dist부터 layout_gap 간격의 한 줄.
                var prefab = LoadAsset<GameObject>($"Assets/Prefabs/Enemies/{lineName}.prefab");
                for (int i = 0; i < lineCount; i++)
                    Place(prefab, center + Direction(layoutDegrees) * (layoutDistance + i * layoutGap));
            }
            if (blockName != null)
            {
                // 플레이어 앞(layout_deg 방향) layout_dist에 중심을 둔 거의 정사각형 덩어리, layout_gap 간격.
                var prefab = LoadAsset<GameObject>($"Assets/Prefabs/Enemies/{blockName}.prefab");
                int cols = Mathf.CeilToInt(Mathf.Sqrt(blockCount));
                int rows = Mathf.CeilToInt(blockCount / (float)cols);
                Vector2 forward = Direction(layoutDegrees), side = new Vector2(-forward.y, forward.x);
                Vector2 origin = center + forward * layoutDistance;
                for (int i = 0; i < blockCount; i++)
                {
                    float f = (i / cols - (rows - 1) * 0.5f) * layoutGap;
                    float s = (i % cols - (cols - 1) * 0.5f) * layoutGap;
                    Place(prefab, origin + forward * f + side * s);
                }
            }
            serpentsFor = player;
            Status = "extra enemies spawned";
        }

        // 봇이 자리를 정해 놓는 적 (ring·line·block). freeze=1이면 이동(EnemyMover 계열)을 끄고 몸을 고정해 넉백에도 밀리지 않게 한다 — 장면 연출용.
        // 씬의 적은 FindObjectsByType로 세므로 목록 등록이 따로 없고, 웨이브 종료 정리도 평소대로 받는다.
        void Place(GameObject prefab, Vector2 position)
        {
            var enemy = Object.Instantiate(prefab, position, Quaternion.identity);
            if (placeRank != EnemyRankKind.Normal)
                enemy.GetComponent<EnemyHealth>().ApplyRank(Object.FindFirstObjectByType<WaveManager>().RankOf(placeRank));
            if (!freezeEnemies) return;
            foreach (var mover in enemy.GetComponents<EnemyMover>()) mover.enabled = false;
            var body = enemy.GetComponent<Rigidbody2D>();
            body.linearVelocity = Vector2.zero;
            body.constraints = RigidbodyConstraints2D.FreezeAll;
        }

        static T LoadAsset<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                throw new System.Exception($"[PickShoot] PickShootPlayDriver: 자산 없음 — {path}");
            return asset;
        }

        void TickCards(UpgradeManager upgrades)
        {
            float shownAt = (float)CardsShownAtField.GetValue(upgrades);
            // Select 뒤 PresentAndWait가 다음 프레임에 닫으므로 한 화면에 두 번 고르지 않도록 뽑힌 시각으로 구분한다.
            if (shownAt == selectedShownAt) { Status = "card chosen, closing"; return; }
            if (Time.unscaledTime - shownAt < cardWait) { Status = "cards shown"; return; }
            // 시작 화면 — 캐릭터를 고르면 currentCards가 그 캐릭터의 시작 무기로 바뀌므로 이어서 무기를 고른다.
            string character = "";
            bool startPick = (bool)StartPickField.GetValue(upgrades);
            if (startPick)
            {
                var pool = (System.Collections.IList)CharacterPoolField.GetValue(upgrades);
                int c = characterName == null ? rng.Next(pool.Count) : CharacterIndex(pool, characterName);
                SelectCharacterMethod.Invoke(upgrades, new object[] { c });
                character = $"character {c}, ";
            }
            var cards = (System.Array)CurrentCardsField.GetValue(upgrades);
            int index = startPick && startWeapon != null ? StartWeaponIndex(cards, startWeapon) : rng.Next(cards.Length);
            SelectMethod.Invoke(upgrades, new object[] { index });
            selectedShownAt = shownAt;
            Status = $"{character}card {index} chosen";
        }

        static int StartWeaponIndex(System.Array cards, string name)
        {
            var names = new List<string>();
            for (int i = 0; i < cards.Length; i++)
            {
                var option = (UpgradeOption)cards.GetValue(i);
                if (option.AcquireData == null) continue;
                if (option.AcquireData.name == name) return i;
                names.Add(option.AcquireData.name);
            }
            throw new System.Exception($"[PickShoot] PickShootPlayDriver: start_weapon={name}이 이 캐릭터의 시작 무기 카드에 없음 (있는 것: {string.Join(", ", names)}).");
        }

        static int CharacterIndex(System.Collections.IList pool, string name)
        {
            var names = new List<string>();
            for (int i = 0; i < pool.Count; i++)
            {
                var data = (Object)pool[i];
                if (data.name == name) return i;
                names.Add(data.name);
            }
            throw new System.Exception($"[PickShoot] PickShootPlayDriver: character={name}이 characterPool에 없음 (있는 것: {string.Join(", ", names)}).");
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

        // scenario=pickup — 적이 없는 필드에서 쏘기와 줍기를 번갈아 한다. 쏘는 동안은 제자리에서 aim_deg 둘레로 흔들며 쏘고,
        // 줍는 동안은 가장 가까운 정착 탄환으로 걷는다(자석 반경에 들면 PlayerPickup이 끌어온다). 남은 탄환이 없으면 반대 방향으로 다시 쏜다.
        void TickPickup(GameObject player, int frame)
        {
            Vector2 position = player.transform.position;
            var weapons = player.GetComponent<PlayerWeapons>();
            if (pickupStartedAt < 0f) pickupStartedAt = Time.time;
            if (Time.time - pickupStartedAt < pickupDelay)
            {
                Status = "pickup idle";
                Send(ToScreen(position + Direction(pickupAimDegrees) * AimDistance), false);
                return;
            }
            if (pickupPhaseStart < 0f) pickupPhaseStart = Time.time;
            float elapsed = Time.time - pickupPhaseStart;

            Bullet nearestBullet = null;
            float nearestBulletDistance = float.PositiveInfinity;
            int remaining = 0;
            int inAbilityRange = 0;
            foreach (var bullet in Object.FindObjectsByType<Bullet>(FindObjectsSortMode.None))
            {
                remaining++;
                if (!bullet.IsSettled || bullet.IsMagnetized) continue;
                float distance = ((Vector2)bullet.transform.position - position).magnitude;
                if (distance < abilityRange) inAbilityRange++;
                if (distance < nearestBulletDistance) { nearestBulletDistance = distance; nearestBullet = bullet; }
            }

            bool fire = false;
            float aimDegrees = pickupAimDegrees;
            if (pickupPhase == PickupPhase.Shoot)
            {
                aimDegrees += spinAim ? elapsed * spinSpeed : Mathf.Sin(elapsed * PickupSweepRate) * aimSweep;
                fire = HasAmmo(weapons);
                if (elapsed >= shootSeconds) { pickupPhase = PickupPhase.Collect; pickupPhaseStart = Time.time; }
            }
            else
            {
                if (pickupCollect && nearestBullet != null) PressMove((Vector2)nearestBullet.transform.position - position);
                var ability = player.GetComponent<PlayerAbility>();
                if (pickupAbility && ability != null && ability.Ready && inAbilityRange >= PickupAbilityMinBullets
                    && frame - lastAbilityFrame > AbilityRepeatFrames)
                {
                    keys.Add(Key.Space);
                    lastAbilityFrame = frame;
                }
                if (remaining == 0 || elapsed >= PickupCollectTimeout)
                {
                    pickupPhase = PickupPhase.Shoot;
                    pickupPhaseStart = Time.time;
                    pickupAimDegrees += 180f;
                }
            }
            Vector2 aimWorld = position + Direction(aimDegrees) * AimDistance;
            lastAimDirection = Direction(aimDegrees);
            Status = $"pickup {pickupPhase} bullets={remaining} fire={fire}";
            Send(ToScreen(aimWorld), fire);
        }

        void TickPlay(GameObject player, int frame)
        {
            Vector2 position = player.transform.position;
            var ability = player.GetComponent<PlayerAbility>();
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

            PressMove(avoid * 2f + seek + edge * 1.5f + drift);

            if (ability != null && nearestEnemyDistance < abilityRange && ability.Ready
                && frame - lastAbilityFrame > AbilityRepeatFrames)
            {
                keys.Add(Key.Space);
                lastAbilityFrame = frame;
            }

            bool hasAmmo = HasAmmo(weapons);
            Vector2 aimWorld;
            bool fire;
            if (spinAim)
            {
                // 사방 사격 — 커서를 플레이어 둘레로 돌리고 탄약이 있는 한 쏜다.
                aimWorld = position + Direction(Time.time * spinSpeed) * AimDistance;
                fire = hasAmmo;
            }
            else
            {
                if (nearestEnemy != null) aimWorld = nearestEnemy.position;
                else if (nearestBullet != null) aimWorld = nearestBullet.transform.position;
                else aimWorld = position + lastAimDirection * 3f;
                fire = nearestEnemy != null && nearestEnemyDistance < FireRange && hasAmmo;
            }
            Vector2 aimDelta = aimWorld - position;
            if (aimDelta.sqrMagnitude > 0.01f) lastAimDirection = aimDelta.normalized;

            var waves = Object.FindFirstObjectByType<WaveManager>();
            Status = $"wave={(waves != null ? waves.CurrentWave : 0)} pos=({position.x:F1},{position.y:F1}) nearest={nearestEnemyDistance:F1} ammo={hasAmmo} fire={fire} keys={keys.Count}";
            Send(ToScreen(aimWorld), fire);
        }

        static bool HasAmmo(PlayerWeapons weapons)
        {
            if (weapons == null) return false;
            foreach (var weapon in weapons.Slots)
                if (weapon.CurrentAmmo > 0) return true;
            return false;
        }

        static Vector2 Direction(float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(r), Mathf.Sin(r));
        }

        // 이동 벡터를 8방향 키로 양자화해 keys에 더한다. 짧으면(데드존) 서 있는다.
        void PressMove(Vector2 move)
        {
            if (move.magnitude < MoveDeadZone) return;
            Vector2 direction = move.normalized;
            if (direction.x > KeyThreshold) keys.Add(Key.D);
            else if (direction.x < -KeyThreshold) keys.Add(Key.A);
            if (direction.y > KeyThreshold) keys.Add(Key.W);
            else if (direction.y < -KeyThreshold) keys.Add(Key.S);
        }

        Vector2 ToScreen(Vector2 world)
        {
            var camera = Camera.main;
            Vector2 screen = camera != null ? (Vector2)camera.WorldToScreenPoint(world) : lastScreen;
            screen.x = Mathf.Clamp(screen.x, 0f, Screen.width - 1);
            screen.y = Mathf.Clamp(screen.y, 0f, Screen.height - 1);
            return screen;
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
