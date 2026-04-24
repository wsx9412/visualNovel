using System.Collections.Generic;
using System.Linq;
using ReincarnationLog.Core;
using ReincarnationLog.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace ReincarnationLog.Runtime
{
    public class ReincarnationDebugUi : MonoBehaviour
    {
        private const int MaxVisibleOptions = 4;

        private ReincarnationGameManager _gameManager;
        private Text _statusText;
        private Image _backgroundImage;
        private ScrollRect _storyScrollRect;
        private RectTransform _storyContent;
        private RectTransform _choicesContainer;
        private readonly List<Button> _optionButtons = new();
        private readonly List<Text> _optionLabels = new();
        private readonly List<EventOption> _visibleOptions = new();

        private bool _stickToBottom = true;

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

            _backgroundImage = CreateImage("Background", canvas.transform, new Color(0.16f, 0.16f, 0.18f, 1f), Vector2.zero, Vector2.one);
            _backgroundImage.preserveAspect = true;

            var dimLayer = CreateImage("Dim", canvas.transform, new Color(0f, 0f, 0f, 0.35f), Vector2.zero, Vector2.one);
            dimLayer.raycastTarget = false;

            var root = CreatePanel("Root", canvas.transform, new Color(0f, 0f, 0f, 0.12f), Vector2.zero, Vector2.one);

            _statusText = CreateText("Status", root, "로딩 중...", 30, TextAnchor.UpperLeft, new Vector2(0.04f, 0.88f), new Vector2(0.96f, 0.98f));

            _storyScrollRect = CreateStoryScroll(root, out _storyContent);
            _storyScrollRect.onValueChanged.AddListener(_ =>
            {
                _stickToBottom = _storyScrollRect.verticalNormalizedPosition <= 0.05f;
            });

            _choicesContainer = CreateChoicesContainer(root);
            for (var i = 0; i < MaxVisibleOptions; i++)
            {
                var button = CreateChoiceButton(_choicesContainer, i);
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
            _choicesContainer.gameObject.SetActive(true);
            ApplyBackground(eventDefinition.event_id);
            AppendStory($"[{eventDefinition.event_id}]\n{eventDefinition.text}", true);

            _visibleOptions.Clear();
            var orderedOptions = unlockedOptions
                .Concat(eventDefinition.options.Where(option => !unlockedOptions.Contains(option)))
                .Take(MaxVisibleOptions)
                .ToList();

            for (var i = 0; i < _optionButtons.Count; i++)
            {
                var canShow = i < orderedOptions.Count;
                _optionButtons[i].gameObject.SetActive(canShow);
                if (!canShow)
                {
                    continue;
                }

                var option = orderedOptions[i];
                var isUnlocked = unlockedOptions.Contains(option);
                _visibleOptions.Add(option);
                _optionButtons[i].interactable = isUnlocked;
                _optionLabels[i].text = isUnlocked ? option.text : $"{option.text} (잠김)";
            }

            RefreshStatus();
            ScrollStoryToBottom();
        }

        private void HandleLog(string message)
        {
            AppendStory(message, false);
            RefreshStatus();
            ScrollStoryToBottom();
        }

        private void HandleRunEnd(bool reachedEnding)
        {
            AppendStory(reachedEnding ? "엔딩 도달! 새 게임을 시작합니다." : "사망했습니다. 새 게임을 시작합니다.", false);
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
                AppendStory("해당 선택지는 조건이 맞지 않아 선택할 수 없습니다.", false);
                return;
            }

            AppendStory($"선택: {option.text}", false);
            _choicesContainer.gameObject.SetActive(false);
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
            var legacyPoint = _gameManager.Legacy?.LegacyPoint ?? 0;
            _statusText.text =
                $"Day {player.Day}  HP {player.Hp}  포만감 {player.Satiety}  EXP {player.Experience}  업적 {legacyPoint}\n" +
                $"STR {player.Stats.Strength}  DEX {player.Stats.Dexterity}  INT {player.Stats.Intelligence}  LUK {player.Stats.Luck}";
        }

        private void AppendStory(string text, bool highlighted)
        {
            var entryObject = new GameObject(highlighted ? "StoryEvent" : "StoryLog", typeof(Text));
            entryObject.transform.SetParent(_storyContent, false);

            var entryText = entryObject.GetComponent<Text>();
            entryText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            entryText.fontSize = highlighted ? 44 : 34;
            entryText.alignment = TextAnchor.UpperLeft;
            entryText.color = Color.white;
            entryText.horizontalOverflow = HorizontalWrapMode.Wrap;
            entryText.verticalOverflow = VerticalWrapMode.Overflow;
            entryText.text = text;

            var layout = entryObject.AddComponent<LayoutElement>();
            layout.minHeight = highlighted ? 150f : 90f;

            var rect = entryObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0f, layout.minHeight);
        }

        private void ScrollStoryToBottom()
        {
            if (!_stickToBottom)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();
            _storyScrollRect.verticalNormalizedPosition = 0f;
        }

        private void ApplyBackground(string eventId)
        {
            var sprite = Resources.Load<Sprite>($"Backgrounds/{eventId}") ?? Resources.Load<Sprite>("Backgrounds/default");
            _backgroundImage.sprite = sprite;
            _backgroundImage.color = sprite == null ? new Color(0.16f, 0.16f, 0.18f, 1f) : Color.white;
        }

        private static Image CreateImage(string name, Transform parent, Color color, Vector2 anchorMin, Vector2 anchorMax)
        {
            var imageObject = new GameObject(name, typeof(Image));
            imageObject.transform.SetParent(parent, false);
            var image = imageObject.GetComponent<Image>();
            image.color = color;

            var rect = image.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return image;
        }

        private static RectTransform CreatePanel(string name, Transform parent, Color color, Vector2 anchorMin, Vector2 anchorMax)
        {
            var panel = CreateImage(name, parent, color, anchorMin, anchorMax);
            return panel.rectTransform;
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
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return text;
        }

        private static ScrollRect CreateStoryScroll(Transform parent, out RectTransform content)
        {
            var scrollObject = new GameObject("StoryScroll", typeof(Image), typeof(Mask), typeof(ScrollRect));
            scrollObject.transform.SetParent(parent, false);
            var viewportImage = scrollObject.GetComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0.2f);
            scrollObject.GetComponent<Mask>().showMaskGraphic = false;

            var scrollRect = scrollObject.GetComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 28f;

            var viewportRect = scrollObject.GetComponent<RectTransform>();
            viewportRect.anchorMin = new Vector2(0.04f, 0.20f);
            viewportRect.anchorMax = new Vector2(0.96f, 0.86f);
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;

            var contentObject = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentObject.transform.SetParent(scrollObject.transform, false);
            content = contentObject.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = new Vector2(18f, 0f);
            content.offsetMax = new Vector2(-18f, 0f);

            var layout = contentObject.GetComponent<VerticalLayoutGroup>();
            layout.childForceExpandHeight = false;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.spacing = 20f;
            layout.padding = new RectOffset(20, 20, 20, 20);

            var fitter = contentObject.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewportRect;
            scrollRect.content = content;
            return scrollRect;
        }

        private static RectTransform CreateChoicesContainer(Transform parent)
        {
            var panelObject = new GameObject("Choices", typeof(Image), typeof(VerticalLayoutGroup));
            panelObject.transform.SetParent(parent, false);

            var panelImage = panelObject.GetComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0.35f);

            var panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.04f, 0.02f);
            panelRect.anchorMax = new Vector2(0.96f, 0.18f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            var layout = panelObject.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 14f;
            layout.padding = new RectOffset(12, 12, 12, 12);
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = true;

            return panelRect;
        }

        private static Button CreateChoiceButton(Transform parent, int index)
        {
            var buttonObject = new GameObject($"OptionButton_{index}", typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);

            var image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.13f, 0.2f, 0.38f, 0.95f);

            var layout = buttonObject.GetComponent<LayoutElement>();
            layout.minHeight = 88f;

            var labelObject = new GameObject("Label", typeof(Text));
            labelObject.transform.SetParent(buttonObject.transform, false);
            var label = labelObject.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 32;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleLeft;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;

            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(24f, 8f);
            labelRect.offsetMax = new Vector2(-24f, -8f);

            return buttonObject.GetComponent<Button>();
        }
    }
}
