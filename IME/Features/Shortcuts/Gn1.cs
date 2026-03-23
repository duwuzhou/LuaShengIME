using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Widget;
using IME.Features.Input;
using IME.Features.Settings;
using IME.Shared.ResourceProtection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IME.Features.Shortcuts
{
    public sealed class Gn1StateChangedEventArgs : EventArgs
    {
        public Gn1StateChangedEventArgs(Gn1PanelMode panelMode, string currentCategory, IReadOnlyList<string> currentItems)
        {
            PanelMode = panelMode;
            CurrentCategory = currentCategory ?? string.Empty;
            CurrentItems = currentItems ?? Array.Empty<string>();
        }

        public Gn1PanelMode PanelMode { get; }

        public string CurrentCategory { get; }

        public IReadOnlyList<string> CurrentItems { get; }
    }

    public enum Gn1PanelMode
    {
        Home,
        Shortcuts,
        Clipboard
    }

    public class Gn1 : LinearLayout
    {
        private const int ClipboardHistoryPreviewLength = 28;

        private string _currentCategory = string.Empty;
        private List<string> _currentItems = new();
        private int _loadVersion;
        private Gn1PanelMode _panelMode = Gn1PanelMode.Home;

        private LinearLayout _layoutLeft = null!;
        private LinearLayout _layoutCenter = null!;
        private LinearLayout _layoutRight = null!;
        private Button _buttonRight = null!;
        private Ime? _imeService;

        public Gn1(Context context) : base(context)
        {
            Initialize(context);
        }

        public Gn1(Context context, IAttributeSet attrs) : base(context, attrs)
        {
            Initialize(context);
        }

        public event EventHandler<Gn1StateChangedEventArgs>? StateChanged;

        private void Initialize(Context context)
        {
            var inflater = (LayoutInflater)context.GetSystemService(Context.LayoutInflaterService);
#if DEBUG
            inflater.Inflate(Resource.Layout.gn1, this, true);
#else
            EncryptedLayout.Inflate(inflater, "layout/gn1", Resource.Layout.gn1, this, true);
#endif
            GetViews();
            BindButtons();
            RenderPanel();
        }

        private void BindButtons()
        {
            _buttonRight.Click += (_, _) =>
            {
                if (_panelMode != Gn1PanelMode.Shortcuts)
                {
                    return;
                }

                SendRandomShortcut();
            };
        }

        private void GetViews()
        {
            _layoutLeft = FindViewById<LinearLayout>(Resource.Id.layout_left)!;
            _layoutCenter = FindViewById<LinearLayout>(Resource.Id.layout_center)!;
            _layoutRight = FindViewById<LinearLayout>(Resource.Id.layout_right)!;
            _buttonRight = FindViewById<Button>(Resource.Id.button_right)!;
        }

        public void SetImeService(Ime imeService)
        {
            _imeService = imeService;
        }

        public void RestoreState(Gn1PanelMode panelMode, string? currentCategory, IReadOnlyList<string>? currentItems)
        {
            _panelMode = panelMode;
            _currentCategory = currentCategory ?? string.Empty;
            _currentItems = CanUseCustomShortcuts()
                ? currentItems?.Where(item => !string.IsNullOrWhiteSpace(item)).ToList() ?? new List<string>()
                : new List<string>();
        }

        public void ShowHome()
        {
            _panelMode = Gn1PanelMode.Home;
            RenderPanel();
        }

        public void UpdateViews()
        {
            RenderPanel();
        }

        private void ShowShortcutsPanel()
        {
            _panelMode = Gn1PanelMode.Shortcuts;
            RenderPanel();
        }

        private void ShowClipboardPanel()
        {
            _panelMode = Gn1PanelMode.Clipboard;
            RenderPanel();
        }

        private void RenderPanel()
        {
            _layoutLeft.RemoveAllViews();
            _layoutCenter.RemoveAllViews();

            switch (_panelMode)
            {
                case Gn1PanelMode.Home:
                    RenderHomePanel();
                    break;
                case Gn1PanelMode.Shortcuts:
                    RenderShortcutsPanel();
                    break;
                case Gn1PanelMode.Clipboard:
                    RenderClipboardPanel();
                    break;
            }

            NotifyStateChanged();
        }

        private void RenderHomePanel()
        {
            _layoutRight.Visibility = ViewStates.Gone;
            _layoutCenter.SetGravity(GravityFlags.CenterVertical);

            AddTitleText(_layoutLeft, Resource.String.function_home_title);
            AddMutedText(_layoutLeft, Context.GetString(Resource.String.function_home_hint));

            _layoutCenter.AddView(CreateActionButton(Resource.String.function_home_shortcuts, (_, _) => ShowShortcutsPanel()));
            _layoutCenter.AddView(CreateActionButton(Resource.String.function_home_clipboard, (_, _) => ShowClipboardPanel()));
        }

        private void RenderShortcutsPanel()
        {
            _layoutRight.Visibility = ViewStates.Visible;
            _layoutCenter.SetGravity(GravityFlags.NoGravity);
            _buttonRight.Text = Context.GetString(Resource.String.function_shortcut_random);

            AddTitleText(_layoutLeft, Resource.String.function_home_shortcuts);

            if (_imeService == null)
            {
                RenderMessage(Resource.String.clipboard_msg_action_unavailable);
                return;
            }

            bool includeCustomShortcuts = CanUseCustomShortcuts();
            List<string> categories = _imeService._swordDBHelper.QueryCategories(includeCustomShortcuts);
            if (categories.Count == 0)
            {
                _currentCategory = string.Empty;
                _currentItems.Clear();
                RenderMessage(Resource.String.function_msg_no_categories);
                return;
            }

            if (string.IsNullOrWhiteSpace(_currentCategory) || !categories.Contains(_currentCategory))
            {
                _currentCategory = categories[0];
                _currentItems.Clear();
            }

            foreach (string category in categories)
            {
                AddCategoryView(category, string.Equals(category, _currentCategory, StringComparison.Ordinal));
            }

            if (_currentItems.Count > 0)
            {
                RenderItems(_currentItems);
                return;
            }

            RenderMessage(Resource.String.function_msg_select_category);
            _ = LoadCategoryItemsAsync(_currentCategory);
        }

        private void RenderClipboardPanel()
        {
            _layoutRight.Visibility = ViewStates.Gone;
            _layoutCenter.SetGravity(GravityFlags.NoGravity);

            AddTitleText(_layoutLeft, Resource.String.function_home_clipboard);
            AddMutedText(_layoutLeft, Context.GetString(Resource.String.function_clipboard_hint));

            _layoutCenter.AddView(CreateClipboardActionRow(
                CreateCompactPanelButton(Resource.String.clipboard_copy, (_, _) => ExecuteClipboardAction(ClipboardActionType.Copy)),
                CreateCompactPanelButton(Resource.String.clipboard_cut, (_, _) => ExecuteClipboardAction(ClipboardActionType.Cut))));
            _layoutCenter.AddView(CreateClipboardActionRow(
                CreateCompactPanelButton(Resource.String.clipboard_paste, (_, _) => ExecuteClipboardAction(ClipboardActionType.Paste)),
                CreateCompactPanelButton(Resource.String.clipboard_select_all, (_, _) => ExecuteClipboardAction(ClipboardActionType.SelectAll))));
            RenderClipboardHistory();
        }

        private void AddTitleText(LinearLayout container, int textId)
        {
            var title = new TextView(Context)
            {
                Text = Context.GetString(textId),
                TextSize = 18
            };
            title.SetTextColor(Color.Black);
            title.SetTypeface(title.Typeface, TypefaceStyle.Bold);
            title.LayoutParameters = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.WrapContent)
            {
                TopMargin = 10
            };
            container.AddView(title);
        }

        private void AddMutedText(LinearLayout container, string text)
        {
            var textView = new TextView(Context)
            {
                Text = text,
                TextSize = 14
            };
            textView.SetTextColor(Color.DarkGray);
            textView.LayoutParameters = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.WrapContent)
            {
                TopMargin = 10
            };
            container.AddView(textView);
        }

        private Button CreateActionButton(int textId, EventHandler onClick, bool compact = false)
        {
            var button = new Button(Context)
            {
                Text = Context.GetString(textId)
            };
            button.SetAllCaps(false);
            button.LayoutParameters = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.WrapContent)
            {
                TopMargin = compact ? 10 : 14
            };
            button.Click += onClick;
            return button;
        }

        private void AddCategoryView(string category, bool isSelected)
        {
            TextView textView = CreateTextView(category);
            textView.SetTextColor(Color.Black);
            textView.SetBackgroundColor(isSelected ? Color.AliceBlue : Color.Lavender);
            _layoutLeft.AddView(textView);

            textView.Click += async (_, _) => { await LoadCategoryItemsAsync(category); };
        }

        private async Task LoadCategoryItemsAsync(string category)
        {
            if (_imeService == null)
            {
                return;
            }

            int loadVersion = ++_loadVersion;
            _currentCategory = category;
            _currentItems.Clear();

            if (_panelMode == Gn1PanelMode.Shortcuts)
            {
                _layoutCenter.RemoveAllViews();
                var progressBar = new ProgressBar(Context) { Indeterminate = true };
                _layoutCenter.AddView(progressBar);
                NotifyStateChanged();
            }

            Log.Info("IME", $"Load shortcut category: {category}");

            bool includeCustomShortcuts = CanUseCustomShortcuts();
            List<string> items = await Task.Run(() => _imeService._swordDBHelper.QueryRandomItemsByCategory(category, 200, includeCustomShortcuts));
            if (loadVersion != _loadVersion)
            {
                return;
            }

            _currentItems = items.Where(item => !string.IsNullOrWhiteSpace(item)).ToList();

            if (_panelMode != Gn1PanelMode.Shortcuts)
            {
                NotifyStateChanged();
                return;
            }

            RenderPanel();
        }

        private void RenderItems(IReadOnlyList<string> items)
        {
            _layoutCenter.RemoveAllViews();

            if (items.Count == 0)
            {
                RenderMessage(Resource.String.function_msg_no_shortcuts);
                return;
            }

            foreach (string item in items)
            {
                AddItemView(item);
            }
        }

        private void AddItemView(string item)
        {
            TextView textView = CreateTextView(item);
            textView.SetBackgroundColor(Color.White);
            textView.SetTextColor(Color.Black);
            _layoutCenter.AddView(textView);

            textView.Click += (_, _) =>
            {
                Log.Info("IME", $"Send shortcut item: {item}");
                SendMessageEvent(item);
            };
        }

        private void ExecuteClipboardAction(ClipboardActionType action)
        {
            if (_imeService?.PerformClipboardAction(action) == true)
            {
                RenderPanel();
            }
        }

        private void RenderClipboardHistory()
        {
            var history = _imeService?.GetClipboardHistory() ?? Array.Empty<string>();

            _layoutCenter.AddView(CreateSectionTitle(Resource.String.clipboard_history_title));
            if (history.Count == 0)
            {
                _layoutCenter.AddView(CreateHistoryEmptyView());
                return;
            }

            for (int i = 0; i < history.Count; i++)
            {
                _layoutCenter.AddView(CreateClipboardHistoryItem(history[i]));
            }
        }

        private TextView CreateSectionTitle(int textId)
        {
            var textView = new TextView(Context)
            {
                Text = Context.GetString(textId),
                TextSize = 16
            };
            textView.SetTextColor(Color.Black);
            textView.SetTypeface(textView.Typeface, TypefaceStyle.Bold);
            textView.LayoutParameters = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.WrapContent)
            {
                TopMargin = 18
            };
            return textView;
        }

        private TextView CreateHistoryEmptyView()
        {
            var textView = new TextView(Context)
            {
                Text = Context.GetString(Resource.String.clipboard_history_empty),
                TextSize = 14
            };
            textView.SetTextColor(Color.Gray);
            textView.LayoutParameters = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.WrapContent)
            {
                TopMargin = 12
            };
            return textView;
        }

        private View CreateClipboardHistoryItem(string text)
        {
            var container = new LinearLayout(Context)
            {
                Orientation = Orientation.Vertical
            };
            container.SetBackgroundColor(Color.White);
            container.SetPadding(20, 16, 20, 16);
            container.LayoutParameters = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.WrapContent)
            {
                TopMargin = 12
            };

            var content = new TextView(Context)
            {
                Text = BuildClipboardHistoryPreview(text),
                TextSize = 15
            };
            content.SetTextColor(Color.Black);
            container.AddView(content);

            var buttonRow = new LinearLayout(Context)
            {
                Orientation = Orientation.Horizontal
            };
            buttonRow.LayoutParameters = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.WrapContent)
            {
                TopMargin = 10
            };

            var pasteButton = CreateInlineActionButton(Resource.String.clipboard_history_paste, (_, _) => PasteClipboardHistoryItem(text));
            var copyButton = CreateInlineActionButton(Resource.String.clipboard_history_copy, (_, _) => CopyClipboardHistoryItem(text));
            buttonRow.AddView(pasteButton);
            buttonRow.AddView(copyButton);
            container.AddView(buttonRow);

            return container;
        }

        private Button CreateInlineActionButton(int textId, EventHandler onClick)
        {
            var button = new Button(Context)
            {
                Text = Context.GetString(textId)
            };
            button.SetAllCaps(false);
            button.SetMinHeight(0);
            button.SetMinimumHeight(0);
            button.TextSize = 12;
            button.SetPadding(16, 8, 16, 8);
            button.LayoutParameters = new LinearLayout.LayoutParams(
                0,
                ViewGroup.LayoutParams.WrapContent,
                1f)
            {
                RightMargin = 8
            };
            button.Click += onClick;
            return button;
        }

        private LinearLayout CreateClipboardActionRow(Button leftButton, Button rightButton)
        {
            var row = new LinearLayout(Context)
            {
                Orientation = Orientation.Horizontal
            };
            row.LayoutParameters = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.WrapContent)
            {
                TopMargin = 8
            };

            row.AddView(leftButton);
            row.AddView(rightButton);
            return row;
        }

        private Button CreateCompactPanelButton(int textId, EventHandler onClick)
        {
            var button = new Button(Context)
            {
                Text = Context.GetString(textId)
            };
            button.SetAllCaps(false);
            button.SetMinHeight(0);
            button.SetMinimumHeight(0);
            button.TextSize = 13;
            button.SetPadding(12, 10, 12, 10);
            button.LayoutParameters = new LinearLayout.LayoutParams(
                0,
                ViewGroup.LayoutParams.WrapContent,
                1f)
            {
                RightMargin = 8
            };
            button.Click += onClick;
            return button;
        }

        private void PasteClipboardHistoryItem(string text)
        {
            if (_imeService?.PasteClipboardHistoryItem(text) == true)
            {
                RenderPanel();
            }
        }

        private void CopyClipboardHistoryItem(string text)
        {
            if (_imeService?.CopyTextToClipboard(text) == true)
            {
                RenderPanel();
            }
        }

        private static string BuildClipboardHistoryPreview(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            string singleLine = text.Replace("\r", " ").Replace("\n", " ").Trim();
            if (singleLine.Length <= ClipboardHistoryPreviewLength)
            {
                return singleLine;
            }

            return singleLine.Substring(0, ClipboardHistoryPreviewLength) + "...";
        }

        private void SendRandomShortcut()
        {
            if (_imeService == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_currentCategory))
            {
                List<string> categories = _imeService._swordDBHelper.QueryCategories(CanUseCustomShortcuts());
                if (categories.Count > 0)
                {
                    _currentCategory = categories[0];
                }
            }

            if (string.IsNullOrWhiteSpace(_currentCategory))
            {
                Toast.MakeText(Context, Resource.String.function_msg_no_categories, ToastLength.Short)?.Show();
                return;
            }

            List<string> list = _imeService._swordDBHelper.QueryRandomItemsByCategory(_currentCategory, 1, CanUseCustomShortcuts());
            if (list.Count == 0)
            {
                Toast.MakeText(Context, Resource.String.function_msg_no_shortcuts, ToastLength.Short)?.Show();
                return;
            }

            SendMessageEvent(list[0]);
        }

        private void SendMessageEvent(string message)
        {
            if (_imeService == null)
            {
                return;
            }

            _imeService.CommitText(message, false);
            _imeService.CurrentInputConnection?.FinishComposingText();

            long now = Java.Lang.JavaSystem.CurrentTimeMillis();
            new Handler(Looper.MainLooper).PostDelayed(() =>
            {
                var inputConnection = _imeService.CurrentInputConnection;
                if (inputConnection == null)
                {
                    return;
                }

                bool handled = inputConnection.PerformEditorAction(Android.Views.InputMethods.ImeAction.Send);
                if (!handled)
                {
                    var downEvent = new KeyEvent(now, now, KeyEventActions.Down, Keycode.Enter, 0);
                    var upEvent = new KeyEvent(now, now, KeyEventActions.Up, Keycode.Enter, 0);
                    inputConnection.SendKeyEvent(downEvent);
                    inputConnection.SendKeyEvent(upEvent);
                }
            }, 50);
        }

        private void RenderMessage(int textId)
        {
            _layoutCenter.RemoveAllViews();
            _layoutCenter.AddView(CreateMessageText(Context.GetString(textId)));
        }

        private TextView CreateMessageText(string text)
        {
            return new TextView(Context)
            {
                TextSize = 18,
                Text = text,
                LayoutParameters = new LinearLayout.LayoutParams(
                    ViewGroup.LayoutParams.MatchParent,
                    ViewGroup.LayoutParams.WrapContent),
                Gravity = GravityFlags.Center
            };
        }

        private TextView CreateTextView(string text)
        {
            return new TextView(Context)
            {
                TextSize = 20,
                Text = text,
                LayoutParameters = new LinearLayout.LayoutParams(
                    ViewGroup.LayoutParams.MatchParent,
                    ViewGroup.LayoutParams.WrapContent)
                {
                    TopMargin = 10
                },
                Gravity = GravityFlags.CenterHorizontal
            };
        }

        private void NotifyStateChanged()
        {
            StateChanged?.Invoke(this, new Gn1StateChangedEventArgs(_panelMode, _currentCategory, _currentItems.ToArray()));
        }

        private bool CanUseCustomShortcuts()
        {
            return KamiVipConfig.CanUseCustomShortcuts(Context);
        }
    }
}




