using Android.Content;
using Android.Media;
using Android.OS;
using Android.Util;

namespace IME.Features.Input.Feedback;

public class  KeyFeedbackManager
{
    private readonly Context _context;
    private Vibrator? _vibrator;
    private AudioManager? _audioManager;

    private const int VibrateDurationMs = 20;

    public KeyFeedbackManager(Context context)
    {
        _context = context;
        Initialize();
    }

    private void Initialize()
    {
        try
        {
            _vibrator = (Vibrator)_context.GetSystemService(Context.VibratorService);
            _audioManager = (AudioManager)_context.GetSystemService(Context.AudioService);
        }
        catch (System.Exception ex)
        {
            Log.Warn("KeyFeedbackManager", $"Feedback init failed: {ex.Message}");
        }
    }

    public void PerformKeyFeedback()
    {
        PerformVibration();
        PerformSound();
    }

    private void PerformVibration()
    {
        if (!IME.Features.Settings.SettingsActivity.GetVibrateEnabled(_context)) return;
        if (_vibrator == null || !_vibrator.HasVibrator) return;

        try
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                _vibrator.Vibrate(VibrationEffect.CreateOneShot(VibrateDurationMs, VibrationEffect.DefaultAmplitude));
            }
            else
            {
#pragma warning disable CS0618
                _vibrator.Vibrate(VibrateDurationMs);
#pragma warning restore CS0618
            }
        }
        catch (System.Exception ex)
        {
            Log.Warn("KeyFeedbackManager", $"Vibration failed: {ex.Message}");
        }
    }

    private void PerformSound()
    {
        if (!IME.Features.Settings.SettingsActivity.GetSoundEnabled(_context)) return;
        if (_audioManager == null) return;

        try
        {
            _audioManager.PlaySoundEffect((Android.Media.SoundEffect)0);
        }
        catch (System.Exception ex)
        {
            Log.Warn("KeyFeedbackManager", $"Sound feedback failed: {ex.Message}");
        }
    }

    public void Cleanup()
    {
        _vibrator = null;
        _audioManager = null;
    }
}
