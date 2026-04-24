using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ReincarnationLog.Core;
using ReincarnationLog.Data;

namespace ReincarnationLog.Runtime
{
    public class ReincarnationGameManager : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private TextAsset eventJson;
        [SerializeField] private int endingDay = 20;

        [Header("Difficulty")]
        [SerializeField] private AnimationCurve difficultyByDay = AnimationCurve.Linear(1, 1f, 20, 2f);

        private SaveService _saveService;
        private IAdReviveService _adReviveService;
        private EventCatalog _catalog;

        public PlayerState Player { get; private set; }
        public LegacyState Legacy { get; private set; }
        public EventDefinition CurrentEvent { get; private set; }

        public event Action<EventDefinition, IReadOnlyList<EventOption>> OnEventReady;
        public event Action<string> OnLog;
        public event Action<bool> OnRunEnd;

        private void Awake()
        {
            _saveService = new SaveService();
            _adReviveService = new DebugAdReviveService();

            Legacy = _saveService.LoadLegacy();
            Player = _saveService.LoadRun();

            LoadCatalog();

            if (CurrentEvent == null)
            {
                NextEvent();
            }
        }

        public void StartNewRun(int bonusHp = 0)
        {
            Player = new PlayerState
            {
                Hp = Mathf.Clamp(100 + bonusHp, 1, 150),
                Satiety = 100
            };

            ApplyLegacyBonus();
            _saveService.SaveRun(Player);
            NextEvent();
        }

        public void ChooseOption(int optionIndex)
        {
            if (CurrentEvent == null || optionIndex < 0 || optionIndex >= CurrentEvent.options.Count)
            {
                return;
            }

            ExecuteOption(CurrentEvent.options[optionIndex]);
        }

        public void ChooseOption(EventOption option)
        {
            if (CurrentEvent == null || option == null)
            {
                return;
            }

            if (!CurrentEvent.options.Contains(option))
            {
                OnLog?.Invoke("현재 이벤트에 존재하지 않는 선택지입니다.");
                return;
            }

            ExecuteOption(option);
        }

        private void ExecuteOption(EventOption option)
        {
            if (!EventResolver.IsOptionUnlocked(Player, option))
            {
                OnLog?.Invoke("조건이 맞지 않아 선택할 수 없습니다.");
                return;
            }

            var success = EventResolver.TryOption(Player, option, CurrentDifficulty(), out var chance);
            var result = success ? option.on_success : option.on_fail;
            EventResolver.ApplyOutcome(Player, result);

            var summary = success ? "성공" : "실패";
            OnLog?.Invoke($"[{CurrentEvent.event_id}] {summary} ({chance}% 확률)");
            if (!string.IsNullOrWhiteSpace(result?.log))
            {
                OnLog?.Invoke(result.log);
            }

            PostTurn();
        }

        public void BuyLegacyUpgrade(LegacyUpgradeType upgradeType)
        {
            var cost = LegacyShop.GetCost(upgradeType);
            if (Legacy.LegacyPoint < cost)
            {
                OnLog?.Invoke("업적 포인트가 부족합니다.");
                return;
            }

            Legacy.LegacyPoint -= cost;
            LegacyShop.ApplyUpgrade(Legacy, upgradeType);
            _saveService.SaveLegacy(Legacy);
            OnLog?.Invoke($"{upgradeType} 업그레이드 구매 완료");
        }

        private void PostTurn()
        {
            Player.Day += 1;
            Player.Satiety = Mathf.Max(0, Player.Satiety - 5);

            if (Player.Hp <= 0 || Player.Satiety <= 0)
            {
                TryReviveOrEnd();
                return;
            }

            if (Player.Day > endingDay)
            {
                EndRun(true);
                return;
            }

            _saveService.SaveRun(Player);
            NextEvent();
        }

        private void TryReviveOrEnd()
        {
            if (Player.UsedReviveInRun)
            {
                EndRun(false);
                return;
            }

            _adReviveService.ShowReviveAd(success =>
            {
                if (!success)
                {
                    EndRun(false);
                    return;
                }

                Player.UsedReviveInRun = true;
                Player.Hp = Mathf.Max(Player.Hp, 20);
                Player.Satiety = Mathf.Max(Player.Satiety, 20);
                OnLog?.Invoke("기적적으로 위기를 넘겼습니다...");

                _saveService.SaveRun(Player);
                NextEvent();
            });
        }

        private void EndRun(bool reachedEnding)
        {
            var reward = CalculateLegacyPoints(reachedEnding);
            Legacy.LegacyPoint += reward;
            Legacy.Runs += 1;
            Legacy.MaxDaySurvived = Mathf.Max(Legacy.MaxDaySurvived, Player.Day);

            _saveService.SaveLegacy(Legacy);
            _saveService.ClearRun();

            OnLog?.Invoke(reachedEnding
                ? $"엔딩 도달! 업적 포인트 +{reward}"
                : $"사망... 업적 포인트 +{reward}");
            OnRunEnd?.Invoke(reachedEnding);
        }

        private int CalculateLegacyPoints(bool reachedEnding)
        {
            var days = Mathf.Max(1, Player.Day);
            var bonus = reachedEnding ? 20 : 0;
            var itemScore = Player.Items.Count * 2;
            var skillScore = Player.Skills.Count * 3;
            return days + bonus + itemScore + skillScore;
        }

        private float CurrentDifficulty()
        {
            return difficultyByDay.Evaluate(Player.Day);
        }

        private void NextEvent()
        {
            var pool = _catalog.events
                .Where(e => Player.Stage >= e.min_stage && Player.Stage <= e.max_stage)
                .ToList();

            if (pool.Count == 0)
            {
                OnLog?.Invoke("이벤트 풀이 비어 있습니다. run 종료 처리합니다.");
                EndRun(false);
                return;
            }

            CurrentEvent = pool[UnityEngine.Random.Range(0, pool.Count)];
            var unlocked = CurrentEvent.options.Where(option => EventResolver.IsOptionUnlocked(Player, option)).ToList();
            OnEventReady?.Invoke(CurrentEvent, unlocked);
        }

        private void ApplyLegacyBonus()
        {
            if (LegacyShop.HasUnlocked(Legacy, LegacyUpgradeType.BonusStrength))
            {
                Player.Stats.Strength += 2;
            }

            if (LegacyShop.HasUnlocked(Legacy, LegacyUpgradeType.BonusDexterity))
            {
                Player.Stats.Dexterity += 2;
            }

            if (LegacyShop.HasUnlocked(Legacy, LegacyUpgradeType.StartingRation))
            {
                Player.Items.Add("비상 식량");
            }
        }

        private void LoadCatalog()
        {
            if (eventJson == null)
            {
                eventJson = Resources.Load<TextAsset>("events");
            }

            if (eventJson == null)
            {
                Debug.LogError("events.json 텍스트 에셋이 없습니다.");
                _catalog = new EventCatalog();
                return;
            }

            _catalog = JsonUtility.FromJson<EventCatalog>(eventJson.text);
            _catalog ??= new EventCatalog();
        }
    }
}
