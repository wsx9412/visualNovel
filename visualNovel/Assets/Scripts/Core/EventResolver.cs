using UnityEngine;
using ReincarnationLog.Data;

namespace ReincarnationLog.Core
{
    public static class EventResolver
    {
        public static int GetSuccessChance(PlayerState player, EventOption option, float difficultyMultiplier)
        {
            var statValue = player.Stats.GetValue(option.check_stat);
            var alignmentValue = 0f;

            if (option.chance_alignment_modifier != null)
            {
                alignmentValue = player.Alignment.GetValue(option.chance_alignment_modifier.axis) * option.chance_alignment_modifier.weight;
            }

            var chance = option.base_chance
                         + statValue * option.stat_weight
                         + option.skill_bonus
                         + alignmentValue;

            chance /= Mathf.Max(1f, difficultyMultiplier);
            return Mathf.Clamp(Mathf.RoundToInt(chance), 5, 95);
        }

        public static bool IsOptionUnlocked(PlayerState player, EventOption option)
        {
            if (option.alignment_requirement == null)
            {
                return true;
            }

            var value = player.Alignment.GetValue(option.alignment_requirement.axis);
            return value >= option.alignment_requirement.min;
        }

        public static bool TryOption(PlayerState player, EventOption option, float difficultyMultiplier, out int chance)
        {
            chance = GetSuccessChance(player, option, difficultyMultiplier);
            return Random.Range(0, 100) < chance;
        }

        public static void ApplyOutcome(PlayerState player, OptionOutcome outcome)
        {
            if (outcome == null)
            {
                return;
            }

            player.Hp += outcome.hp;
            player.Satiety += outcome.satiety;
            player.Experience += outcome.exp;

            if (outcome.stat_deltas != null)
            {
                foreach (var statDelta in outcome.stat_deltas)
                {
                    player.Stats.Add(statDelta.stat, statDelta.amount);
                }
            }

            if (outcome.alignment_deltas != null)
            {
                foreach (var alignmentDelta in outcome.alignment_deltas)
                {
                    player.Alignment.Add(alignmentDelta.axis, alignmentDelta.amount);
                }
            }

            if (!string.IsNullOrWhiteSpace(outcome.add_item))
            {
                player.Items.Add(outcome.add_item);
            }

            if (!string.IsNullOrWhiteSpace(outcome.add_skill))
            {
                player.Skills.Add(outcome.add_skill);
            }

            player.Hp = Mathf.Clamp(player.Hp, 0, 100);
            player.Satiety = Mathf.Clamp(player.Satiety, 0, 100);
        }
    }
}
