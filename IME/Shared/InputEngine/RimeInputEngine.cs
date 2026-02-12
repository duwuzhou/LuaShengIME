using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Android.Content;
using Android.Content.Res;
using Android.Util;
using Android.Views;
using Android.Views.InputMethods;
using IME.Shared.Abstractions;

namespace IME.Shared.InputEngine
{
    /// <summary>
    /// Rime 输入引擎实现，通过 JNI 调用 librime.so
    /// </summary>
    public class RimeInputEngine : IInputEngine
    {
        private static readonly string Tag = "RimeInputEngine";

        private IntPtr _sessionId = IntPtr.Zero;
        private bool _initialized = false;
        private bool _disposed = false;
        private bool _asciiMode = false;  // false=中文模式，true=英文模式
        private bool _simplifyLogged = false;
        private bool _useSimplified = false;
        private OpenccSimplifier? _openccSimplifier;
        private int _lastPageNo = 0;
        private bool _lastIsLastPage = false;
        private int _lastPageSize = 0;
        private bool _hasPagingInfo = false;
        private readonly List<string> _lastCandidateComments = new List<string>();

        // Rime 数据目录
        private string _sharedDataDir;
        private string _userDataDir;

        // Rime 按键修饰符
        private const int MOD_NONE = 0;
        private const int MOD_SHIFT = RimeNativeBindings.RIME_SHIFT_LSHIFT | RimeNativeBindings.RIME_SHIFT_RSHIFT;

        // Rime keycodes (rime_keycode.h / X11)
        private const int RIME_KEY_BACKSPACE = 0xFF08;
        private const int RIME_KEY_RETURN = 0xFF0D;
        private const int RIME_KEY_SHIFT_L = 0xFFE1;
        private const int RIME_KEY_SHIFT_R = 0xFFE2;

        // 静态构造函数
        // 注意：.NET for Android 在 P/Invoke 调用时会自动加载原生库
        // 无需手动调用 Java.Lang.JavaSystem.LoadLibrary()
        static RimeInputEngine()
        {
            Log.Info(Tag, "RimeInputEngine 类已加载，原生库将在首次 P/Invoke 调用时自动加载");
        }

