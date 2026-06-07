using UnityEngine;
using Curling.Core;
using Curling.Serialization;

namespace Curling.Match
{
    [RequireComponent(typeof(MatchManager))]
    public class MatchAutoStart : MonoBehaviour
    {
        public bool useDefaultSettings = true;
        public CpuDifficulty difficultyOverride = CpuDifficulty.Hard;
        public byte endCountOverride = 2;

        void Start()
        {
            var settings = Dc4Json.LoadDefault();
            if (!useDefaultSettings)
            {
                settings.cpu_difficulty = difficultyOverride;
                settings.standard_end_count = endCountOverride;
            }
            else
            {
                settings.cpu_difficulty = difficultyOverride;
                settings.standard_end_count = endCountOverride;
            }
            settings.FillDefaultSkills();

            var mgr = GetComponent<MatchManager>();
            mgr.StartNewMatch(settings);
            Debug.Log($"[Curling] Match started. End count={settings.standard_end_count}, CPU={settings.cpu_difficulty}");
        }
    }
}
