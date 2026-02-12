using System.Threading.Tasks;
using Android.App;
using Android.InputMethodServices;
using Android.Util;
using Android.Views;
using IME.Features.Input.Abstractions;
using IME.Features.Input.Feedback;
using IME.Features.Input.Handlers;
using IME.Features.Keyboard;
using IME.Features.Prediction;
using IME.Features.UserLexicon;
using IME.Shared.Abstractions;
using IME.Shared.Data;

namespace IME.Features.Input
{
    [Service(Name = "IME.Features.Input.Ime", Permission = "android.permission.BIND_INPUT_METHOD", Exported = true)]
    [IntentFilter(new[] { "android.view.InputMethod" })]
    [MetaData("android.view.im", Resource = "@xml/method_config")]
    public class Ime : InputMethodService, IInputEngineHost
    {
        private KeyboardHandler? _keyboardHandler;
        private InputCoordinator? _inputCoordinator;
        private InputEngineManager? _engineManager;
        private IInputConnectionAdapter? _inputConnection;
        private KeyFeedbackManager? _feedbackManager;
        private PredictionCoordinator? _predictionCoordinator;

        private SwordDBHelper? _swordDbHelper;
        private LocalUserLexiconStore? _localLexiconStore;
        private readonly object _lexiconLock = new object();

        public SwordDBHelper _swordDBHelper => _swordDbHelper!;
        public IInputEngine? _currentInputEngine => _engineManager?.CurrentEngine;

        public override void OnCreate()
        {
            base.OnCreate();

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

                InitializeLocalStores();

                _engineManager.InitializeRimeAsync(engine =>
                {
                    _keyboardHandler?.NotifyEngineChanged(engine);
                });
            }
            catch (System.Exception ex)
            {
                Log.Error("IME", $"Initialization failed: {ex.Message}");
                Log.Error("IME", $"StackTrace: {ex.StackTrace}");
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
                Log.Error("IME", $"Sword DB init failed: {ex.Message}");
            }
        }

        public bool IsRimeAvailable()
        {
            return _engineManager?.IsRimeAvailable ?? false;
        }

        internal void SetCurrentInputEngine(IInputEngine engine)
        {
            _engineManager?.SetCurrentEngine(engine);
        }

        public override View? OnCreateInputView()
        {
            try
            {
                var keyboardView = _keyboardHandler?.CreateKeyboardView();
                if (keyboardView == null)
                {
                    Log.Error("InputMethodService", "Keyboard layout load failed.");
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
                Log.Error("IME", $"CreateInputView failed: {ex.Message}");
                Log.Error("IME", $"StackTrace: {ex.StackTrace}");
                return null;
            }
        }

        public override void OnInitializeInterface()
        {
            // No-op. Initialization is handled in OnCreate.
        }

        public override void OnDestroy()
        {
            try
            {
                _inputCoordinator?.Cleanup();
                _inputCoordinator = null;

                if (_keyboardHandler != null)
                {
                    _keyboardHandler.Cleanup();
                    _keyboardHandler = null;
                }

                _predictionCoordinator?.Cleanup();
                _predictionCoordinator = null;

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
                Log.Error("IME", $"Cleanup failed: {ex.Message}");
            }
            finally
            {
                base.OnDestroy();
            }
        }

        internal void RecordUserLexicon(string word, string pinyin)
        {
            if (string.IsNullOrWhiteSpace(word) || string.IsNullOrWhiteSpace(pinyin))
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
    }
}
