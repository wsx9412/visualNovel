using ReincarnationLog.Data;

namespace ReincarnationLog.Core
{
    public enum LegacyUpgradeType
    {
        BonusStrength,
        BonusDexterity,
        StartingRation
    }

    public static class LegacyShop
    {
        public static int GetCost(LegacyUpgradeType type)
        {
            return type switch
            {
                LegacyUpgradeType.BonusStrength => 20,
                LegacyUpgradeType.BonusDexterity => 20,
                LegacyUpgradeType.StartingRation => 10,
                _ => 999
            };
        }

        public static void ApplyUpgrade(LegacyState legacy, LegacyUpgradeType type)
        {
            var key = type.ToString();
            if (!legacy.UnlockedSkills.Contains(key))
            {
                legacy.UnlockedSkills.Add(key);
            }
        }

        public static bool HasUnlocked(LegacyState legacy, LegacyUpgradeType type)
        {
            return legacy.UnlockedSkills.Contains(type.ToString());
        }
    }
}
