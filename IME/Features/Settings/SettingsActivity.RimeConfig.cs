using System;
using System.IO;
using Android.Util;
using IME.Features.UserLexicon;

namespace IME.Features.Settings
{
    public partial class SettingsActivity
    {
        private void WriteCandidatePageSizePatch(int pageSize)
        {
            try
            {
                RimeConfigPatcher.TryWritePageSizePatch(this, pageSize);
            }
            catch (Exception ex)
            {
                Log.Warn(Tag, $"写入候选分页设置失败: {ex.Message}");
            }
        }

        private void TryCleanUserBuild()
        {
            try
            {
                string buildDir = Path.Combine(FilesDir.AbsolutePath, "rime", "user", "build");
                if (Directory.Exists(buildDir))
                {
                    Directory.Delete(buildDir, true);
                }
            }
            catch (Exception ex)
            {
                Log.Warn(Tag, $"清理 user/build 失败: {ex.Message}");
            }
        }
    }
}
