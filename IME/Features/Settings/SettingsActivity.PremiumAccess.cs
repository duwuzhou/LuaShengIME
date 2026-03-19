using Android.Views;

namespace IME.Features.Settings
{
    public partial class SettingsActivity
    {
        private void ApplyPremiumFeatureAvailability()
        {
            MarkPremiumButton(_btnPredictionManage, KamiVipConfig.CanUseCustomPrediction(this));
            MarkPremiumButton(_btnShortcutManage, KamiVipConfig.CanUseCustomShortcuts(this));
            MarkPremiumButton(_btnImportLexicon, KamiVipConfig.CanImportUserLexicon(this));
        }

        private static void MarkPremiumButton(Android.Widget.Button button, bool isAvailable)
        {
            if (button == null || isAvailable)
            {
                return;
            }

            string text = button.Text ?? string.Empty;
            if (!text.Contains("会员", System.StringComparison.Ordinal))
            {
                button.Text = text + "（会员）";
            }

            button.Alpha = 0.78f;
        }
    }
}
