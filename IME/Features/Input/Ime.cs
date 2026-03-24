using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.InputMethodServices;
using Android.Util;
using Android.Views;
using Android.Views.InputMethods;
using Android.Widget;
using IME.Features.Input.Abstractions;
using IME.Features.Input.Feedback;
using IME.Features.Input.Handlers;
using IME.Features.Input.Simulation;
using IME.Features.Keyboard;
using IME.Features.Prediction;
using IME.Features.Settings;
using IME.Features.UserLexicon;
using IME.Shared.Abstractions;
using IME.Shared.Data;
using IME.Shared.Security;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace IME.Features.Input
{
    [Service(Name = "IME.Features.Input.Ime", Permission = "android.permission.BIND_INPUT_METHOD", Exported = true)]
    [IntentFilter(new[] { "android.view.InputMethod" })]
    [MetaData("android.view.im", Resource = "@xml/method_config")]
    public class Ime : InputMethodService, IInputEngineHost
    {
        private const string Tag = "IME";
        private const string ClipboardHistoryPrefsName = "ime_clipboard_history";
        private const string ClipboardHistoryKey = "items";
        private const int ClipboardHistoryMaxItems = 12;

        private KeyboardHandler? _keyboardHandler;
        private InputCoordinator? _inputCoordinator;
        private InputEngineManager? _engineManager;
        private IInputConnectionAdapter? _inputConnection;
        private KeyFeedbackManager? _feedbackManager;
        private PredictionCoordinator? _predictionCoordinator;
        private SimulatedTypingService? _simulatedTypingService;
        private SecurityCheckResult _securityCheckResult = SecurityCheckResult.Allowed;

        private SwordDBHelper? _swordDbHelper;
        private LocalUserLexiconStore? _localLexiconStore;
        private readonly object _lexiconLock = new object();
        private int _lifecycleEventCounter;
        private int _inputSessionCounter;

        public SwordDBHelper _swordDBHelper => _swordDbHelper!;
        public IInputEngine? _currentInputEngine => _engineManager?.CurrentEngine;
        internal KeyboardHandler? CurrentKeyboardHandler => _keyboardHandler;

        public override void OnCreate()
        {
            base.OnCreate();
            LogLifecycle("OnCreate");

            _securityCheckResult = AppSecurityGuard.Check(this);
            if (!_securityCheckResult.IsAllowed)
            {
                Log.Warn(Tag, $"Security check blocked IME startup: {_securityCheckResult.DisplayMessage}");
                return;
            }

            try
            {
                _engineManager = new InputEngineManager(this);
                _engineManager.Initialize();

                var keyboardHandler = new KeyboardHandler(this, this);
                var connection = new InputConnectionAdapter(this);
                var feedback = new KeyFeedbackManager(this);

                _keyboardHandler = keyboardHandler;
                _inputConnection = connection;
                _feedbackManager = feedback;

                var characterHandler = new CharacterInputHandler(this, connection, keyboardHandler);
                var specialKeyHandler = new SpecialKeyHandler(this, connection, keyboardHandler, this);
                var switchHandler = new KeyboardSwitchHandler(keyboardHandler, specialKeyHandler);

                _inputCoordinator = new InputCoordinator(
                    connection,
                    feedback,
                    characterHandler,
                    specialKeyHandler,
                    switchHandler,
                    keyboardHandler);

                _predictionCoordinator = new PredictionCoordinator(this, this, keyboardHandler);
                _simulatedTypingService = new SimulatedTypingService(this);

                InitializeLocalStores();
                EnsureEngineReady("OnCreate");
            }
            catch (System.Exception ex)
            {
                Log.Error(Tag, $"Initialization failed: {ex.Message}");
                Log.Error(Tag, $"StackTrace: {ex.StackTrace}");
            }
        }

        private void InitializeLocalStores()
        {
            try
            {
                _swordDbHelper = new SwordDBHelper(this);
            }
            catch (System.Exception ex)
            {
                Log.Error(Tag, $"Sword DB init failed: {ex.Message}");
            }
        }

        private void EnsureEngineReady(string reason, bool logSnapshotWhenIdle = false)
        {
            try
            {
                if (_engineManager == null)
                {
                    Log.Warn(Tag, $"EnsureEngineReady skipped: manager is null. reason={reason}");
                    return;
                }

                string beforeState = _engineManager.GetStateSummary();
                bool started = _engineManager.EnsureRimeInitializedAsync(HandleEngineReady);
                if (started)
                {
                    Log.Info(Tag, $"Requested Rime engine initialization/retry. reason={reason}; {beforeState}");
                }
                else if (logSnapshotWhenIdle)
                {
                    Log.Info(Tag, $"EnsureEngineReady no-op. reason={reason}; {_engineManager.GetStateSummary()}");
                }
            }
            catch (System.Exception ex)
            {
                Log.Warn(Tag, $"EnsureEngineReady failed. reason={reason}; {ex.Message}");
            }
        }

        private void HandleEngineReady(IInputEngine engine)
        {
            try
            {
                Log.Info(Tag, $"Engine ready callback received. engine={engine.GetType().Name}; state={_engineManager?.GetStateSummary() ?? "manager=null"}");
                _keyboardHandler?.NotifyEngineChanged(engine);
                _keyboardHandler?.UpdateChineseCandidates();
            }
            catch (System.Exception ex)
            {
                Log.Warn(Tag, $"HandleEngineReady failed: {ex.Message}");
            }
        }

        public bool IsRimeAvailable()
        {
            return _engineManager?.IsRimeAvailable ?? false;
        }

        internal void SetCurrentInputEngine(IInputEngine engine)
        {
            _engineManager?.SetCurrentEngine(engine);
            Log.Info(Tag, $"SetCurrentInputEngine called. engine={engine?.GetType().Name ?? "null"}; state={_engineManager?.GetStateSummary() ?? "manager=null"}");
        }

        public override void OnBindInput()
        {
            base.OnBindInput();
            LogLifecycle("OnBindInput");
            EnsureEngineReady("OnBindInput", logSnapshotWhenIdle: true);
        }

        public override void OnUnbindInput()
        {
            LogLifecycle("OnUnbindInput");
            base.OnUnbindInput();
        }

        public override void OnStartInput(EditorInfo? attribute, bool restarting)
        {
            base.OnStartInput(attribute, restarting);

            _inputSessionCounter++;
            LogLifecycle("OnStartInput", $"session={_inputSessionCounter}, restarting={restarting}, package={attribute?.PackageName ?? "unknown"}, inputType={attribute?.InputType}, imeOptions={attribute?.ImeOptions}");
            EnsureEngineReady("OnStartInput", logSnapshotWhenIdle: true);
        }

        public override View? OnCreateInputView()
        {
            try
            {
                LogLifecycle("OnCreateInputView");
                if (!_securityCheckResult.IsAllowed)
                {
                    return CreateSecurityBlockedView();
                }

                EnsureEngineReady("OnCreateInputView", logSnapshotWhenIdle: true);

                var keyboardView = _keyboardHandler?.CreateKeyboardView();
                if (keyboardView == null)
                {
                    Log.Error(Tag, $"Keyboard layout load failed. state={_engineManager?.GetStateSummary() ?? "manager=null"}");
                    return null;
                }

                if (_keyboardHandler != null && _inputCoordinator != null)
                {
                    _keyboardHandler.SetKeyEventListener(_inputCoordinator);
                }

                return keyboardView;
            }
            catch (System.Exception ex)
            {
                Log.Error(Tag, $"CreateInputView failed: {ex.Message}");
                Log.Error(Tag, $"StackTrace: {ex.StackTrace}");
                return null;
            }
        }

        public override void OnStartInputView(EditorInfo? info, bool restarting)
        {
            base.OnStartInputView(info, restarting);

            try
            {
                LogLifecycle("OnStartInputView", $"restarting={restarting}, package={info?.PackageName ?? "unknown"}");
                if (!_securityCheckResult.IsAllowed)
                {
                    var blockedView = CreateSecurityBlockedView();
                    SetInputView(blockedView);
                    return;
                }

                EnsureEngineReady("OnStartInputView", logSnapshotWhenIdle: true);

                var refreshedView = OnCreateInputView();
                if (refreshedView != null)
                {
                    SetInputView(refreshedView);
                }
            }
            catch (System.Exception ex)
            {
                Log.Error(Tag, $"Refresh input view failed: {ex.Message}");
            }
        }

        public override void OnFinishInputView(bool finishingInput)
        {
            LogLifecycle("OnFinishInputView", $"finishingInput={finishingInput}");
            base.OnFinishInputView(finishingInput);
        }

        public override void OnFinishInput()
        {
            LogLifecycle("OnFinishInput");
            base.OnFinishInput();
        }

        public override void OnWindowHidden()
        {
            LogLifecycle("OnWindowHidden");
            base.OnWindowHidden();
        }

        public override void OnWindowShown()
        {
            base.OnWindowShown();
            LogLifecycle("OnWindowShown");
            EnsureEngineReady("OnWindowShown", logSnapshotWhenIdle: true);
        }

        public override void OnInitializeInterface()
        {
            // No-op. Initialization is handled in OnCreate.
        }

        public override void OnDestroy()
        {
            try
            {
                LogLifecycle("OnDestroy");
                _inputCoordinator?.Cleanup();
                _inputCoordinator = null;

                if (_keyboardHandler != null)
                {
                    _keyboardHandler.Cleanup();
                    _keyboardHandler = null;
                }

                _predictionCoordinator?.Cleanup();
                _predictionCoordinator = null;

                _simulatedTypingService?.Dispose();
                _simulatedTypingService = null;

                _engineManager?.Dispose();
                _engineManager = null;

                if (_localLexiconStore != null)
                {
                    _localLexiconStore.Dispose();
                    _localLexiconStore = null;
                }

                if (_swordDbHelper != null)
                {
                    _swordDbHelper.Close();
                    _swordDbHelper = null;
                }
            }
            catch (System.Exception ex)
            {
                Log.Error(Tag, $"Cleanup failed: {ex.Message}");
            }
            finally
            {
                base.OnDestroy();
            }
        }

        private void LogLifecycle(string eventName, string? details = null)
        {
            int sequence = System.Threading.Interlocked.Increment(ref _lifecycleEventCounter);
            string suffix = string.IsNullOrWhiteSpace(details) ? string.Empty : $", {details}";
            Log.Info(Tag, $"Lifecycle[{sequence}] {eventName}{suffix}, state={_engineManager?.GetStateSummary() ?? "manager=null"}");
        }

        internal void RecordUserLexicon(string word, string pinyin)
        {
            if (string.IsNullOrWhiteSpace(word) || string.IsNullOrWhiteSpace(pinyin))
            {
                return;
            }

            if (!KamiVipConfig.CanLearnUserLexicon(this))
            {
                return;
            }

            Task.Run(() =>
            {
                try
                {
                    lock (_lexiconLock)
                    {
                        _localLexiconStore ??= new LocalUserLexiconStore(this);
                        _localLexiconStore.IncrementFrequency(pinyin.Trim(), word.Trim(), 1);
                    }
                }
                catch (System.Exception ex)
                {
                    Log.Warn("IME", $"Record user lexicon failed: {ex.Message}");
                }
            });
        }

        internal void CommitText(string text, bool isPredictionCommit, int newCursorPosition = 1)
        {
            if (_inputConnection == null || string.IsNullOrEmpty(text))
            {
                return;
            }

            _inputConnection.CommitText(text, newCursorPosition);
            NotifyCommittedText(text, isPredictionCommit);
        }

        internal void StartSimulatedTyping(string text, bool sendAfterCommit = false)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            if (_simulatedTypingService == null)
            {
                CommitText(text, false);
                return;
            }

            _simulatedTypingService.StartTyping(text, sendAfterCommit);
        }

        internal bool PerformClipboardAction(ClipboardActionType action)
        {
            try
            {
                var connection = CurrentInputConnection;
                if (connection == null)
                {
                    ShowClipboardToast(Resource.String.clipboard_msg_action_unavailable);
                    return false;
                }

                ResetCompositionBeforeClipboardAction();

                return action switch
                {
                    ClipboardActionType.Copy => TryCopySelection(connection),
                    ClipboardActionType.Cut => TryCutSelection(connection),
                    ClipboardActionType.Paste => TryPasteClipboard(connection),
                    ClipboardActionType.SelectAll => TrySelectAll(connection),
                    _ => false
                };
            }
            catch (System.Exception ex)
            {
                Log.Warn("IME", $"Clipboard action failed: {ex.Message}");
                ShowClipboardToast(Resource.String.clipboard_msg_action_unavailable);
                return false;
            }
        }

        internal IReadOnlyList<string> GetClipboardHistory(int maxCount = ClipboardHistoryMaxItems)
        {
            List<string> history = LoadClipboardHistory();
            if (maxCount <= 0 || history.Count <= maxCount)
            {
                return history;
            }

            return history.Take(maxCount).ToList();
        }

        internal bool PasteClipboardHistoryItem(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                ShowClipboardToast(Resource.String.clipboard_msg_empty);
                return false;
            }

            try
            {
                var connection = CurrentInputConnection;
                if (connection == null)
                {
                    ShowClipboardToast(Resource.String.clipboard_msg_action_unavailable);
                    return false;
                }

                ResetCompositionBeforeClipboardAction();
                IInputConnectionExtensions.CommitText(connection, text, 1);
                RecordClipboardHistory(text);
                return true;
            }
            catch (System.Exception ex)
            {
                Log.Warn("IME", $"Paste clipboard history failed: {ex.Message}");
                ShowClipboardToast(Resource.String.clipboard_msg_action_unavailable);
                return false;
            }
        }

        internal bool CopyTextToClipboard(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                ShowClipboardToast(Resource.String.clipboard_msg_empty);
                return false;
            }

            try
            {
                var clipboard = (ClipboardManager?)GetSystemService(ClipboardService);
                if (clipboard == null)
                {
                    ShowClipboardToast(Resource.String.clipboard_msg_action_unavailable);
                    return false;
                }

                clipboard.PrimaryClip = ClipData.NewPlainText("ime_history", text);
                RecordClipboardHistory(text);
                return true;
            }
            catch (System.Exception ex)
            {
                Log.Warn("IME", $"Copy clipboard history failed: {ex.Message}");
                ShowClipboardToast(Resource.String.clipboard_msg_action_unavailable);
                return false;
            }
        }

        bool IInputEngineHost.IsAsciiMode => _keyboardHandler?.GetAsciiMode() ?? false;

        IInputEngine? IInputEngineHost.CurrentEngine => _engineManager?.CurrentEngine;

        void IInputEngineHost.SetCurrentEngine(IInputEngine engine) => SetCurrentInputEngine(engine);

        void IInputEngineHost.UpdateCandidates() => _keyboardHandler?.UpdateChineseCandidates();

        void IInputEngineHost.ClearCandidates() => _keyboardHandler?.ClearCandidates();

        void IInputEngineHost.RecordUserLexicon(string word, string pinyin) => RecordUserLexicon(word, pinyin);

        void IInputEngineHost.NotifyCommittedText(string text, bool isPredictionCommit) => NotifyCommittedText(text, isPredictionCommit);

        internal void NotifyCommittedText(string text, bool isPredictionCommit)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            _predictionCoordinator?.OnCommittedText(text, isPredictionCommit);
        }

        private void ResetCompositionBeforeClipboardAction()
        {
            var engine = _engineManager?.CurrentEngine;
            if (engine == null)
            {
                return;
            }

            string composingText = engine.GetComposingText();
            if (string.IsNullOrEmpty(composingText))
            {
                return;
            }

            engine.Reset();
            _keyboardHandler?.ClearCandidates();
            _keyboardHandler?.NotifyT9BufferShouldReset();
        }

        private bool TryCopySelection(IInputConnection connection)
        {
            string? selectedText = IInputConnectionExtensions.GetSelectedText(connection, GetTextFlags.None);
            bool copied = connection.PerformContextMenuAction(Android.Resource.Id.Copy);
            if (!copied && !string.IsNullOrEmpty(selectedText))
            {
                var clipboard = (ClipboardManager?)GetSystemService(ClipboardService);
                if (clipboard == null)
                {
                    ShowClipboardToast(Resource.String.clipboard_msg_action_unavailable);
                    return false;
                }

                clipboard.PrimaryClip = ClipData.NewPlainText("ime_selection", selectedText);
                copied = true;
            }

            if (!copied)
            {
                ShowClipboardToast(Resource.String.clipboard_msg_select_text_first);
                return false;
            }

            RecordClipboardHistory(selectedText);
            return copied;
        }

        private bool TryCutSelection(IInputConnection connection)
        {
            string? selectedText = IInputConnectionExtensions.GetSelectedText(connection, GetTextFlags.None);
            bool cut = connection.PerformContextMenuAction(Android.Resource.Id.Cut);
            if (!cut)
            {
                ShowClipboardToast(Resource.String.clipboard_msg_select_text_first);
                return false;
            }

            RecordClipboardHistory(selectedText);
            return cut;
        }

        private bool TryPasteClipboard(IInputConnection connection)
        {
            var clipboard = (ClipboardManager?)GetSystemService(ClipboardService);
            string? clipboardText = GetClipboardPrimaryText(clipboard);

            if (connection.PerformContextMenuAction(Android.Resource.Id.Paste))
            {
                RecordClipboardHistory(clipboardText);
                return true;
            }

            if (string.IsNullOrEmpty(clipboardText))
            {
                ShowClipboardToast(Resource.String.clipboard_msg_empty);
                return false;
            }

            IInputConnectionExtensions.CommitText(connection, clipboardText, 1);
            RecordClipboardHistory(clipboardText);
            return true;
        }

        private bool TrySelectAll(IInputConnection connection)
        {
            bool selected = connection.PerformContextMenuAction(Android.Resource.Id.SelectAll);
            if (!selected)
            {
                ShowClipboardToast(Resource.String.clipboard_msg_action_unavailable);
            }

            return selected;
        }

        private void ShowClipboardToast(int messageId)
        {
            try
            {
                Toast.MakeText(this, messageId, ToastLength.Short)?.Show();
            }
            catch (System.Exception ex)
            {
                Log.Warn("IME", $"Show clipboard toast failed: {ex.Message}");
            }
        }

        private string? GetClipboardPrimaryText(ClipboardManager? clipboard)
        {
            if (clipboard?.PrimaryClip == null || clipboard.PrimaryClip.ItemCount <= 0)
            {
                return null;
            }

            var item = clipboard.PrimaryClip.GetItemAt(0);
            return item?.CoerceToText(this)?.ToString();
        }

        private void RecordClipboardHistory(string? text)
        {
            string normalized = text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(normalized))
            {
                return;
            }

            try
            {
                List<string> history = LoadClipboardHistory();
                history.RemoveAll(item => string.Equals(item, normalized, System.StringComparison.Ordinal));
                history.Insert(0, normalized);

                if (history.Count > ClipboardHistoryMaxItems)
                {
                    history.RemoveRange(ClipboardHistoryMaxItems, history.Count - ClipboardHistoryMaxItems);
                }

                SaveClipboardHistory(history);
            }
            catch (System.Exception ex)
            {
                Log.Warn("IME", $"Record clipboard history failed: {ex.Message}");
            }
        }

        private List<string> LoadClipboardHistory()
        {
            try
            {
                var prefs = GetSharedPreferences(ClipboardHistoryPrefsName, FileCreationMode.Private);
                string json = prefs?.GetString(ClipboardHistoryKey, string.Empty) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(json))
                {
                    return new List<string>();
                }

                List<string>? history = JsonConvert.DeserializeObject<List<string>>(json);
                if (history == null)
                {
                    return new List<string>();
                }

                return history
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Select(item => item.Trim())
                    .Distinct(System.StringComparer.Ordinal)
                    .Take(ClipboardHistoryMaxItems)
                    .ToList();
            }
            catch (System.Exception ex)
            {
                Log.Warn("IME", $"Load clipboard history failed: {ex.Message}");
                return new List<string>();
            }
        }

        private void SaveClipboardHistory(List<string> history)
        {
            var prefs = GetSharedPreferences(ClipboardHistoryPrefsName, FileCreationMode.Private);
            if (prefs == null)
            {
                return;
            }

            string json = JsonConvert.SerializeObject(history ?? new List<string>());
            using var editor = prefs.Edit();
            editor.PutString(ClipboardHistoryKey, json);
            editor.Apply();
        }


        private View CreateSecurityBlockedView()
        {
            var container = new LinearLayout(this)
            {
                Orientation = Orientation.Vertical
            };
            container.SetPadding(32, 24, 32, 24);

            var title = new TextView(this)
            {
                Text = GetString(Resource.String.security_block_title)
            };
            title.SetTextSize(Android.Util.ComplexUnitType.Sp, 18);

            var message = new TextView(this)
            {
                Text = string.IsNullOrWhiteSpace(_securityCheckResult.DisplayMessage)
                    ? GetString(Resource.String.security_block_ime_message)
                    : GetString(Resource.String.security_block_ime_message) + System.Environment.NewLine + _securityCheckResult.DisplayMessage
            };
            message.SetTextSize(Android.Util.ComplexUnitType.Sp, 14);

            var messageLayout = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.WrapContent)
            {
                TopMargin = 12
            };
            message.LayoutParameters = messageLayout;

            container.AddView(title);
            container.AddView(message);
            return container;
        }
    }
}

