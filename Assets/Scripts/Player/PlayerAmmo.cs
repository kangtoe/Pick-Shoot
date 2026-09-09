using UnityEngine;

namespace PickShoot
{
    public class PlayerAmmo : MonoBehaviour
    {
        [SerializeField] int maxAmmo = 6;

        int current;

        public int Current => current;
        public int Max => maxAmmo;
        public bool HasAny => current > 0;
        public bool IsFull => current >= maxAmmo;

        void Awake()
        {
            current = maxAmmo;
        }

        public bool TryConsume()
        {
            if (current <= 0) return false;
            current--;
            return true;
        }

        public bool TryAdd()
        {
            if (current >= maxAmmo) return false;
            current++;
            return true;
        }

        void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(12f, 8f, 240f, 32f), $"Ammo  {current} / {maxAmmo}", style);
        }
    }
}
