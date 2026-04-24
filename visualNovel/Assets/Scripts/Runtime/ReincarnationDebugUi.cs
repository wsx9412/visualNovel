using System.Collections.Generic;
using System.Linq;
using ReincarnationLog.Data;
using ReincarnationLog.Core;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace ReincarnationLog.Runtime
{
    public class ReincarnationDebugUi : MonoBehaviour
    {
        private const int MaxVisibleOptions = 4;
        private const float OptionTop = 0.44f;
        private const float OptionBottom = 0.08f;
        private const float OptionSpacing = 0.012f;

        private ReincarnationGameManager _gameManager;
        private Text _statusText;
        private Text _eventText;
        private Text _logText;
        private readonly List<Button> _optionButtons = new();
        private readonly List<Text> _optionLabels = new();
        private readonly List<EventOption> _visibleOptions = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureDebugUi()
        {
            if (FindFirstObjectByType<ReincarnationDebugUi>() != null)
            {
                return;
            }

            var uiRoot = new GameObject("ReincarnationDebugUI");
            uiRoot.AddComponent<ReincarnationDebugUi>();
        }

        private void Awake()
        {
            _gameManager = FindFirstObjectByType<ReincarnationGameManager>();
            if (_gameManager == null)
            {
                var managerObject = new GameObject("ReincarnationGameManager");
                _gameManager = managerObject.AddComponent<ReincarnationGameManager>();
            }

            BuildUi();
        }

        private void OnEnable()
        {
            _gameManager.OnEventReady += HandleEventReady;
            _gameManager.OnLog += HandleLog;
            _gameManager.OnRunEnd += HandleRunEnd;
        }

        private void OnDisable()
        {
            _gameManager.OnEventReady -= HandleEventReady;
            _gameManager.OnLog -= HandleLog;
            _gameManager.OnRunEnd -= HandleRunEnd;
        }

        private void Start()
        {
            if (_gameManager.Player == null)
            {
                _gameManager.StartNewRun();
            }
            else
            {
                RefreshStatus();
                SyncCurrentEventIfNeeded();
            }
        }

        private void SyncCurrentEventIfNeeded()
        {
            if (_gameManager.CurrentEvent == null)
            {
                return;
            }

            var unlocked = _gameManager.CurrentEvent.options
                .Where(option => EventResolver.IsOptionUnlocked(_gameManager.Player, option))
                .ToList();
            HandleEventReady(_gameManager.CurrentEvent, unlocked);
        }

        private void BuildUi()
        {
            EnsureEventSystem();

            var canvasObject = new GameObject("DebugCanvas");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var canvasScaler = canvasObject.AddComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1080f, 1920f);
            canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            canvasScaler.matchWidthOrHeight = 1f;
            canvasObject.AddComponent<GraphicRaycaster>();

            var root = CreatePanel("RootPanel", canvas.transform, new Vector2(0.5f, 0.5f), new Vector2(0.95f, 0.95f));

            _statusText = CreateText("Status", root, "", 36, TextAnchor.UpperLeft, new Vector2(0f, 0.75f), new Vector2(1f, 1f));
            _eventText = CreateText("Event", root, "이벤트 로딩 중...", 42, TextAnchor.UpperLeft, new Vector2(0f, 0.48f), new Vector2(1f, 0.72f));
            _logText = CreateText("Log", root, "로그", 30, TextAnchor.LowerLeft, new Vector2(0f, 0f), new Vector2(1f, 0.28f));

            var rowHeight = (OptionTop - OptionBottom - OptionSpacing * (MaxVisibleOptions - 1)) / MaxVisibleOptions;
            for (var i = 0; i < MaxVisibleOptions; i++)
            {
                var yMax = OptionTop - (rowHeight + OptionSpacing) * i;
                var yMin = yMax - rowHeight;
                var button = CreateButton(root, $"OptionButton_{i}", new Vector2(0f, yMin), new Vector2(1f, yMax));
                var cachedIndex = i;
                button.onClick.AddListener(() => OnOptionClicked(cachedIndex));
                _optionButtons.Add(button);
                _optionLabels.Add(button.GetComponentInChildren<Text>());
            }
        }

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            var eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
#else
            eventSystemObject.AddComponent<StandaloneInputModule>();
#endif
        }

        private void HandleEventReady(EventDefinition eventDefinition, IReadOnlyList<EventOption> unlockedOptions)
        {
            _visibleOptions.Clear();
            _eventText.text = $"[{eventDefinition.event_id}]\n{eventDefinition.text}";
            var orderedOptions = unlockedOptions
                .Concat(eventDefinition.options.Where(option => !unlockedOptions.Contains(option)))
                .Take(MaxVisibleOptions)
                .ToList();

            for (var i = 0; i < _optionButtons.Count; i++)
            {
                var canShow = i < orderedOptions.Count;
                _optionButtons[i].gameObject.SetActive(canShow);
                if (canShow)
                {
                    var option = orderedOptions[i];
                    var isUnlocked = unlockedOptions.Contains(option);
                    _visibleOptions.Add(option);
                    _optionButtons[i].interactable = isUnlocked;
                    _optionLabels[i].text = isUnlocked
                        ? $"{i + 1}. {option.text}"
                        : $"{i + 1}. {option.text} (잠김)";
                }
            }

            if (eventDefinition.options.Count > MaxVisibleOptions)
            {
                HandleLog($"선택지는 최대 {MaxVisibleOptions}개까지 노출됩니다. ({eventDefinition.options.Count}개 중 일부 숨김)");
            }

            if (unlockedOptions.Count == 0)
            {
                HandleLog("현재 조건으로 선택 가능한 옵션이 없습니다. (잠긴 선택지만 표시됨)");
            }

            RefreshStatus();
        }

        private void HandleLog(string message)
        {
            _logText.text = $"로그\n{message}";
            RefreshStatus();
        }

        private void HandleRunEnd(bool reachedEnding)
        {
            HandleLog(reachedEnding ? "엔딩 도달! 새 게임을 시작합니다." : "사망했습니다. 새 게임을 시작합니다.");
            _gameManager.StartNewRun();
        }

        private void OnOptionClicked(int index)
        {
            if (index < 0 || index >= _visibleOptions.Count)
            {
                return;
            }

            var option = _visibleOptions[index];
            if (!EventResolver.IsOptionUnlocked(_gameManager.Player, option))
            {
                HandleLog("해당 선택지는 조건이 맞지 않아 선택할 수 없습니다.");
                return;
            }

            _gameManager.ChooseOption(option);
        }

        private void RefreshStatus()
        {
            if (_gameManager.Player == null)
            {
                _statusText.text = "플레이어 정보 없음";
                return;
            }

            var player = _gameManager.Player;
            _statusText.text =
                $"Day {player.Day}  HP {player.Hp}  포만감 {player.Satiety}  EXP {player.Experience}\n" +
                $"STR {player.Stats.Strength} DEX {player.Stats.Dexterity} INT {player.Stats.Intelligence} LUK {player.Stats.Luck}";
        }

        private static RectTransform CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var panelObject = new GameObject(name, typeof(Image));
            panelObject.transform.SetParent(parent, false);
            var rect = panelObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            panelObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
            return rect;
        }

        private static Text CreateText(
            string name,
            Transform parent,
            string content,
            int fontSize,
            TextAnchor alignment,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            var textObject = new GameObject(name, typeof(Text));
            textObject.transform.SetParent(parent, false);
            var text = textObject.GetComponent<Text>();
            text.text = content;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            var rect = text.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = new Vector2(8f, 8f);
            rect.offsetMax = new Vector2(-8f, -8f);
            return text;
        }

        private static Button CreateButton(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
        {
            var buttonObject = new GameObject(name, typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            var image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.15f, 0.25f, 0.55f, 0.95f);

            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = new Vector2(8f, 4f);
            rect.offsetMax = new Vector2(-8f, -4f);

            var labelObject = new GameObject("Label", typeof(Text));
            labelObject.transform.SetParent(buttonObject.transform, false);
            var label = labelObject.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 28;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleLeft;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 18;
            label.resizeTextMaxSize = 28;

            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(12f, 6f);
            labelRect.offsetMax = new Vector2(-12f, -6f);

            return buttonObject.GetComponent<Button>();
        }
    }
}
