using UnityEngine;

namespace Curling.Match
{
    public class MultiDisplayActivator : MonoBehaviour
    {
        public int additionalDisplays = 2;

        static bool _activated;

        void Awake()
        {
            if (_activated) return;

            int lastDisplay = Mathf.Min(additionalDisplays, Display.displays.Length - 1);
            for (int i = 1; i <= lastDisplay; i++)
            {
                Display.displays[i].Activate();
            }

            _activated = true;
        }
    }
}
