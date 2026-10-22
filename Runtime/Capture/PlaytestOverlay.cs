using System;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace PlaytestNotes
{
    /// <summary>
    /// The recording and review overlay, built in code with uGUI so the package ships no UI assets.
    /// Large buttons for touch; keyboard and gamepad go through the settings' actions, with hints on the buttons.
    /// Lays itself out inside the screen's safe area.
    /// </summary>
    internal sealed class PlaytestOverlay : MonoBehaviour
    {
        private const float ToastSeconds = 2.5f;
        private const int LabelSize = 40;
        private const int HintSize = 24;

        private static readonly Color PanelColor = new Color(0.07f, 0.07f, 0.09f, 0.9f);
        private static readonly Color ButtonColor = new Color(0.24f, 0.24f, 0.28f, 1f);
        private static readonly Color SubmitColor = new Color(0.15f, 0.5f, 0.3f, 1f);
        private static readonly Color DeleteColor = new Color(0.62f, 0.2f, 0.2f, 1f);
        private static readonly Color RecordColor = new Color(0.95f, 0.2f, 0.2f, 1f);
        private static readonly Color SelectedColor = new Color(0.96f, 0.76f, 0.22f, 1f);
        private static readonly Color DarkText = new Color(0.1f, 0.1f, 0.1f, 1f);

        private static readonly NoteCategory[] Categories = (NoteCategory[])Enum.GetValues(typeof(NoteCategory));

        private PlaytestNotesSettings settings;
        private Font font;
        private Canvas canvas;
        private RectTransform safeArea;
        private GameObject recordingPanel;
        private GameObject reviewPanel;
        private GameObject toastPanel;
        private Text recordingLabel;
        private Image recordingDot;
        private Button stopButton;
        private Text stopText;
        private Text reviewLabel;
        private Text statusLabel;
        private Text toastLabel;
        private Button rerecordButton;
        private Text submitText;
        private Text rerecordText;
        private Text deleteText;
        private Text categoryHint;
        private Image[] categoryImages;
        private Text[] categoryTexts;
        private GameObject ownedEventSystem;
        private Rect appliedSafeArea;
        private int shownSeconds = -1;
        private int maxSeconds;
        private float toastHideTime;

        public event Action StopPressed;

        public event Action SubmitPressed;

        public event Action RerecordPressed;

        public event Action DeletePressed;

        public event Action<NoteCategory> CategoryPressed;

        public static PlaytestOverlay Create(Transform parent, PlaytestNotesSettings settings)
        {
            var root = new GameObject("Playtest Notes Overlay", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            PlaytestOverlay overlay = root.AddComponent<PlaytestOverlay>();
            overlay.Build(settings);
            return overlay;
        }

        public void ShowWaiting(string message)
        {
            Show();
            recordingPanel.SetActive(true);
            reviewPanel.SetActive(false);
            stopButton.gameObject.SetActive(false);
            recordingDot.enabled = false;
            recordingLabel.text = message;
        }

        public void ShowRecording(int limitSeconds)
        {
            Show();
            maxSeconds = limitSeconds;
            shownSeconds = -1;
            recordingPanel.SetActive(true);
            reviewPanel.SetActive(false);
            stopButton.gameObject.SetActive(true);
            recordingDot.enabled = true;
            stopText.text = Label("Stop", settings.StopAction);
            SetElapsed(0f);
        }

        public void SetElapsed(float seconds)
        {
            recordingDot.color = new Color(RecordColor.r, RecordColor.g, RecordColor.b, 0.35f + (0.65f * Mathf.PingPong(Time.unscaledTime * 1.5f, 1f)));
            int whole = (int)seconds;
            if (whole == shownSeconds)
            {
                return;
            }

            shownSeconds = whole;
            recordingLabel.text = "Recording  " + Clock(whole) + " / " + Clock(maxSeconds);
        }

        public void ShowReview(NoteCategory category, RecordedAudio audio, bool canRerecord, string status)
        {
            Show();
            recordingPanel.SetActive(false);
            reviewPanel.SetActive(true);
            reviewLabel.text = audio != null ? "Voice note " + Clock(Mathf.RoundToInt(audio.Seconds)) : "Screenshot note";
            statusLabel.text = status ?? string.Empty;
            statusLabel.gameObject.SetActive(!string.IsNullOrEmpty(status));
            rerecordButton.gameObject.SetActive(canRerecord);
            submitText.text = Label("Submit", settings.SubmitAction);
            rerecordText.text = Label("Re-record", settings.RerecordAction);
            deleteText.text = Label("Delete", settings.DeleteAction);
            string hint = Hint(settings.NextCategoryAction);
            categoryHint.text = hint.Length > 0 ? "Category  ·  " + hint + " to change" : "Category";
            SetCategory(category);
        }

        public void SetCategory(NoteCategory category)
        {
            for (int i = 0; i < Categories.Length; i++)
            {
                bool selected = Categories[i] == category;
                categoryImages[i].color = selected ? SelectedColor : ButtonColor;
                categoryTexts[i].color = selected ? DarkText : Color.white;
            }
        }

        public void Hide()
        {
            recordingPanel.SetActive(false);
            reviewPanel.SetActive(false);
            if (ownedEventSystem != null)
            {
                Destroy(ownedEventSystem);
                ownedEventSystem = null;
            }

            canvas.enabled = toastPanel.activeSelf;
        }

        public void Toast(string message)
        {
            toastLabel.text = message;
            toastPanel.SetActive(true);
            toastHideTime = Time.unscaledTime + ToastSeconds;
            canvas.enabled = true;
            FitSafeArea();
        }

        private static string Clock(int seconds)
        {
            return (seconds / 60) + ":" + (seconds % 60).ToString("00");
        }

        private static string Label(string text, InputAction action)
        {
            string hint = Hint(action);
            return hint.Length > 0 ? text + "\n<size=" + HintSize + ">" + hint + "</size>" : text;
        }

        // The first keyboard and first gamepad binding, shown only for devices that are connected.
        private static string Hint(InputAction action)
        {
            string keyboard = null;
            string gamepad = null;
            var bindings = action.bindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                InputBinding binding = bindings[i];
                if (binding.isPartOfComposite)
                {
                    continue;
                }

                string path = binding.isComposite && i + 1 < bindings.Count ? bindings[i + 1].effectivePath : binding.effectivePath;
                if (keyboard == null && Keyboard.current != null && path.StartsWith("<Keyboard>", StringComparison.Ordinal))
                {
                    keyboard = action.GetBindingDisplayString(i);
                }
                else if (gamepad == null && Gamepad.current != null && path.StartsWith("<Gamepad>", StringComparison.Ordinal))
                {
                    gamepad = action.GetBindingDisplayString(i);
                }
            }

            var builder = new StringBuilder();
            if (!string.IsNullOrEmpty(keyboard))
            {
                builder.Append(keyboard);
            }

            if (!string.IsNullOrEmpty(gamepad))
            {
                builder.Append(builder.Length > 0 ? "  ·  " : string.Empty).Append(gamepad);
            }

            return builder.ToString();
        }

        private void Update()
        {
            if (toastPanel.activeSelf && Time.unscaledTime >= toastHideTime)
            {
                toastPanel.SetActive(false);
                canvas.enabled = recordingPanel.activeSelf || reviewPanel.activeSelf;
            }

            if (canvas.enabled)
            {
                FitSafeArea();
            }
        }

        private void Show()
        {
            canvas.enabled = true;
            FitSafeArea();
            if (EventSystem.current == null && ownedEventSystem == null)
            {
                ownedEventSystem = new GameObject("Playtest Notes EventSystem", typeof(EventSystem));
                ownedEventSystem.transform.SetParent(transform, false);
                ownedEventSystem.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
        }

        private void FitSafeArea()
        {
            Rect area = Screen.safeArea;
            if (area == appliedSafeArea || Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            appliedSafeArea = area;
            safeArea.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            safeArea.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
        }

        private void Build(PlaytestNotesSettings notesSettings)
        {
            settings = notesSettings;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            gameObject.AddComponent<GraphicRaycaster>();

            safeArea = CreateRect("Safe Area", transform);
            Stretch(safeArea);

            BuildRecordingPanel();
            BuildReviewPanel();
            BuildToast();

            recordingPanel.SetActive(false);
            reviewPanel.SetActive(false);
            toastPanel.SetActive(false);
            canvas.enabled = false;
        }

        private void BuildRecordingPanel()
        {
            RectTransform panel = CreatePanel("Recording", safeArea, new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(900f, 130f));
            recordingPanel = panel.gameObject;
            HorizontalLayoutGroup row = panel.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(28, 14, 10, 10);
            row.spacing = 22f;
            row.childAlignment = TextAnchor.MiddleLeft;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;

            recordingDot = CreateRect("Dot", panel).gameObject.AddComponent<Image>();
            recordingDot.color = RecordColor;
            SetSize(recordingDot.gameObject, 34f, 34f, 0f);

            recordingLabel = CreateText("Label", panel, LabelSize, TextAnchor.MiddleLeft, Color.white);
            SetSize(recordingLabel.gameObject, 0f, 110f, 1f);

            stopButton = CreateButton("Stop", panel, RecordColor, () => StopPressed?.Invoke(), out stopText);
            SetSize(stopButton.gameObject, 260f, 110f, 0f);
        }

        private void BuildReviewPanel()
        {
            RectTransform panel = CreatePanel("Review", safeArea, new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(1360f, 430f));
            reviewPanel = panel.gameObject;
            VerticalLayoutGroup column = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(28, 28, 20, 24);
            column.spacing = 12f;
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;

            reviewLabel = CreateText("Title", panel, LabelSize, TextAnchor.MiddleLeft, Color.white);
            SetSize(reviewLabel.gameObject, 0f, 52f, 0f);
            statusLabel = CreateText("Status", panel, 28, TextAnchor.MiddleLeft, SelectedColor);
            SetSize(statusLabel.gameObject, 0f, 36f, 0f);
            categoryHint = CreateText("Category Hint", panel, HintSize + 4, TextAnchor.MiddleLeft, new Color(1f, 1f, 1f, 0.7f));
            SetSize(categoryHint.gameObject, 0f, 34f, 0f);

            RectTransform categories = CreateRow("Categories", panel, 90f);
            categoryImages = new Image[Categories.Length];
            categoryTexts = new Text[Categories.Length];
            for (int i = 0; i < Categories.Length; i++)
            {
                NoteCategory category = Categories[i];
                string label = category == NoteCategory.Uncategorized ? "None" : category.ToString();
                Button chip = CreateButton(label, categories, ButtonColor, () => CategoryPressed?.Invoke(category), out Text chipText);
                chipText.text = label;
                SetSize(chip.gameObject, 0f, 90f, 1f);
                categoryImages[i] = chip.image;
                categoryTexts[i] = chipText;
            }

            RectTransform actions = CreateRow("Actions", panel, 120f);
            Button submit = CreateButton("Submit", actions, SubmitColor, () => SubmitPressed?.Invoke(), out submitText);
            SetSize(submit.gameObject, 0f, 120f, 1.4f);
            rerecordButton = CreateButton("Re-record", actions, ButtonColor, () => RerecordPressed?.Invoke(), out rerecordText);
            SetSize(rerecordButton.gameObject, 0f, 120f, 1f);
            Button delete = CreateButton("Delete", actions, DeleteColor, () => DeletePressed?.Invoke(), out deleteText);
            SetSize(delete.gameObject, 0f, 120f, 1f);
        }

        private void BuildToast()
        {
            RectTransform panel = CreatePanel("Toast", safeArea, new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(1000f, 80f));
            toastPanel = panel.gameObject;
            toastLabel = CreateText("Label", panel, 30, TextAnchor.MiddleCenter, Color.white);
            Stretch(toastLabel.rectTransform);
        }

        private RectTransform CreateRow(string name, Transform parent, float height)
        {
            RectTransform row = CreateRect(name, parent);
            HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 14f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            SetSize(row.gameObject, 0f, height, 0f);
            return row;
        }

        private RectTransform CreatePanel(string name, Transform parent, Vector2 anchor, Vector2 offset, Vector2 size)
        {
            RectTransform panel = CreateRect(name, parent);
            panel.anchorMin = anchor;
            panel.anchorMax = anchor;
            panel.pivot = anchor;
            panel.anchoredPosition = offset;
            panel.sizeDelta = size;
            panel.gameObject.AddComponent<Image>().color = PanelColor;
            return panel;
        }

        private Button CreateButton(string name, Transform parent, Color color, Action onClick, out Text label)
        {
            RectTransform rect = CreateRect(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => onClick());
            label = CreateText("Label", rect, 34, TextAnchor.MiddleCenter, Color.white);
            label.text = name;
            Stretch(label.rectTransform);
            return button;
        }

        private Text CreateText(string name, Transform parent, int size, TextAnchor alignment, Color color)
        {
            Text text = CreateRect(name, parent).gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = color;
            text.supportRichText = true;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetSize(GameObject go, float width, float height, float flexibleWidth)
        {
            LayoutElement element = go.AddComponent<LayoutElement>();
            if (width > 0f)
            {
                element.minWidth = width;
                element.preferredWidth = width;
            }

            element.minHeight = height;
            element.preferredHeight = height;
            element.flexibleWidth = flexibleWidth;
        }
    }
}
