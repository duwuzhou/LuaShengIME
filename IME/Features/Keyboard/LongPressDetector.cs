using Android.OS;
using Android.Util;
using Java.Lang;
using System;

namespace IME.Features.Keyboard;

/// <summary>
/// 长按检测器 - 负责检测和处理键盘长按事件
/// </summary>
public class LongPressDetector : IDisposable
{
    private Handler _handler;
    private Runnable _longPressRunnable;
    private bool _isDisposed;

    /// <summary>
    /// 长按触发延迟（毫秒）
    /// </summary>
    public int LongPressDelay { get; set; } = 1000;

    /// <summary>
    /// 长按事件触发时的回调
    /// </summary>
    public event Action<int> OnLongPress;

    public LongPressDetector()
    {
        _handler = new Handler(Looper.MainLooper);
    }

    /// <summary>
    /// 开始检测长按
    /// </summary>
    /// <param name="keyCode">按键码</param>
    public void StartDetection(int keyCode)
    {
        if (_isDisposed) return;

        // 取消之前的检测
        CancelDetection();

        _longPressRunnable = new Runnable(() =>
        {
            Log.Debug("LongPressDetector", $"长按事件触发: keyCode={keyCode}");
            OnLongPress?.Invoke(keyCode);
        });

        _handler?.PostDelayed(_longPressRunnable, LongPressDelay);
    }

    /// <summary>
    /// 取消长按检测
    /// </summary>
    public void CancelDetection()
    {
        if (_longPressRunnable != null && _handler != null)
        {
            _handler.RemoveCallbacks(_longPressRunnable);
            _longPressRunnable = null;
        }
    }

    /// <summary>
    /// 清理资源
    /// </summary>
    public void Cleanup()
    {
        CancelDetection();

        if (_handler != null)
        {
            _handler.RemoveCallbacksAndMessages(null);
            _handler = null;
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        Cleanup();
    }
}
