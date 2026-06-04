using UnityEngine;

namespace Curling.Match
{
    public class CameraSwitcher : MonoBehaviour
    {
        public Camera[] cameras;
        public string[] labels;
        public int currentIndex = 0;

        void Start()
        {
            ApplyActive();
        }

        void Update()
        {
            if (cameras == null || cameras.Length == 0) return;
            for (int i = 0; i < cameras.Length && i < 9; i++)
            {
                if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    currentIndex = i;
                    ApplyActive();
                }
            }
        }

        void ApplyActive()
        {
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] == null) continue;
                cameras[i].enabled = (i == currentIndex);
                var listener = cameras[i].GetComponent<AudioListener>();
                if (listener != null) listener.enabled = (i == currentIndex);
            }
        }

        void OnGUI()
        {
            if (cameras == null || cameras.Length == 0) return;
            var box = new GUIStyle(GUI.skin.box) { fontSize = 12, alignment = TextAnchor.UpperLeft };
            string txt = "[ Cameras ]\n";
            for (int i = 0; i < cameras.Length; i++)
            {
                string mark = (i == currentIndex) ? "▶" : "  ";
                string label = (labels != null && i < labels.Length) ? labels[i] : $"Cam {i + 1}";
                txt += $"{mark}{i + 1}: {label}\n";
            }
            GUI.Box(new Rect(Screen.width - 200, 12, 188, 20 + 16 * cameras.Length), txt, box);
        }
    }
}
