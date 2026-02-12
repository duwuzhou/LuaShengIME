using System;
using System.Threading.Tasks;
using Android.Content;
using Android.Util;
using IME.Shared.Abstractions;
using IME.Shared.InputEngine;

namespace IME.Features.Input;

public sealed class InputEngineManager : IDisposable
{
    private readonly Context _context;
    private readonly object _lock = new object();
    private readonly DatabaseInputEngine _databaseEngine;

    private RimeInputEngine _rimeEngine;
    private IInputEngine? _currentEngine;
    private bool _rimeAvailable;
    private bool _rimeInitializing;

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

    public void Initialize()
    {
        if (_databaseEngine.Initialize(_context))
        {
            lock (_lock)
            {
                _currentEngine = _databaseEngine;
            }
        }
    }

    public void InitializeRimeAsync(Action<IInputEngine> onReady)
    {
        lock (_lock)
        {
            if (_rimeInitializing || _rimeAvailable)
            {
                return;
            }

            _rimeInitializing = true;
        }

        Task.Run(() =>
        {
            try
            {
                Log.Info("IME", "Starting async initialization for Rime engine.");

                var rime = new RimeInputEngine();
                if (rime.Initialize(_context))
                {
                    lock (_lock)
                    {
                        _rimeAvailable = true;
                        _rimeEngine = rime;
                        _currentEngine = rime;
                    }

                    Log.Info("IME", "Rime engine initialized and active.");
                    onReady?.Invoke(rime);
                }
                else
                {
                    Log.Warn("IME", "Rime engine init failed, keeping database engine.");
                    rime.Dispose();
                }
            }
            catch (Exception ex)
            {
                Log.Error("IME", $"Rime engine async init failed: {ex.Message}");
            }
            finally
            {
                lock (_lock)
                {
                    _rimeInitializing = false;
                }
            }
        });
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
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _rimeEngine?.Dispose();
            _rimeEngine = null;

            _databaseEngine?.Dispose();
            _currentEngine = null;
            _rimeAvailable = false;
            _rimeInitializing = false;
        }
    }
}
