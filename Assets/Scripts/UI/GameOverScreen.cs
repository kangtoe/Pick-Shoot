using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace PickShoot
{
    // OnGUI가 PlayerHealth의 데미지 플래시(붉은 오버레이) 위에 그려지도록
    // 실행 순서를 뒤로 밀어 마지막에 렌더링되게 한다.
    [DefaultExecutionOrder(100)]
    public class GameOverScreen : MonoBehaviour
    {
        [SerializeField] PlayerHealth playerHealth;
        [SerializeField] float gameOverDelay = 1f;

        bool gameOver;
        Texture2D overlayTex;

        void Awake()
        {
            if (playerHealth == null)
                throw new System.Exception("[PickShoot] GameOverScreen.playerHealth 미할당.");
            playerHealth.Died += OnPlayerDied;
        }

        void OnDestroy()
        {
            if (playerHealth != null) playerHealth.Died -= OnPlayerDied;
            if (overlayTex != null) Destroy(overlayTex);
        }

        void OnPlayerDied()
        {
            StartCoroutine(ShowAfterDelay());
        }

        IEnumerator ShowAfterDelay()
        {
            yield return new WaitForSeconds(gameOverDelay);
            gameOver = true;
        }

        void Update()
        {
            if (!gameOver) return;
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.rKey.wasPressedThisFrame) Restart();
        }

        void Restart()
        {
            var scene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(scene.buildIndex);
        }

        void OnGUI()
        {
            if (!gameOver) return;
            EnsureOverlay();

            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), overlayTex);

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 56,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            var hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.85f, 0.85f, 0.85f) }
            };

            GUI.Label(new Rect(0f, Screen.height * 0.42f, Screen.width, 80f), "YOU DIED", titleStyle);
            GUI.Label(new Rect(0f, Screen.height * 0.55f, Screen.width, 30f), "Press R to restart", hintStyle);
        }

        void EnsureOverlay()
        {
            if (overlayTex != null) return;
            overlayTex = new Texture2D(1, 1);
            overlayTex.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.7f));
            overlayTex.Apply();
        }
    }
}
