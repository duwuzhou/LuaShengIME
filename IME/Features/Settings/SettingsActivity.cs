using Android.App;
using Android.Content;
using Android.OS;
using IME.Features.UserLexicon;

namespace IME.Features.Settings
{
    /// <summary>
    /// Settings activity for the IME.
    /// </summary>
    [Activity(Label = "输入法设置", Theme = "@style/MyNoActionBarTheme", Exported = true)]
    public partial class SettingsActivity : Activity
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_settings);

            InitializeViews();
            InitializeLexiconServices();
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