        public bool Initialize(Context context)
        {
            if (_initialized)
            {
                Log.Info(Tag, "Rime 引擎已初始化");
                return true;
            }

            try
            {
                // 初始化 Rime API
                if (!RimeNativeBindings.InitializeApi())
                {
                    Log.Error(Tag, "无法获取 Rime API");
                    return false;
                }

                // 检查版本
                string? version = RimeNativeBindings.GetVersion();
                if (!string.IsNullOrEmpty(version))
                {
                    Log.Info(Tag, $"Rime 版本: {version}");
                }

                // 准备数据目录
                _sharedDataDir = Path.Combine(context.FilesDir.AbsolutePath, "rime", "shared");
                _userDataDir = Path.Combine(context.FilesDir.AbsolutePath, "rime", "user");

                // 确保目录存在
                Directory.CreateDirectory(_sharedDataDir);
                Directory.CreateDirectory(_userDataDir);
                IME.Features.UserLexicon.RimeUserLexiconStore.EnsureSchemaPatch(context);

                // 从 Assets 复制 Rime 数据文件到 shared 目录（首次运行或更新时）
                CopyRimeAssetsIfNeeded(context);
                InitializeOpenccSimplifier();
                bool pageSizeChanged = EnsureRimePageSize(context);

                // 创建 RimeTraits 并设置数据目录
                IntPtr traitsPtr = RimeTraitsHelper.CreateTraits(_sharedDataDir, _userDataDir);

                try
                {
                    // 设置 Rime（传递 traits）
                    RimeNativeBindings.Setup(traitsPtr);

                    // 初始化 Rime
                    RimeNativeBindings.RimeInitialize(traitsPtr);

                    // 记录 Rime 使用的数据目录，排查 opencc 路径问题
                    var sharedDir = RimeNativeBindings.GetSharedDataDir();
                    var userDir = RimeNativeBindings.GetUserDataDir();
                    Log.Info(Tag, $"RIME_DIRS shared='{sharedDir}', user='{userDir}'");
                }
                finally
                {
                    // 释放 Traits 分配的非托管内存
                    RimeTraitsHelper.FreeTraits(traitsPtr);
                }

                // 强制执行 Rime 部署（首次运行需要编译方案）
                Log.Info(Tag, "开始 Rime 部署...");
                bool startResult = RimeNativeBindings.StartMaintenance(true);
                Log.Info(Tag, $"StartMaintenance 返回: {startResult}");

                if (RimeNativeBindings.IsMaintenanceMode())
                {
                    Log.Info(Tag, "执行 Rime 部署...");
                    EnsureCleanBuildIfPreviousDeployCrashed();
                    if (pageSizeChanged)
                    {
                        TryCleanUserBuild();
                    }
                    string deployMarker = GetDeployMarkerPath();
                    try
                    {
                        File.WriteAllText(deployMarker, DateTime.UtcNow.ToString("O"), new UTF8Encoding(false));
                    }
                    catch (Exception ex)
                    {
                        Log.Warn(Tag, $"写入部署标记失败: {ex.Message}");
                    }

                    bool deployResult = RimeNativeBindings.Deploy();
                    Log.Info(Tag, $"Deploy 返回: {deployResult}");
                    RimeNativeBindings.JoinMaintenanceThread();
                    try
                    {
                        if (File.Exists(deployMarker))
                            File.Delete(deployMarker);
                    }
                    catch (Exception ex)
                    {
                        Log.Warn(Tag, $"清理部署标记失败: {ex.Message}");
                    }
                    Log.Info(Tag, "Rime 部署完成");
                }
                else
                {
                    Log.Info(Tag, "Rime 不在维护模式，跳过部署");
                }

                // 创建会话
                _sessionId = RimeNativeBindings.CreateSession();
                if (_sessionId == IntPtr.Zero)
                {
                    Log.Error(Tag, "创建 Rime 会话失败");
                    return false;
                }
                Log.Info(Tag, $"创建 Rime 会话成功: {_sessionId}");

                // 选择输入方案（luna_pinyin 是默认的拼音方案）
                if (!RimeNativeBindings.SelectSchema(_sessionId, "luna_pinyin"))
                {
                    Log.Warn(Tag, "选择 luna_pinyin 方案失败，尝试 cangjie5");
                    if (!RimeNativeBindings.SelectSchema(_sessionId, "cangjie5"))
                    {
                        Log.Error(Tag, "没有可用的输入方案");
                    }
                }

                _initialized = true;
                Log.Info(Tag, "Rime 引擎初始化成功");

                return true;
            }
            catch (System.Exception ex)
            {
                Log.Error(Tag, $"初始化 Rime 引擎失败: {ex.Message}");
                Log.Error(Tag, $"堆栈跟踪: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// 从 Assets 复制 Rime 数据文件到共享数据目录
        /// </summary>
        private void CopyRimeAssetsIfNeeded(Context context)
        {
            try
            {
                // 使用版本标记文件来判断是否需要更新
                string versionFile = Path.Combine(_sharedDataDir, ".assets_version");
                string currentVersion = GetAppVersionName(context);
                bool forceCopy = false;

                if (File.Exists(versionFile))
                {
                    string savedVersion = File.ReadAllText(versionFile, Encoding.UTF8).Trim();
                    if (savedVersion == currentVersion)
                    {
                        // 版本一致但关键目录缺失时强制补拷贝
                        string requiredOpenccDir = Path.Combine(_sharedDataDir, "opencc");
                        if (!Directory.Exists(requiredOpenccDir))
                        {
                            forceCopy = true;
                            Log.Warn(Tag, "Rime 资源版本一致但 opencc 目录缺失，强制复制资源");
                        }
                        else
                        {
                            Log.Info(Tag, "Rime 数据文件已是最新，跳过复制");
                            return;
                        }
                    }
                }

                Log.Info(Tag, "开始复制 Rime 数据文件...");

                // 递归复制 Assets/rime 目录下的所有文件和子目录
                CopyAssetDirectory(context, "rime", _sharedDataDir);

                // 写入版本标记
                if (forceCopy || !File.Exists(versionFile))
                {
                    File.WriteAllText(versionFile, currentVersion, new UTF8Encoding(false));
                }
                Log.Info(Tag, "Rime 数据文件复制完成");
            }
            catch (System.Exception ex)
            {
                Log.Error(Tag, $"复制 Rime 数据文件失败: {ex.Message}");
            }
        }

        private void InitializeOpenccSimplifier()
        {
            try
            {
                string openccDir = Path.Combine(_sharedDataDir, "opencc");
                _openccSimplifier = OpenccSimplifier.TryLoad(openccDir, msg => Log.Warn(Tag, msg));
                if (_openccSimplifier == null)
                {
                    Log.Warn(Tag, "OpenCC simplifier not available; fallback conversion disabled");
                }
                else
                {
                    Log.Info(Tag, $"OpenCC simplifier loaded from '{openccDir}'");
                }
            }
            catch (Exception ex)
            {
                Log.Warn(Tag, $"OpenCC simplifier init failed: {ex.Message}");
            }
        }

        private bool EnsureRimePageSize(Context context)
        {
            try
            {
                int pageSize = IME.Features.Settings.SettingsActivity.GetCandidatePageSize(context);
                if (pageSize <= 0)
                    return false;

                Directory.CreateDirectory(_userDataDir);
                string customPath = Path.Combine(_userDataDir, "default.custom.yaml");
                string content;
                bool changed = false;

                if (File.Exists(customPath))
                {
                    string existing = File.ReadAllText(customPath, Encoding.UTF8);
                    content = UpsertPageSizePatch(existing, pageSize);
                    if (string.Equals(existing, content, StringComparison.Ordinal))
                        return false;
                    changed = true;
                }
                else
                {
                    content = $"patch:\n  menu/page_size: {pageSize}\n";
                    changed = true;
                }

                File.WriteAllText(customPath, content, new UTF8Encoding(false));
                Log.Info(Tag, $"已写入候选分页设置: page_size={pageSize}");
                return changed;
            }
            catch (Exception ex)
            {
                Log.Warn(Tag, $"写入候选分页设置失败: {ex.Message}");
                return false;
            }
        }

        private static string UpsertPageSizePatch(string existing, int pageSize)
        {
            if (string.IsNullOrEmpty(existing))
            {
                return $"patch:\n  menu/page_size: {pageSize}\n";
            }

            string normalized = existing.Replace("\r\n", "\n");
            var lines = new List<string>(normalized.Split('\n'));

            int menuLineIndex = -1;
            int patchLineIndex = -1;

            for (int i = 0; i < lines.Count; i++)
            {
                string trimmed = lines[i].TrimStart();
                if (trimmed.StartsWith("patch:", StringComparison.Ordinal))
                {
                    patchLineIndex = i;
                }
                if (trimmed.StartsWith("menu/page_size:", StringComparison.Ordinal))
                {
                    menuLineIndex = i;
                    break;
                }
            }

            if (menuLineIndex >= 0)
            {
                string indent = lines[menuLineIndex].Substring(0, lines[menuLineIndex].Length - lines[menuLineIndex].TrimStart().Length);
                if (indent.Length == 0)
                    indent = "  ";
                lines[menuLineIndex] = $"{indent}menu/page_size: {pageSize}";
            }
            else if (patchLineIndex >= 0)
            {
                lines.Insert(patchLineIndex + 1, $"  menu/page_size: {pageSize}");
            }
            else
            {
                if (lines.Count > 0 && lines[^1].Length != 0)
                    lines.Add(string.Empty);
                lines.Add("patch:");
                lines.Add($"  menu/page_size: {pageSize}");
            }

            return string.Join("\n", lines).TrimEnd() + "\n";
        }

        private void TryCleanUserBuild()
        {
            try
            {
                string buildDir = Path.Combine(_userDataDir, "build");
                if (Directory.Exists(buildDir))
                {
                    Directory.Delete(buildDir, true);
                    Log.Warn(Tag, "检测到分页设置变更，已清理 user/build 以避免编译崩溃");
                }
            }
            catch (Exception ex)
            {
                Log.Warn(Tag, $"清理 user/build 失败: {ex.Message}");
            }
        }

        private string GetDeployMarkerPath()
        {
            return Path.Combine(_userDataDir, ".deploying");
        }

        private void EnsureCleanBuildIfPreviousDeployCrashed()
        {
            try
            {
                string marker = GetDeployMarkerPath();
                if (File.Exists(marker))
                {
                    Log.Warn(Tag, "检测到上次部署异常中断，清理 user/build 后重新部署");
                    TryCleanUserBuild();
                    File.Delete(marker);
                }
            }
            catch (Exception ex)
            {
                Log.Warn(Tag, $"检查部署标记失败: {ex.Message}");
            }
        }

        private string ApplySimplificationIfNeeded(string text)
        {
            if (!_useSimplified || _openccSimplifier == null || string.IsNullOrEmpty(text))
                return text;

            return _openccSimplifier.Convert(text);
        }

        /// <summary>
        /// 递归复制 Assets 目录到目标路径
        /// </summary>
        private void CopyAssetDirectory(Context context, string assetPath, string destPath)
        {
            // 确保目标目录存在
            Directory.CreateDirectory(destPath);

            string[] items = context.Assets.List(assetPath);
            if (items == null) return;

            foreach (string item in items)
            {
                string assetItemPath = $"{assetPath}/{item}";
                string destItemPath = Path.Combine(destPath, item);

                // 尝试判断是文件还是目录
                // Assets.List 对目录返回非空数组，对文件返回空数组或抛异常
                try
                {
                    string[] subItems = context.Assets.List(assetItemPath);
                    if (subItems != null && subItems.Length > 0)
                    {
                        // 是目录，递归复制
                        CopyAssetDirectory(context, assetItemPath, destItemPath);
                    }
                    else
                    {
                        // 是文件，直接复制
                        CopyAssetFile(context, assetItemPath, destItemPath);
                    }
                }
                catch
                {
                    // Assets.List 失败，说明是文件
                    CopyAssetFile(context, assetItemPath, destItemPath);
                }
            }
        }

        /// <summary>
        /// 复制单个 Asset 文件
        /// </summary>
        private void CopyAssetFile(Context context, string assetPath, string destPath)
        {
            try
            {
                using (var input = context.Assets.Open(assetPath))
                using (var output = new FileStream(destPath, FileMode.Create))
                {
                    input.CopyTo(output);
                }
                Log.Info(Tag, $"已复制: {assetPath}");
            }
            catch (System.Exception ex)
            {
                Log.Warn(Tag, $"复制文件失败 {assetPath}: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取应用版本名
        /// </summary>
        private string GetAppVersionName(Context context)
        {
            try
            {
                var packageInfo = context.PackageManager.GetPackageInfo(context.PackageName, 0);
                return packageInfo?.VersionName ?? "1.0.0";
            }
            catch
            {
                return "1.0.0";
            }
        }

        public bool ProcessKey(int keyCode)
        {
            Log.Info(Tag, $"ProcessKey 输入: keyCode={keyCode}, char='{(char)keyCode}'");

            if (!_initialized || _sessionId == IntPtr.Zero)
            {
                Log.Warn(Tag, $"Rime 未初始化: _initialized={_initialized}, _sessionId={_sessionId}");
                return false;
            }

            try
            {
                if (!_simplifyLogged)
                {
                    LogSimplificationState("first_key");
                    _simplifyLogged = true;
                }

                // 处理特殊按键
                if (keyCode == (int)Keycode.Del || keyCode == 8)
                {
                    // 退格键 - Rime 使用 Backspace 键码
                    return RimeNativeBindings.ProcessKey(_sessionId, RIME_KEY_BACKSPACE, MOD_NONE);
                }
                else if (keyCode == (int)Keycode.Enter || keyCode == 13)
                {
                    // 回车键 - Rime 使用 Return 键码
                    return RimeNativeBindings.ProcessKey(_sessionId, RIME_KEY_RETURN, MOD_NONE);
                }
                else if (keyCode == (int)Keycode.Space)
                {
                    // 空格键 - Rime 使用 Space 键码 (32)
                    return RimeNativeBindings.ProcessKey(_sessionId, 32, MOD_NONE);
                }
                else if (keyCode == (int)Keycode.ShiftLeft || keyCode == (int)Keycode.ShiftRight)
                {
                    // Shift 键
                    int rimeShift = keyCode == (int)Keycode.ShiftRight ? RIME_KEY_SHIFT_R : RIME_KEY_SHIFT_L;
                    return RimeNativeBindings.ProcessKey(_sessionId, rimeShift, MOD_SHIFT);
                }
                else
                {
                    // 普通字符按键
                    // 将 keyCode 转换为 Rime 键码
                    int rimeKeyCode = ConvertToRimeKeyCode(keyCode);
                    Log.Info(Tag, $"转换后的 Rime 键码: {rimeKeyCode}");

                    if (rimeKeyCode >= 0)
                    {
                        bool result = RimeNativeBindings.ProcessKey(_sessionId, rimeKeyCode, MOD_NONE);
                        Log.Info(Tag, $"RimeNativeBindings.ProcessKey 返回: {result}");
                        return result;
                    }
                    else
                    {
                        Log.Warn(Tag, $"无法转换键码: {keyCode}");
                    }
                }

                return false;
            }
            catch (System.Exception ex)
            {
                Log.Error(Tag, $"处理按键失败: {ex.Message}");
                return false;
            }
        }

        public string GetComposingText()
        {
            if (!_initialized || _sessionId == IntPtr.Zero)
                return string.Empty;

            if (!TryGetContext(out var context, out var contextPtr))
                return string.Empty;

            try
            {
                string preedit = RimeNativeBindings.PtrToStringUtf8(context.composition.preedit);
                string simplified = ApplySimplificationIfNeeded(preedit);
                Log.Info(Tag, $"GetComposingText: preedit='{simplified}'");
                return simplified;
            }
            finally
            {
                RimeNativeBindings.FreeContext(contextPtr);
                Marshal.FreeHGlobal(contextPtr);
            }
        }

        public List<string> GetCandidates()
        {
            var candidates = new List<string>();
            _lastCandidateComments.Clear();

            if (!_initialized || _sessionId == IntPtr.Zero)
                return candidates;

            if (!TryGetContext(out var context, out var contextPtr))
                return candidates;

            try
            {
                int numCandidates = context.menu.num_candidates;
                Log.Info(Tag, $"候选词数量: {numCandidates}");

                _lastPageNo = context.menu.page_no;
                _lastIsLastPage = context.menu.is_last_page != 0;
                _lastPageSize = context.menu.page_size;
                _hasPagingInfo = true;

                if (numCandidates <= 0)
                    return candidates;

                IntPtr candidatesArrayPtr = context.menu.candidates;
                if (candidatesArrayPtr == IntPtr.Zero)
                    return candidates;

                int candidateStructSize = Marshal.SizeOf<RimeNativeBindings.RimeCandidate>();
                for (int i = 0; i < numCandidates; i++)
                {
                    IntPtr candidatePtr = IntPtr.Add(candidatesArrayPtr, i * candidateStructSize);
                    var candidate = Marshal.PtrToStructure<RimeNativeBindings.RimeCandidate>(candidatePtr);
                    string text = RimeNativeBindings.PtrToStringUtf8(candidate.text);
                    string comment = RimeNativeBindings.PtrToStringUtf8(candidate.comment);
                    text = ApplySimplificationIfNeeded(text);
                    if (!string.IsNullOrEmpty(text))
                    {
                        candidates.Add(text);
                        _lastCandidateComments.Add(comment ?? string.Empty);
                    }
                }
            }
            catch (System.Exception ex)
            {
                Log.Error(Tag, $"获取候选词失败: {ex.Message}");
            }
            finally
            {
                RimeNativeBindings.FreeContext(contextPtr);
                Marshal.FreeHGlobal(contextPtr);
            }

            return candidates;
        }

        public List<string> GetCandidateComments()
        {
            return new List<string>(_lastCandidateComments);
        }

        internal List<string> GetPredictionCandidates(int maxResults)
        {
            var results = new List<string>();
            if (!_initialized || _sessionId == IntPtr.Zero || maxResults <= 0)
            {
                return results;
            }

            bool restorePredicting = false;

            try
            {
                bool predicting = RimeNativeBindings.GetOption(_sessionId, "predicting");
                if (!predicting)
                {
                    RimeNativeBindings.SetOption(_sessionId, "predicting", true);
                    restorePredicting = true;
                }

                if (!TryGetContext(out var context, out var contextPtr))
                {
                    return results;
                }

                try
                {
                    int numCandidates = context.menu.num_candidates;
                    if (numCandidates <= 0)
                    {
                        return results;
                    }

                    IntPtr candidatesArrayPtr = context.menu.candidates;
                    if (candidatesArrayPtr == IntPtr.Zero)
                    {
                        return results;
                    }

                    int candidateStructSize = Marshal.SizeOf<RimeNativeBindings.RimeCandidate>();
                    int limit = Math.Min(numCandidates, maxResults);
                    for (int i = 0; i < limit; i++)
                    {
                        IntPtr candidatePtr = IntPtr.Add(candidatesArrayPtr, i * candidateStructSize);
                        var candidate = Marshal.PtrToStructure<RimeNativeBindings.RimeCandidate>(candidatePtr);
                        string text = RimeNativeBindings.PtrToStringUtf8(candidate.text);
                        text = ApplySimplificationIfNeeded(text);
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            results.Add(text);
                        }
                    }
                }
                finally
                {
                    RimeNativeBindings.FreeContext(contextPtr);
                    Marshal.FreeHGlobal(contextPtr);
                }
            }
            catch (Exception ex)
            {
                Log.Warn(Tag, $"GetPredictionCandidates failed: {ex.Message}");
            }
            finally
            {
                if (restorePredicting)
                {
                    RimeNativeBindings.SetOption(_sessionId, "predicting", false);
                }
            }

            return results;
        }

        public string SelectCandidate(int index)
        {
            if (!_initialized || _sessionId == IntPtr.Zero)
                return string.Empty;

            try
            {
                // 选择候选词
                if (RimeNativeBindings.SelectCandidateOnCurrentPage(_sessionId, (IntPtr)index))
                {
                    // 提交候选词
                    if (RimeNativeBindings.CommitComposition(_sessionId))
                    {
                        if (TryGetCommitText(out var committedText))
                            return ApplySimplificationIfNeeded(committedText);
                    }
                }
            }
            catch (System.Exception ex)
            {
                Log.Error(Tag, $"选择候选词失败: {ex.Message}");
            }

            return string.Empty;
        }

        private bool TryGetContext(out RimeNativeBindings.RimeContext context, out IntPtr contextPtr)
        {
            context = default;
            contextPtr = IntPtr.Zero;

            int contextSize = Marshal.SizeOf<RimeNativeBindings.RimeContext>();
            contextPtr = Marshal.AllocHGlobal(contextSize);

            var init = new RimeNativeBindings.RimeContext
            {
                data_size = RimeNativeBindings.GetStructDataSize<RimeNativeBindings.RimeContext>()
            };
            Marshal.StructureToPtr(init, contextPtr, false);

            if (!RimeNativeBindings.GetContext(_sessionId, contextPtr))
            {
                Marshal.FreeHGlobal(contextPtr);
                contextPtr = IntPtr.Zero;
                return false;
            }

            context = Marshal.PtrToStructure<RimeNativeBindings.RimeContext>(contextPtr);
            return true;
        }

        private bool TryGetCommitText(out string text)
        {
            text = string.Empty;

            int commitSize = Marshal.SizeOf<RimeNativeBindings.RimeCommit>();
            IntPtr commitPtr = Marshal.AllocHGlobal(commitSize);
            bool gotCommit = false;

            try
            {
                var init = new RimeNativeBindings.RimeCommit
                {
                    data_size = RimeNativeBindings.GetStructDataSize<RimeNativeBindings.RimeCommit>()
                };
                Marshal.StructureToPtr(init, commitPtr, false);

                gotCommit = RimeNativeBindings.GetCommit(_sessionId, commitPtr);
                if (!gotCommit)
                    return false;

                var commit = Marshal.PtrToStructure<RimeNativeBindings.RimeCommit>(commitPtr);
                text = RimeNativeBindings.PtrToStringUtf8(commit.text);
                return !string.IsNullOrEmpty(text);
            }
            finally
            {
                if (gotCommit)
                    RimeNativeBindings.FreeCommit(commitPtr);
                Marshal.FreeHGlobal(commitPtr);
            }
        }

        public void Reset()
        {
            if (!_initialized || _sessionId == IntPtr.Zero)
                return;

            try
            {
                RimeNativeBindings.ClearComposition(_sessionId);
            }
            catch (System.Exception ex)
            {
                Log.Error(Tag, $"重置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 运行时切换 Rime 输入方案
        /// </summary>
        public bool SwitchSchema(string schemaId)
        {
            if (!_initialized || _sessionId == IntPtr.Zero)
            {
                Log.Warn(Tag, $"SwitchSchema 失败：引擎未初始化");
                return false;
            }

            Reset();

            bool result = RimeNativeBindings.SelectSchema(_sessionId, schemaId);
            Log.Info(Tag, $"SwitchSchema to '{schemaId}': {result}");
            return result;
        }

        public void SetAsciiMode(bool asciiMode)
        {
            if (!_initialized || _sessionId == IntPtr.Zero)
                return;

            try
            {
                _asciiMode = asciiMode;
                // Rime 使用 "ascii_mode" 选项控制中英文切换
                bool result = RimeNativeBindings.SetOption(_sessionId, "ascii_mode", asciiMode);
                if (result)
                {
                    Log.Info(Tag, $"设置 {(asciiMode ? "英文" : "中文")} 模式成功");
                }
                else
                {
                    Log.Error(Tag, $"设置 {(asciiMode ? "英文" : "中文")} 模式失败");
                }
            }
            catch (System.Exception ex)
            {
                Log.Error(Tag, $"设置中英文模式失败: {ex.Message}");
            }
        }

        public bool GetAsciiMode()
        {
            return _asciiMode;
        }

        public void SetSimplification(bool simplified)
        {
            if (!_initialized || _sessionId == IntPtr.Zero)
                return;

            try
            {
                _useSimplified = simplified;
                // cangjie5 使用 "simplification" 选项
                RimeNativeBindings.SetOption(_sessionId, "simplification", simplified);

                // luna_pinyin 的 options 组：zh_trad / zh_simp / zh_hk / zh_tw
                // 需显式互斥，避免多选导致仍输出繁体
                if (simplified)
                {
                    RimeNativeBindings.SetOption(_sessionId, "zh_simp", true);
                    RimeNativeBindings.SetOption(_sessionId, "zh_trad", false);
                    RimeNativeBindings.SetOption(_sessionId, "zh_hk", false);
                    RimeNativeBindings.SetOption(_sessionId, "zh_tw", false);
                }
                else
                {
                    RimeNativeBindings.SetOption(_sessionId, "zh_trad", true);
                    RimeNativeBindings.SetOption(_sessionId, "zh_simp", false);
                    RimeNativeBindings.SetOption(_sessionId, "zh_hk", false);
                    RimeNativeBindings.SetOption(_sessionId, "zh_tw", false);
                }

                Log.Info(Tag, $"设置{(simplified ? "简体" : "繁体")}模式成功（zh_simp/zh_trad 互斥）");
                LogSimplificationState("after_set");
            }
            catch (System.Exception ex)
            {
                Log.Error(Tag, $"设置繁简模式失败: {ex.Message}");
            }
        }

        public bool SupportsPaging => true;

        public bool TryGetPagingInfo(out int pageNo, out bool isLastPage, out int pageSize)
        {
            if (!_hasPagingInfo)
            {
                pageNo = 0;
                isLastPage = false;
                pageSize = 0;
                return false;
            }

            pageNo = _lastPageNo;
            isLastPage = _lastIsLastPage;
            pageSize = _lastPageSize;
            return true;
        }

        public bool ChangePage(bool backward)
        {
            if (!_initialized || _sessionId == IntPtr.Zero)
                return false;

            try
            {
                return RimeNativeBindings.ChangePage(_sessionId, backward);
            }
            catch (Exception ex)
            {
                Log.Warn(Tag, $"翻页失败: {ex.Message}");
                return false;
            }
        }

        private void LogSimplificationState(string stage)
        {
            try
            {
                bool simp = RimeNativeBindings.GetOption(_sessionId, "zh_simp");
                bool trad = RimeNativeBindings.GetOption(_sessionId, "zh_trad");
                bool hk = RimeNativeBindings.GetOption(_sessionId, "zh_hk");
                bool tw = RimeNativeBindings.GetOption(_sessionId, "zh_tw");
                bool simpleOption = RimeNativeBindings.GetOption(_sessionId, "simplification");

                Log.Info(Tag, $"SIMPLIFY[{stage}] opt: zh_simp={simp}, zh_trad={trad}, zh_hk={hk}, zh_tw={tw}, simplification={simpleOption}");

                if (TryGetStatus(out var status))
                {
                    Log.Info(Tag, $"SIMPLIFY[{stage}] status: simplified={status.is_simplified != 0}, traditional={status.is_traditional != 0}, ascii={status.is_ascii_mode != 0}");
                }
                else
                {
                    Log.Warn(Tag, $"SIMPLIFY[{stage}] status: unavailable");
                }
            }
            catch (System.Exception ex)
            {
                Log.Warn(Tag, $"SIMPLIFY[{stage}] log failed: {ex.Message}");
            }
        }

        private bool TryGetStatus(out RimeNativeBindings.RimeStatus status)
        {
            status = default;

            int statusSize = Marshal.SizeOf<RimeNativeBindings.RimeStatus>();
            IntPtr statusPtr = Marshal.AllocHGlobal(statusSize);
            bool gotStatus = false;

            try
            {
                var init = new RimeNativeBindings.RimeStatus
                {
                    data_size = RimeNativeBindings.GetStructDataSize<RimeNativeBindings.RimeStatus>()
                };
                Marshal.StructureToPtr(init, statusPtr, false);

                gotStatus = RimeNativeBindings.GetStatus(_sessionId, statusPtr);
                if (!gotStatus)
                    return false;

                status = Marshal.PtrToStructure<RimeNativeBindings.RimeStatus>(statusPtr);
                return true;
            }
            finally
            {
                if (gotStatus)
                    RimeNativeBindings.FreeStatus(statusPtr);
                Marshal.FreeHGlobal(statusPtr);
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            try
            {
                if (_sessionId != IntPtr.Zero)
                {
                    RimeNativeBindings.DestroySession(_sessionId);
                    _sessionId = IntPtr.Zero;
                }

                if (_initialized)
                {
                    RimeNativeBindings.RimeFinalize();
                    _initialized = false;
                }

                Log.Info(Tag, "Rime 引擎已释放");
            }
            catch (System.Exception ex)
            {
                Log.Error(Tag, $"释放 Rime 引擎失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 将 Android 按键码转换为 Rime 键码
        /// </summary>
        private int ConvertToRimeKeyCode(int androidKeyCode)
        {
            // Rime 键码定义（参考 rime.h）
            // 普通字符使用 Unicode 码点
            if (androidKeyCode >= 32 && androidKeyCode <= 126)
            {
                // 可打印 ASCII 字符
                return androidKeyCode;
            }
            else if (androidKeyCode >= (int)Keycode.A && androidKeyCode <= (int)Keycode.Z)
            {
                // A-Z 转为小写
                return 'a' + (androidKeyCode - (int)Keycode.A);
            }
            else if (androidKeyCode >= (int)Keycode.Num0 && androidKeyCode <= (int)Keycode.Num9)
            {
                // 数字 0-9
                return '0' + (androidKeyCode - (int)Keycode.Num0);
            }

            // 标点符号和特殊字符映射
            switch ((Keycode)androidKeyCode)
            {
                case Keycode.Comma: return ',';
                case Keycode.Period: return '.';
                case Keycode.Slash: return '/';
                case Keycode.Semicolon: return ';';
                case Keycode.Apostrophe: return '\'';
                case Keycode.LeftBracket: return '[';
                case Keycode.RightBracket: return ']';
                case Keycode.Backslash: return '\\';
                case Keycode.Minus: return '-';
                case Keycode.Equals: return '=';
                case Keycode.Grave: return '`';

                // 数字键盘
                case Keycode.Numpad0: return '0';
                case Keycode.Numpad1: return '1';
                case Keycode.Numpad2: return '2';
                case Keycode.Numpad3: return '3';
                case Keycode.Numpad4: return '4';
                case Keycode.Numpad5: return '5';
                case Keycode.Numpad6: return '6';
                case Keycode.Numpad7: return '7';
                case Keycode.Numpad8: return '8';
                case Keycode.Numpad9: return '9';
                case Keycode.NumpadDot: return '.';
                case Keycode.NumpadDivide: return '/';
                case Keycode.NumpadMultiply: return '*';
                case Keycode.NumpadSubtract: return '-';
                case Keycode.NumpadAdd: return '+';
                case Keycode.NumpadEquals: return '=';

                // 其他特殊字符
                case Keycode.At: return '@';
                case Keycode.Pound: return '#';
                case Keycode.Star: return '*';
                case Keycode.Plus: return '+';

                default:
                    // 未映射的按键返回 -1
                    return -1;
            }
        }
    }
}
