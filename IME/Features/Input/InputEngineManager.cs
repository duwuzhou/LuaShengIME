using System;
using System.Threading.Tasks;
using Android.Content;
using Android.OS;
using Android.Util;
using IME.Shared.Abstractions;
using IME.Shared.InputEngine;

namespace IME.Features.Input;

public sealed class InputEngineManager : IDisposable
{
    private const string Tag = "InputEngineManager";

    private readonly Context _context;
    private readonly object _lock = new();
    private readonly DatabaseInputEngine _databaseEngine;

    private RimeInputEngine? _rimeEngine;
    private IInputEngine? _currentEngine;
    private bool _rimeAvailable;
    private bool _rimeInitializing;
    private int _rimeInitAttemptCount;
    private DateTimeOffset? _lastRimeInitStartedAt;
    private DateTimeOffset? _lastRimeInitFinishedAt;
    private string _lastRimeInitResult = "never_started";

    public InputEngineManager(Context context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _databaseEngine = new DatabaseInputEngine();
    }

    public IInputEngine? CurrentEngine
    {
        get
        {
            lock (_lock)
            {
                return _currentEngine;
            }
        }
    }

    public bool IsRimeAvailable
    {
        get
        {
            lock (_lock)
            {
                return _rimeAvailable;
            }
        }
    }

    public bool IsRimeInitializing
    {
        get
        {
            lock (_lock)
            {
                return _rimeInitializing;
            }
        }
    }

    public void Initialize()
    {
        if (_databaseEngine.Initialize(_context))
        {
            lock (_lock)
            {
                _currentEngine = _databaseEngine;
            }

            Log.Info(Tag, "Database engine initialized as fallback engine.");
        }
        else
        {
            Log.Warn(Tag, "Database engine initialization failed.");
        }
    }

    public bool EnsureRimeInitializedAsync(Action<IInputEngine>? onReady)
    {
        bool shouldStart;

        lock (_lock)
        {
            if (_rimeAvailable)
            {
                Log.Info(Tag, $"Rime already available. {GetStateSummary()}");
                if (_rimeEngine != null && onReady != null)
                {
                    DispatchReady(onReady, _rimeEngine);
                }

                return false;
            }

            if (_rimeInitializing)
            {
                Log.Info(Tag, $"Rime initialization already in progress. {GetStateSummary()}");
                return false;
            }

            _rimeInitializing = true;
            _rimeInitAttemptCount++;
            _lastRimeInitStartedAt = DateTimeOffset.UtcNow;
            _lastRimeInitResult = "in_progress";
            shouldStart = true;
        }

        if (!shouldStart)
        {
            return false;
        }

        Task.Run(() =>
        {
            DateTimeOffset startedAt = DateTimeOffset.UtcNow;
            try
            {
                Log.Info(Tag, $"Starting async initialization for Rime engine. attempt={_rimeInitAttemptCount}");

                var rime = new RimeInputEngine();
                if (rime.Initialize(_context))
                {
                    lock (_lock)
                    {
                        _rimeAvailable = true;
                        _rimeEngine = rime;
                        _currentEngine = rime;
                        _lastRimeInitFinishedAt = DateTimeOffset.UtcNow;
                        _lastRimeInitResult = "success";
                    }

                    Log.Info(Tag, $"Rime engine initialized and active in {(DateTimeOffset.UtcNow - startedAt).TotalMilliseconds:F0} ms.");
                    if (onReady != null)
                    {
                        DispatchReady(onReady, rime);
                    }
                }
                else
                {
                    lock (_lock)
                    {
                        _lastRimeInitFinishedAt = DateTimeOffset.UtcNow;
                        _lastRimeInitResult = "failed_initialize";
                        if (_currentEngine == null)
                        {
                            _currentEngine = _databaseEngine;
                        }
                    }

                    Log.Warn(Tag, $"Rime engine init failed, keeping database engine. {GetStateSummary()}");
                    rime.Dispose();
                }
            }
            catch (Exception ex)
            {
                lock (_lock)
                {
                    _lastRimeInitFinishedAt = DateTimeOffset.UtcNow;
                    _lastRimeInitResult = $"exception:{ex.GetType().Name}";
                    if (_currentEngine == null)
                    {
                        _currentEngine = _databaseEngine;
                    }
                }

                Log.Error(Tag, $"Rime engine async init failed: {ex.Message}");
            }
            finally
            {
                lock (_lock)
                {
                    _rimeInitializing = false;
                }
            }
        });

        return true;
    }

    public void InitializeRimeAsync(Action<IInputEngine> onReady)
    {
        EnsureRimeInitializedAsync(onReady);
    }

    public void SetCurrentEngine(IInputEngine engine)
    {
        if (engine == null)
        {
            return;
        }

        lock (_lock)
        {
            _currentEngine = engine;
        }

        Log.Info(Tag, $"Current engine updated to {GetEngineName(engine)}.");
    }

    public string GetStateSummary()
    {
        lock (_lock)
        {
            return $"current={GetEngineName(_currentEngine)}, rimeAvailable={_rimeAvailable}, rimeInitializing={_rimeInitializing}, attempts={_rimeInitAttemptCount}, lastResult={_lastRimeInitResult}, lastStarted={FormatTimestamp(_lastRimeInitStartedAt)}, lastFinished={FormatTimestamp(_lastRimeInitFinishedAt)}";
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _rimeEngine?.Dispose();
            _rimeEngine = null;

            _databaseEngine.Dispose();
            _currentEngine = null;
            _rimeAvailable = false;
            _rimeInitializing = false;
        }

        Log.Info(Tag, "Disposed all input engines.");
    }

    private static void DispatchReady(Action<IInputEngine> onReady, IInputEngine engine)
    {
        var handler = new Handler(Looper.MainLooper);
        handler.Post(() =>
        {
            try
            {
                onReady(engine);
            }
            catch (Exception ex)
            {
                Log.Warn("IME", $"Dispatch engine ready callback failed: {ex.Message}");
            }
        });
    }

    private static string GetEngineName(IInputEngine? engine)
    {
        return engine?.GetType().Name ?? "null";
    }

    private static string FormatTimestamp(DateTimeOffset? value)
    {
        return value?.ToString("O") ?? "n/a";
    }
}
