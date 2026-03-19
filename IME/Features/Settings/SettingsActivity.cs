using Android.App;
using Android.Content;
using Android.OS;
using IME.Features.UserLexicon;
using IME.Shared.ResourceProtection;
using IME.Shared.Security;

namespace IME.Features.Settings
{
    /// <summary>
    /// Settings activity for the IME.
    /// </summary>
    [Activity(Label = "输入法设置", Theme = "@style/MyNoActionBarTheme", Exported = true)]
    public partial class SettingsActivity : SecurityMonitoredActivity
    {
        protected override void OnResume()
        {
            base.OnResume();
            KamiVipVerificationCoordinator.Start(this, nameof(SettingsActivity), ApplyPremiumFeatureAvailability);
        }

        protected override void OnPause()
        {
            KamiVipVerificationCoordinator.Stop();
            base.OnPause();
        }

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            if (!EnsureSecurityAllowedNow())
            {
                return;
            }
#if DEBUG
            SetContentView(Resource.Layout.activity_settings);
#else
            SetContentView(EncryptedLayout.Inflate(this, "layout/activity_settings", Resource.Layout.activity_settings));
#endif

            InitializeViews();
            InitializeLexiconServices();
            ApplyPremiumFeatureAvailability();
            LoadSettings();
            SetupEventHandlers();
            LoadVersionInfo();
        }

        protected override void OnDestroy()
        {
            _localLexiconStore?.Dispose();
            _localLexiconStore = null;
            _rimeLexiconStore = null;
            base.OnDestroy();
        }
    }
}
