using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PickShoot
{
    // Phase 1 프로토타입용 씬 세팅 자동화. Phase 1 완료 후 삭제 예정.
    public static class SceneSetup
    {
        const float ArenaSize = 20f;
        const float WallThickness = 1f;
        const string BounceMatPath = "Assets/Art/Physics/BounceWall.physicsMaterial2D";
        const string CircleSpritePath = "Assets/Art/Sprites/Circle.png";
        const string SquareSpritePath = "Assets/Art/Sprites/Square.png";
        const string BulletPrefabPath = "Assets/Prefabs/Bullet.prefab";
        const string EnemyPrefabPath = "Assets/Prefabs/Enemy.prefab";
        const int SpriteResolution = 64;

        [MenuItem("PickShoot/Setup/1-A · Create Arena")]
        public static void CreateArena()
        {
            Physics2D.gravity = Vector2.zero;

            ConfigureCamera();
            var bounceMat = EnsureBounceMaterial();

            var existing = GameObject.Find("Arena");
            if (existing != null)
            {
                if (!EditorUtility.DisplayDialog(
                        "Arena Exists",
                        "'Arena' 오브젝트가 이미 있습니다. 재생성하시겠습니까?",
                        "재생성", "취소"))
                    return;
                Object.DestroyImmediate(existing);
            }

            var arena = new GameObject("Arena");
            var walls = new GameObject("Walls");
            walls.transform.SetParent(arena.transform);

            float half = ArenaSize / 2f;
            float wallOffset = half + WallThickness / 2f;
            float horizontalLen = ArenaSize + 2f * WallThickness;

            CreateWall(walls.transform, "WallTop",    new Vector2(0f,  wallOffset), new Vector2(horizontalLen, WallThickness), bounceMat);
            CreateWall(walls.transform, "WallBottom", new Vector2(0f, -wallOffset), new Vector2(horizontalLen, WallThickness), bounceMat);
            CreateWall(walls.transform, "WallLeft",   new Vector2(-wallOffset, 0f), new Vector2(WallThickness, ArenaSize),      bounceMat);
            CreateWall(walls.transform, "WallRight",  new Vector2( wallOffset, 0f), new Vector2(WallThickness, ArenaSize),      bounceMat);

            CreateBoundaryVisual(arena.transform);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Selection.activeGameObject = arena;
            Debug.Log($"[PickShoot] Arena {ArenaSize}x{ArenaSize} 생성 완료. 씬 저장 필요.");
        }

        static void ConfigureCamera()
        {
            var camGO = GameObject.Find("Main Camera");
            if (camGO == null)
            {
                camGO = new GameObject("Main Camera");
                camGO.tag = "MainCamera";
                camGO.AddComponent<Camera>();
                camGO.AddComponent<AudioListener>();
            }
            var cam = camGO.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = ArenaSize * 0.55f;
            cam.backgroundColor = new Color(0.08f, 0.08f, 0.1f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            camGO.transform.position = new Vector3(0f, 0f, -10f);
            camGO.transform.rotation = Quaternion.identity;
        }

        static PhysicsMaterial2D EnsureBounceMaterial()
        {
            var dir = Path.GetDirectoryName(BounceMatPath);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
                AssetDatabase.Refresh();
            }

            var mat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(BounceMatPath);
            if (mat == null)
            {
                mat = new PhysicsMaterial2D("BounceWall") { bounciness = 1f, friction = 0f };
                AssetDatabase.CreateAsset(mat, BounceMatPath);
            }
            else
            {
                mat.bounciness = 1f;
                mat.friction = 0f;
                EditorUtility.SetDirty(mat);
            }
            AssetDatabase.SaveAssets();
            return mat;
        }

        static void CreateWall(Transform parent, string name, Vector2 pos, Vector2 size, PhysicsMaterial2D mat)
        {
            var wall = new GameObject(name);
            wall.transform.SetParent(parent);
            wall.transform.position = pos;
            var box = wall.AddComponent<BoxCollider2D>();
            box.size = size;
            box.sharedMaterial = mat;
        }

        [MenuItem("PickShoot/Setup/1-B · Create Player")]
        public static void CreatePlayer()
        {
            var existing = GameObject.Find("Player");
            if (existing != null)
            {
                if (!EditorUtility.DisplayDialog(
                        "Player Exists",
                        "'Player' 오브젝트가 이미 있습니다. 재생성하시겠습니까?",
                        "재생성", "취소"))
                    return;
                Object.DestroyImmediate(existing);
            }

            var circleSprite = EnsureSprite(CircleSpritePath, filled: true);
            var squareSprite = EnsureSprite(SquareSpritePath, filled: false);

            var player = new GameObject("Player");
            player.transform.position = Vector3.zero;

            var sr = player.AddComponent<SpriteRenderer>();
            sr.sprite = circleSprite;
            sr.color = new Color(0.4f, 0.75f, 1f);
            sr.sortingOrder = 5;

            var rb = player.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.linearDamping = 0f;
            rb.angularDamping = 0f;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var col = player.AddComponent<CircleCollider2D>();
            col.radius = 0.45f;

            var indicator = new GameObject("AimIndicator");
            indicator.transform.SetParent(player.transform, false);
            indicator.transform.localPosition = new Vector3(0.6f, 0f, 0f);
            indicator.transform.localScale = new Vector3(0.4f, 0.15f, 1f);
            var isr = indicator.AddComponent<SpriteRenderer>();
            isr.sprite = squareSprite;
            isr.color = new Color(1f, 0.9f, 0.4f);
            isr.sortingOrder = 6;

            player.AddComponent<PickShoot.PlayerMovement>();
            player.AddComponent<PickShoot.PlayerAiming>();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Selection.activeGameObject = player;
            Debug.Log("[PickShoot] Player 생성 완료. 씬 저장 필요.");
        }

        [MenuItem("PickShoot/Setup/1-C · Setup Shooting")]
        public static void SetupShooting()
        {
            int bulletLayer = EnsureLayer("Bullet");
            Physics2D.IgnoreLayerCollision(bulletLayer, bulletLayer, true);

            var bounceMat = EnsureBounceMaterial();
            var circleSprite = EnsureSprite(CircleSpritePath, filled: true);
            var bulletPrefab = EnsureBulletPrefab(bounceMat, circleSprite, bulletLayer);

            var player = GameObject.Find("Player");
            if (player == null)
                throw new System.Exception("[PickShoot] Player 없음. '1-B · Create Player' 먼저 실행하세요.");

            var muzzle = player.transform.Find("AimIndicator");
            if (muzzle == null)
                throw new System.Exception("[PickShoot] AimIndicator 자식이 없습니다. Player 재생성 필요.");

            var shooter = player.GetComponent<PickShoot.PlayerShooter>();
            if (shooter == null) shooter = player.AddComponent<PickShoot.PlayerShooter>();

            var bulletComponent = bulletPrefab.GetComponent<PickShoot.Bullet>();
            if (bulletComponent == null)
                throw new System.Exception("[PickShoot] Bullet 프리팹에 Bullet 컴포넌트가 없습니다.");

            var so = new SerializedObject(shooter);
            so.FindProperty("bulletPrefab").objectReferenceValue = bulletComponent;
            so.FindProperty("muzzle").objectReferenceValue = muzzle;
            so.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[PickShoot] 1-C 사격 세팅 완료. 씬 저장 필요.");
        }

        static GameObject EnsureBulletPrefab(PhysicsMaterial2D mat, Sprite sprite, int layer)
        {
            var dir = Path.GetDirectoryName(BulletPrefabPath);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
                AssetDatabase.Refresh();
            }

            var temp = new GameObject("Bullet");
            temp.layer = layer;
            temp.transform.localScale = new Vector3(0.3f, 0.3f, 1f);

            var sr = temp.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = new Color(1f, 0.75f, 0.2f);
            sr.sortingOrder = 4;

            var rb = temp.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.linearDamping = 0.7f;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var col = temp.AddComponent<CircleCollider2D>();
            col.radius = 0.5f;
            col.sharedMaterial = mat;

            var bullet = temp.AddComponent<PickShoot.Bullet>();

            var soBullet = new SerializedObject(bullet);
            soBullet.FindProperty("settleSpeedThreshold").floatValue = 0.4f;
            soBullet.FindProperty("settleTime").floatValue = 0.2f;
            soBullet.FindProperty("pickupDelay").floatValue = 0.2f;
            soBullet.FindProperty("damage").intValue = 1;
            soBullet.FindProperty("knockbackForce").floatValue = 6f;
            soBullet.FindProperty("postHitSpeedMultiplier").floatValue = 0.6f;
            soBullet.ApplyModifiedProperties();

            var prefab = PrefabUtility.SaveAsPrefabAsset(temp, BulletPrefabPath);
            Object.DestroyImmediate(temp);
            AssetDatabase.SaveAssets();
            if (prefab == null)
                throw new System.Exception($"[PickShoot] Bullet 프리팹 저장 실패: {BulletPrefabPath}");
            return prefab;
        }

        [MenuItem("PickShoot/Setup/1-D · Setup Ammo & Pickup")]
        public static void SetupAmmoAndPickup()
        {
            var player = GameObject.Find("Player");
            if (player == null)
                throw new System.Exception("[PickShoot] Player 없음. '1-B · Create Player' 먼저 실행.");

            var shooter = player.GetComponent<PickShoot.PlayerShooter>();
            if (shooter == null)
                throw new System.Exception("[PickShoot] PlayerShooter 없음. '1-C · Setup Shooting' 먼저 실행.");

            var ammo = player.GetComponent<PickShoot.PlayerAmmo>();
            if (ammo == null) ammo = player.AddComponent<PickShoot.PlayerAmmo>();

            var pickup = player.GetComponent<PickShoot.PlayerPickup>();
            if (pickup == null) pickup = player.AddComponent<PickShoot.PlayerPickup>();

            var soShooter = new SerializedObject(shooter);
            soShooter.FindProperty("ammo").objectReferenceValue = ammo;
            soShooter.ApplyModifiedProperties();

            var soPickup = new SerializedObject(pickup);
            soPickup.FindProperty("ammo").objectReferenceValue = ammo;
            soPickup.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[PickShoot] 1-D 회수/재장전 세팅 완료. 씬 저장 필요.");
        }

        [MenuItem("PickShoot/Setup/2-C · Setup Waves")]
        public static void SetupWaves()
        {
            var enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
            if (enemyPrefab == null)
                throw new System.Exception($"[PickShoot] Enemy 프리팹 없음. '2-A · Setup Enemy' 먼저 실행. ({EnemyPrefabPath})");

            var testEnemies = GameObject.Find("TestEnemies");
            if (testEnemies != null) Object.DestroyImmediate(testEnemies);

            var gm = GameObject.Find("GameManager");
            if (gm == null) gm = new GameObject("GameManager");

            var wm = gm.GetComponent<PickShoot.WaveManager>();
            if (wm == null) wm = gm.AddComponent<PickShoot.WaveManager>();

            var so = new SerializedObject(wm);
            so.FindProperty("enemyPrefab").objectReferenceValue = enemyPrefab;
            so.FindProperty("arenaHalfExtents").vector2Value = new Vector2(ArenaSize * 0.5f - 1f, ArenaSize * 0.5f - 1f);
            so.FindProperty("baseEnemies").intValue = 3;
            so.FindProperty("enemiesPerWaveIncrement").intValue = 2;
            so.FindProperty("betweenWaveDelay").floatValue = 2f;
            so.FindProperty("spawnInterval").floatValue = 0.5f;
            so.FindProperty("minSpawnDistanceFromPlayer").floatValue = 5f;
            so.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[PickShoot] 2-C 웨이브 세팅 완료. TestEnemies 제거됨. 씬 저장 필요.");
        }

        [MenuItem("PickShoot/Setup/2-B · Setup Player Health & Death")]
        public static void SetupPlayerHealthAndDeath()
        {
            var player = GameObject.Find("Player");
            if (player == null)
                throw new System.Exception("[PickShoot] Player 없음. '1-B · Create Player' 먼저 실행.");

            var health = player.GetComponent<PickShoot.PlayerHealth>();
            if (health == null) health = player.AddComponent<PickShoot.PlayerHealth>();

            var gm = GameObject.Find("GameManager");
            if (gm == null) gm = new GameObject("GameManager");

            var overScreen = gm.GetComponent<PickShoot.GameOverScreen>();
            if (overScreen == null) overScreen = gm.AddComponent<PickShoot.GameOverScreen>();

            var so = new SerializedObject(overScreen);
            so.FindProperty("playerHealth").objectReferenceValue = health;
            so.ApplyModifiedProperties();

            EnsureActiveSceneInBuildSettings();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[PickShoot] 2-B 세팅 완료. 씬 저장 필요.");
        }

        static void EnsureActiveSceneInBuildSettings()
        {
            var scenePath = EditorSceneManager.GetActiveScene().path;
            if (string.IsNullOrEmpty(scenePath))
                throw new System.Exception("[PickShoot] 활성 씬이 저장되지 않아 Build Settings 등록 불가.");

            var current = EditorBuildSettings.scenes;
            for (int i = 0; i < current.Length; i++)
                if (current[i].path == scenePath) return;

            var updated = new EditorBuildSettingsScene[current.Length + 1];
            System.Array.Copy(current, updated, current.Length);
            updated[current.Length] = new EditorBuildSettingsScene(scenePath, true);
            EditorBuildSettings.scenes = updated;
            Debug.Log($"[PickShoot] Build Settings에 씬 등록: {scenePath}");
        }

        [MenuItem("PickShoot/Setup/2-A · Setup Enemy")]
        public static void SetupEnemy()
        {
            int enemyLayer = EnsureLayer("Enemy");

            var player = GameObject.Find("Player");
            if (player == null)
                throw new System.Exception("[PickShoot] Player 없음. '1-B · Create Player' 먼저 실행.");
            if (player.tag != "Player") player.tag = "Player";

            var enemyPrefab = EnsureEnemyPrefab(enemyLayer);

            var container = GameObject.Find("TestEnemies");
            if (container != null) Object.DestroyImmediate(container);
            container = new GameObject("TestEnemies");

            Vector2[] spawnPositions =
            {
                new Vector2( 6f,  6f),
                new Vector2(-6f,  6f),
                new Vector2( 0f, -7f),
            };
            foreach (var pos in spawnPositions)
            {
                var enemyGO = (GameObject)PrefabUtility.InstantiatePrefab(enemyPrefab);
                enemyGO.transform.SetParent(container.transform, false);
                enemyGO.transform.position = pos;
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[PickShoot] 2-A 세팅 완료: Enemy 프리팹, Player 태그, 테스트 적 3마리.");
        }

        static GameObject EnsureEnemyPrefab(int layer)
        {
            var dir = Path.GetDirectoryName(EnemyPrefabPath);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
                AssetDatabase.Refresh();
            }

            var circleSprite = EnsureSprite(CircleSpritePath, filled: true);

            var temp = new GameObject("Enemy");
            temp.layer = layer;
            temp.transform.localScale = new Vector3(0.8f, 0.8f, 1f);

            var sr = temp.AddComponent<SpriteRenderer>();
            sr.sprite = circleSprite;
            sr.color = new Color(0.9f, 0.25f, 0.25f);
            sr.sortingOrder = 3;

            var rb = temp.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;

            var col = temp.AddComponent<CircleCollider2D>();
            col.radius = 0.5f;

            var movement = temp.AddComponent<PickShoot.EnemyMovement>();
            var health = temp.AddComponent<PickShoot.EnemyHealth>();

            var soMovement = new SerializedObject(movement);
            soMovement.FindProperty("moveSpeed").floatValue = 2.5f;
            soMovement.FindProperty("knockbackDamping").floatValue = 20f;
            soMovement.FindProperty("separationRadius").floatValue = 0.9f;
            soMovement.FindProperty("separationStrength").floatValue = 4f;
            soMovement.FindProperty("enemyLayerMask").intValue = 1 << layer;
            soMovement.ApplyModifiedProperties();

            var soHealth = new SerializedObject(health);
            soHealth.FindProperty("maxHp").intValue = 3;
            soHealth.FindProperty("flashDuration").floatValue = 0.08f;
            soHealth.FindProperty("flashColor").colorValue = Color.white;
            soHealth.ApplyModifiedProperties();

            var prefab = PrefabUtility.SaveAsPrefabAsset(temp, EnemyPrefabPath);
            Object.DestroyImmediate(temp);
            AssetDatabase.SaveAssets();
            if (prefab == null)
                throw new System.Exception($"[PickShoot] Enemy 프리팹 저장 실패: {EnemyPrefabPath}");
            return prefab;
        }

        static int EnsureLayer(string layerName)
        {
            int existing = LayerMask.NameToLayer(layerName);
            if (existing != -1) return existing;

            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets.Length == 0)
                throw new System.Exception("[PickShoot] TagManager.asset 로드 실패.");

            var tagManager = new SerializedObject(assets[0]);
            var layersProp = tagManager.FindProperty("layers");
            for (int i = 8; i < layersProp.arraySize; i++)
            {
                var element = layersProp.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(element.stringValue))
                {
                    element.stringValue = layerName;
                    tagManager.ApplyModifiedProperties();
                    AssetDatabase.SaveAssets();
                    Debug.Log($"[PickShoot] 레이어 '{layerName}' → 슬롯 {i} 생성.");
                    return i;
                }
            }
            throw new System.Exception($"[PickShoot] 빈 레이어 슬롯이 없어 '{layerName}' 생성 실패.");
        }

        static Sprite EnsureSprite(string path, bool filled)
        {
            var dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
                AssetDatabase.Refresh();
            }

            if (!File.Exists(path))
            {
                var tex = new Texture2D(SpriteResolution, SpriteResolution, TextureFormat.RGBA32, false);
                float half = SpriteResolution / 2f;
                float radius = half - 1f;
                for (int y = 0; y < SpriteResolution; y++)
                {
                    for (int x = 0; x < SpriteResolution; x++)
                    {
                        Color c = Color.clear;
                        if (filled)
                        {
                            float dx = x - half + 0.5f;
                            float dy = y - half + 0.5f;
                            if (dx * dx + dy * dy <= radius * radius) c = Color.white;
                        }
                        else
                        {
                            c = Color.white;
                        }
                        tex.SetPixel(x, y, c);
                    }
                }
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);

                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spritePixelsPerUnit = SpriteResolution;
                importer.filterMode = FilterMode.Bilinear;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
                throw new System.Exception($"[PickShoot] 스프라이트 로드 실패: {path}");
            return sprite;
        }

        static void CreateBoundaryVisual(Transform parent)
        {
            var vis = new GameObject("BoundaryVisual");
            vis.transform.SetParent(parent);

            var lr = vis.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.positionCount = 4;
            float half = ArenaSize / 2f;
            lr.SetPositions(new Vector3[]
            {
                new Vector3(-half, -half, 0f),
                new Vector3( half, -half, 0f),
                new Vector3( half,  half, 0f),
                new Vector3(-half,  half, 0f),
            });
            lr.widthMultiplier = 0.1f;

            var shader = Shader.Find("Sprites/Default");
            if (shader == null)
                throw new System.Exception("[PickShoot] 'Sprites/Default' 셰이더를 찾지 못했습니다.");
            var lineMat = new Material(shader);
            lr.sharedMaterial = lineMat;
            lr.startColor = lr.endColor = Color.white;
        }
    }
}
