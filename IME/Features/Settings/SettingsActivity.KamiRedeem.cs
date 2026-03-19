using System;
using Android.Content;
using Android.Views;
using Android.Widget;

namespace IME.Features.Settings
{
    public partial class SettingsActivity
    {
        private void EnsureKamiViews()
        {
            if (_btnExportDict?.Parent is not LinearLayout maintenanceLayout)
            {
                return;
            }

            _btnKamiVipPage = new Button(this)
            {
                Text = "卡密会员"
            };
            _btnKamiVipPage.LayoutParameters = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                DpToPx(48))
            {
                TopMargin = DpToPx(8)
            };

            _btnKamiVipPage.Click += (sender, e) =>
            {
                var intent = new Intent(this, typeof(KamiVipActivity));
                StartActivity(intent);
            };

            maintenanceLayout.AddView(_btnKamiVipPage);
        }

        private int DpToPx(int dp)
        {
            float density = Resources?.DisplayMetrics?.Density ?? 1f;
            return (int)Math.Round(dp * density);
        }
    }
}
