using System.Collections.Generic;
using ReincarnationLog.Data;
using UnityEngine;
using UnityEngine.UI;

namespace ReincarnationLog.Runtime
{
    public class ReincarnationDebugUi : MonoBehaviour
    {
        private const int MaxVisibleOptions = 4;

        private ReincarnationGameManager _gameManager;
        private Text _statusText;
        private Text _eventText;
        private Text _logText;
        private readonly List<Button> _optionButtons = new();
        private readonly List<Text> _optionLabels = new();
        private IReadOnlyList<EventOption> _lastUnlockedOptions;

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
            }
        }

        private void BuildUi()
        {
            var canvasObject = new GameObject("DebugCanvas");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();

            var root = CreatePanel("RootPanel", canvas.transform, new Vector2(0.5f, 0.5f), new Vector2(0.95f, 0.95f));

            _statusText = CreateText("Status", root, "", 18, TextAnchor.UpperLeft, new Vector2(0f, 0.75f), new Vector2(1f, 1f));
            _eventText = CreateText("Event", root, "이벤트 로딩 중...", 22, TextAnchor.UpperLeft, new Vector2(0f, 0.45f), new Vector2(1f, 0.72f));
            _logText = CreateText("Log", root, "로그", 16, TextAnchor.LowerLeft, new Vector2(0f, 0f), new Vector2(1f, 0.28f));

            for (var i = 0; i < MaxVisibleOptions; i++)
            {
                var rowHeight = 0.1f;
                var yMax = 0.42f - (i * rowHeight);
                var yMin = yMax - rowHeight + 0.01f;
                var button = CreateButton(root, $"OptionButton_{i}", new Vector2(0f, yMin), new Vector2(1f, yMax));
                var cachedIndex = i;
                button.onClick.AddListener(() => OnOptionClicked(cachedIndex));
                _optionButtons.Add(button);
                _optionLabels.Add(button.GetComponentInChildren<Text>());
            }
        }

        private void HandleEventReady(EventDefinition eventDefinition, IReadOnlyList<EventOption> unlockedOptions)
        {
            _lastUnlockedOptions = unlockedOptions;
            _eventText.text = $"[{eventDefinition.event_id}]\n{eventDefinition.text}";

            for (var i = 0; i < _optionButtons.Count; i++)
            {
                var canShow = i < unlockedOptions.Count && i < MaxVisibleOptions;
                _optionButtons[i].gameObject.SetActive(canShow);
                if (canShow)
                {
                    _optionLabels[i].text = $"{i + 1}. {unlockedOptions[i].text}";
                }
            }

            if (unlockedOptions.Count > MaxVisibleOptions)
            {
                HandleLog($"선택지는 최대 {MaxVisibleOptions}개까지 노출됩니다. ({unlockedOptions.Count}개 중 일부 숨김)");
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
            if (_lastUnlockedOptions == null || index >= _lastUnlockedOptions.Count)
            {
                return;
            }

            _gameManager.ChooseOption(_lastUnlockedOptions[index]);
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
            label.fontSize = 18;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleLeft;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;

            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(12f, 6f);
            labelRect.offsetMax = new Vector2(-12f, -6f);

            return buttonObject.GetComponent<Button>();
        }
    }
}
