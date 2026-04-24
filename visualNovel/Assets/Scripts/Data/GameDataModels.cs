using System;
using System.Collections.Generic;
using UnityEngine;

namespace ReincarnationLog.Data
{
    public enum StatType
    {
        Strength,
        Dexterity,
        Intelligence,
        Luck
    }

    public enum AlignmentAxis
    {
        Order,
        Good
    }

    [Serializable]
    public class PlayerStats
    {
        public int Strength = 5;
        public int Dexterity = 5;
        public int Intelligence = 5;
        public int Luck = 5;

        public int GetValue(StatType statType)
        {
            return statType switch
            {
                StatType.Strength => Strength,
                StatType.Dexterity => Dexterity,
                StatType.Intelligence => Intelligence,
                StatType.Luck => Luck,
                _ => 0
            };
        }

        public void Add(StatType statType, int amount)
        {
            switch (statType)
            {
                case StatType.Strength:
                    Strength += amount;
                    break;
                case StatType.Dexterity:
                    Dexterity += amount;
                    break;
                case StatType.Intelligence:
                    Intelligence += amount;
                    break;
                case StatType.Luck:
                    Luck += amount;
                    break;
            }
        }
    }

    [Serializable]
    public class AlignmentState
    {
        // Positive = lawful / good, Negative = chaos / evil
        public int OrderChaos;
        public int GoodEvil;

        public int GetValue(AlignmentAxis axis)
        {
            return axis == AlignmentAxis.Order ? OrderChaos : GoodEvil;
        }

        public void Add(AlignmentAxis axis, int amount)
        {
            if (axis == AlignmentAxis.Order)
            {
                OrderChaos += amount;
            }
            else
            {
                GoodEvil += amount;
            }
        }
    }

    [Serializable]
    public class PlayerState
    {
        public PlayerStats Stats = new();
        public AlignmentState Alignment = new();

        public int Hp = 100;
        public int Satiety = 100;
        public int Day = 1;
        public int Experience;
        public int Stage = 1;

        public bool UsedReviveInRun;
        public List<string> Skills = new();
        public List<string> Items = new();
    }

    [Serializable]
    public class LegacyState
    {
        public int LegacyPoint;
        public int Runs;
        public int MaxDaySurvived;
        public List<string> UnlockedSkills = new();
    }

    [Serializable]
    public class EventCatalog
    {
        public List<EventDefinition> events = new();
    }

    [Serializable]
    public class EventDefinition
    {
        public string event_id;
        [TextArea] public string text;
        public int min_stage = 1;
        public int max_stage = 999;
        public List<EventOption> options = new();
    }

    [Serializable]
    public class EventOption
    {
        public string text;
        public StatType check_stat = StatType.Luck;
        public int base_chance = 50;
        public float stat_weight = 2f;
        public int skill_bonus;

        public AlignmentRequirement alignment_requirement;
        public AlignmentModifier chance_alignment_modifier;

        public OptionOutcome on_success;
        public OptionOutcome on_fail;
    }

    [Serializable]
    public class AlignmentRequirement
    {
        public AlignmentAxis axis = AlignmentAxis.Good;
        public int min;
    }

    [Serializable]
    public class AlignmentModifier
    {
        public AlignmentAxis axis = AlignmentAxis.Good;
        public float weight = 1f;
    }

    [Serializable]
    public class OptionOutcome
    {
        public int hp;
        public int satiety;
        public int exp;

        public StatDelta[] stat_deltas = Array.Empty<StatDelta>();
        public AlignmentDelta[] alignment_deltas = Array.Empty<AlignmentDelta>();
        public string add_item;
        public string add_skill;
        public string log;
    }

    [Serializable]
    public class StatDelta
    {
        public StatType stat;
        public int amount;
    }

    [Serializable]
    public class AlignmentDelta
    {
        public AlignmentAxis axis;
        public int amount;
    }
}
