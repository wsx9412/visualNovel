using UnityEngine;
using ReincarnationLog.Data;

namespace ReincarnationLog.Runtime
{
    public class SaveService
    {
        private const string LegacyKey = "legacy_state";
        private const string RunKey = "run_state";

        public LegacyState LoadLegacy()
        {
            if (!PlayerPrefs.HasKey(LegacyKey))
            {
                return new LegacyState();
            }

            return JsonUtility.FromJson<LegacyState>(PlayerPrefs.GetString(LegacyKey));
        }

        public PlayerState LoadRun()
        {
            if (!PlayerPrefs.HasKey(RunKey))
            {
                return new PlayerState();
            }

            return JsonUtility.FromJson<PlayerState>(PlayerPrefs.GetString(RunKey));
        }

        public void SaveLegacy(LegacyState state)
        {
            PlayerPrefs.SetString(LegacyKey, JsonUtility.ToJson(state));
            PlayerPrefs.Save();
        }

        public void SaveRun(PlayerState state)
        {
            PlayerPrefs.SetString(RunKey, JsonUtility.ToJson(state));
            PlayerPrefs.Save();
        }

        public void ClearRun()
        {
            PlayerPrefs.DeleteKey(RunKey);
        }
    }
}
