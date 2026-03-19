using System;
using Android.App;
using Android.OS;
using Android.Widget;

namespace IME.Shared.Security;

public abstract class SecurityMonitoredActivity : Activity
{
    private const int DefaultSecurityMonitorIntervalMs = 1500;

    private Handler? _securityMonitorHandler;
    private Action? _securityMonitorAction;
    private bool _securityBlockApplied;

    protected virtual int SecurityMonitorIntervalMs => DefaultSecurityMonitorIntervalMs;

    protected override void OnResume()
    {
        base.OnResume();

        if (EvaluateSecurityState())
        {
            StartSecurityMonitor();
        }
    }

    protected override void OnPause()
    {
        StopSecurityMonitor();
        base.OnPause();
    }

    protected override void OnDestroy()
    {
        StopSecurityMonitor();
        _securityMonitorHandler = null;
        _securityMonitorAction = null;
        base.OnDestroy();
    }

    protected bool EnsureSecurityAllowedNow()
    {
        return EvaluateSecurityState();
    }

    protected virtual void HandleSecurityBlocked(SecurityCheckResult result)
    {
        string message = string.IsNullOrWhiteSpace(result.DisplayMessage)
            ? "检测到安全风险，已阻止继续使用"
            : result.DisplayMessage;

        Toast.MakeText(this, message, ToastLength.Short)?.Show();
        Finish();
    }

    private void EnsureSecurityMonitorInitialized()
    {
        _securityMonitorHandler ??= new Handler(Looper.MainLooper);
        _securityMonitorAction ??= () =>
        {
            if (_securityBlockApplied)
            {
                return;
            }

            if (!EvaluateSecurityState())
            {
                return;
            }

            _securityMonitorHandler?.PostDelayed(_securityMonitorAction, SecurityMonitorIntervalMs);
        };
    }

    private void StartSecurityMonitor()
    {
        if (_securityBlockApplied)
        {
            return;
        }

        EnsureSecurityMonitorInitialized();
        _securityMonitorHandler?.RemoveCallbacks(_securityMonitorAction);
        _securityMonitorHandler?.PostDelayed(_securityMonitorAction, SecurityMonitorIntervalMs);
    }

    private void StopSecurityMonitor()
    {
        if (_securityMonitorHandler == null || _securityMonitorAction == null)
        {
            return;
        }

        _securityMonitorHandler.RemoveCallbacks(_securityMonitorAction);
    }

    private bool EvaluateSecurityState()
    {
        if (_securityBlockApplied)
        {
            return false;
        }

        SecurityCheckResult result = AppSecurityGuard.Check(this);
        if (result.IsAllowed)
        {
            return true;
        }

        _securityBlockApplied = true;
        StopSecurityMonitor();
        HandleSecurityBlocked(result);
        return false;
    }
}
