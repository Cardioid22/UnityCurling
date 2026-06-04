using System.IO;
using UnityEngine;
using Curling.Core;

namespace Curling.Serialization
{
    public static class Dc4Json
    {
        public static string Serialize(MatchState state) => JsonUtility.ToJson(state, true);
        public static MatchState DeserializeMatchState(string json) => JsonUtility.FromJson<MatchState>(json);

        public static string SerializeSettings(MatchSettings s) => JsonUtility.ToJson(s, true);
        public static MatchSettings DeserializeSettings(string json) => JsonUtility.FromJson<MatchSettings>(json);

        public static MatchSettings LoadDefault()
        {
            var ta = Resources.Load<TextAsset>("config/default_match");
            if (ta == null)
            {
                var s = new MatchSettings();
                s.FillDefaultSkills();
                return s;
            }
            var settings = DeserializeSettings(ta.text);
            settings.FillDefaultSkills();
            return settings;
        }

        public static void WriteToFile(MatchState state, string absolutePath)
        {
            File.WriteAllText(absolutePath, Serialize(state));
        }
    }
}
