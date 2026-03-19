using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Android.Util;

namespace IME.Shared.InputEngine
{
    /// <summary>
    /// Rime API 原生绑定，通过 JNI 调用 librime.so
    /// 使用函数指针 API 模式 (RimeApi*)
    /// </summary>
    internal static class RimeNativeBindings
    {
        private const string LIBRIME = "rime";

        // Rime 初始化标志
        public const int RIME_SHIFT_LSHIFT = 1 << 0;
        public const int RIME_SHIFT_RSHIFT = 1 << 1;
        public const int RIME_SHIFT_LCTRL = 1 << 2;
        public const int RIME_SHIFT_RCTRL = 1 << 3;
        public const int RIME_SHIFT_LALT = 1 << 4;
        public const int RIME_SHIFT_RALT = 1 << 5;
        public const int RIME_SHIFT_LMETA = 1 << 8;
        public const int RIME_SHIFT_RMETA = 1 << 9;

        // 静态 API 指针（在 Initialize 时获取）
        private static IntPtr _apiPtr = IntPtr.Zero;

        // RimeApi 结构体函数指针偏移量（64位系统，每个指针8字节）
        // 第一个成员 data_size 是 int (4字节) + 4字节 padding = 8字节
        // 所以函数指针从 offset 8 开始
        private static class ApiOffset
        {
            public const int Setup = 1;                      // offset 8
            public const int SetNotificationHandler = 2;     // offset 16
            public const int Initialize = 3;                 // offset 24
            public const int Finalize = 4;                   // offset 32
            public const int StartMaintenance = 5;           // offset 40
            public const int IsMaintenanceMode = 6;          // offset 48
            public const int JoinMaintenanceThread = 7;      // offset 56
            public const int DeployerInitialize = 8;         // offset 64
            public const int Prebuild = 9;                   // offset 72
            public const int Deploy = 10;                    // offset 80
            public const int DeploySchema = 11;              // offset 88
            public const int DeployConfigFile = 12;          // offset 96
            public const int SyncUserData = 13;              // offset 104
            public const int CreateSession = 14;             // offset 112
            public const int FindSession = 15;               // offset 120
            public const int DestroySession = 16;            // offset 128
            public const int CleanupStaleSessions = 17;      // offset 136
            public const int CleanupAllSessions = 18;        // offset 144
            public const int ProcessKey = 19;                // offset 152
            public const int CommitComposition = 20;         // offset 160
            public const int ClearComposition = 21;          // offset 168
            public const int GetCommit = 22;                 // offset 176
            public const int FreeCommit = 23;                // offset 184
            public const int GetContext = 24;                // offset 192
            public const int FreeContext = 25;               // offset 200
            public const int GetStatus = 26;                 // offset 208
            public const int FreeStatus = 27;                // offset 216
            public const int SetOption = 28;                 // offset 224
            public const int GetOption = 29;                 // offset 232
            public const int SetProperty = 30;               // offset 240
            public const int GetProperty = 31;               // offset 248
            public const int GetSchemaList = 32;             // offset 256
            public const int FreeSchemaList = 33;            // offset 264
            public const int GetCurrentSchema = 34;          // offset 272
            public const int SelectSchema = 35;              // offset 280
            public const int SchemaOpen = 36;                // offset 288
            public const int ConfigOpen = 37;                // offset 296
            public const int ConfigClose = 38;               // offset 304
            public const int ConfigGetBool = 39;             // offset 312
            public const int ConfigGetInt = 40;              // offset 320
            public const int ConfigGetDouble = 41;           // offset 328
            public const int ConfigGetString = 42;           // offset 336
            public const int ConfigGetCstring = 43;          // offset 344
            public const int ConfigUpdateSignature = 44;     // offset 352
            public const int ConfigBeginMap = 45;            // offset 360
            public const int ConfigNext = 46;                // offset 368
            public const int ConfigEnd = 47;                 // offset 376
            public const int SimulateKeySequence = 48;       // offset 384
            public const int RegisterModule = 49;            // offset 392
            public const int FindModule = 50;                // offset 400
            public const int RunTask = 51;                   // offset 408
            public const int GetSharedDataDir = 52;          // offset 416
            public const int GetUserDataDir = 53;            // offset 424
            public const int GetSyncDir = 54;                // offset 432
            public const int GetUserId = 55;                 // offset 440
            public const int GetUserDataSyncDir = 56;        // offset 448
            public const int ConfigInit = 57;                // offset 456
            public const int ConfigLoadString = 58;          // offset 464
            public const int ConfigSetBool = 59;             // offset 472
            public const int ConfigSetInt = 60;              // offset 480
            public const int ConfigSetDouble = 61;           // offset 488
            public const int ConfigSetString = 62;           // offset 496
            public const int ConfigGetItem = 63;             // offset 504
            public const int ConfigSetItem = 64;             // offset 512
            public const int ConfigClear = 65;               // offset 520
            public const int ConfigCreateList = 66;          // offset 528
            public const int ConfigCreateMap = 67;           // offset 536
            public const int ConfigListSize = 68;            // offset 544
            public const int ConfigBeginList = 69;           // offset 552
            public const int GetInput = 70;                  // offset 560
            public const int GetCaretPos = 71;               // offset 568
            public const int SelectCandidate = 72;           // offset 576
            public const int GetVersion = 73;                // offset 584
            public const int SetCaretPos = 74;               // offset 592
            public const int SelectCandidateOnCurrentPage = 75;  // offset 600
            public const int CandidateListBegin = 76;        // offset 608
            public const int CandidateListNext = 77;         // offset 616
            public const int CandidateListEnd = 78;          // offset 624
            public const int ChangePage = 98;                // offset 784
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct RimeTraits
        {
            public int data_size;
            public IntPtr shared_data_dir;
            public IntPtr user_data_dir;
            public IntPtr distribution_name;
            public IntPtr distribution_code_name;
            public IntPtr distribution_version;
            public IntPtr app_name;
            public IntPtr modules;
            public int min_log_level;
            public IntPtr log_dir;
            public IntPtr prebuilt_data_dir;
            public IntPtr staging_dir;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct RimeComposition
        {
            public int length;
            public int cursor_pos;
            public int sel_start;
            public int sel_end;
            public IntPtr preedit;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct RimeCandidate
        {
            public IntPtr text;
            public IntPtr comment;
            public IntPtr reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct RimeMenu
        {
            public int page_size;
            public int page_no;
            public int is_last_page;
            public int highlighted_candidate_index;
            public int num_candidates;
            public IntPtr candidates;
            public IntPtr select_keys;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct RimeCommit
        {
            public int data_size;
            public IntPtr text;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct RimeContext
        {
            public int data_size;
            public RimeComposition composition;
            public RimeMenu menu;
            public IntPtr commit_text_preview;
            public IntPtr select_labels;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct RimeStatus
        {
            public int data_size;
            public IntPtr schema_id;
            public IntPtr schema_name;
            public int is_disabled;
            public int is_composing;
            public int is_ascii_mode;
            public int is_full_shape;
            public int is_simplified;
            public int is_traditional;
            public int is_ascii_punct;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct RimeCandidateListIterator
        {
            public IntPtr ptr;
            public int index;
            public RimeCandidate candidate;
        }

        internal static int GetStructDataSize<T>() where T : struct
        {
            return Marshal.SizeOf<T>() - sizeof(int);
        }

        /// <summary>
        /// 获取 Rime API 指针
        /// </summary>
        [DllImport(LIBRIME, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr rime_get_api();

        /// <summary>
        /// 读取函数指针
        /// </summary>
        private static IntPtr GetFunctionPointer(int index)
        {
            if (_apiPtr == IntPtr.Zero)
                return IntPtr.Zero;
            return Marshal.ReadIntPtr(_apiPtr, index * IntPtr.Size);
        }

        /// <summary>
        /// 公开的函数指针获取方法
        /// </summary>
        public static IntPtr GetApiFunctionPointer(int index)
        {
            return GetFunctionPointer(index);
        }

        public static IntPtr FindModule(string moduleName)
        {
            if (_apiPtr == IntPtr.Zero || string.IsNullOrEmpty(moduleName))
            {
                return IntPtr.Zero;
            }

            try
            {
                IntPtr funcPtr = GetFunctionPointer(ApiOffset.FindModule);
                if (funcPtr != IntPtr.Zero)
                {
                    var func = Marshal.GetDelegateForFunctionPointer<FindModuleDelegate>(funcPtr);
                    return func(moduleName);
                }
            }
            catch (Exception ex)
            {
                Log.Error("RimeNativeBindings", $"FindModule 失败: {ex.Message}");
            }

            return IntPtr.Zero;
        }

        /// <summary>
        /// 初始化 API 结构体
        /// </summary>
        public static bool InitializeApi()
        {
            if (_apiPtr != IntPtr.Zero)
                return true;

            try
            {
                _apiPtr = rime_get_api();
                if (_apiPtr == IntPtr.Zero)
                {
                    Log.Error("RimeNativeBindings", "无法获取 Rime API");
                    return false;
                }

                Log.Info("RimeNativeBindings", $"Rime API 指针获取成功: {_apiPtr}");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("RimeNativeBindings", $"获取 Rime API 失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取 API 版本号
        /// </summary>
        public static string? GetVersion()
        {
            if (_apiPtr == IntPtr.Zero)
                return null;

            try
            {
                IntPtr funcPtr = GetFunctionPointer(ApiOffset.GetVersion);
                if (funcPtr == IntPtr.Zero)
                    return null;

                var func = Marshal.GetDelegateForFunctionPointer<GetVersionDelegate>(funcPtr);
                IntPtr versionPtr = func();
                return PtrToStringUtf8(versionPtr);
            }
            catch (Exception ex)
            {
                Log.Error("RimeNativeBindings", $"获取版本失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 初始化 Rime
        /// </summary>
        public static void Setup(IntPtr traits)
        {
            if (_apiPtr == IntPtr.Zero)
                return;

            try
            {
                IntPtr funcPtr = GetFunctionPointer(ApiOffset.Setup);
                if (funcPtr != IntPtr.Zero)
                {
                    var func = Marshal.GetDelegateForFunctionPointer<SetupDelegate>(funcPtr);
                    func(traits);
                }
            }
            catch (Exception ex)
            {
                Log.Error("RimeNativeBindings", $"Setup 失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 初始化 Rime
        /// </summary>
        public static void RimeInitialize(IntPtr traits)
        {
            if (_apiPtr == IntPtr.Zero)
                return;

            try
            {
                IntPtr funcPtr = GetFunctionPointer(ApiOffset.Initialize);
                if (funcPtr != IntPtr.Zero)
                {
                    var func = Marshal.GetDelegateForFunctionPointer<InitializeDelegate>(funcPtr);
                    func(traits);
                }
            }
            catch (Exception ex)
            {
                Log.Error("RimeNativeBindings", $"Initialize 失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 释放 Rime
        /// </summary>
        public static void RimeFinalize()
        {
            if (_apiPtr == IntPtr.Zero)
                return;

            try
            {
                IntPtr funcPtr = GetFunctionPointer(ApiOffset.Finalize);
                if (funcPtr != IntPtr.Zero)
                {
                    var func = Marshal.GetDelegateForFunctionPointer<FinalizeDelegate>(funcPtr);
                    func();
                }
            }
            catch (Exception ex)
            {
                Log.Error("RimeNativeBindings", $"Finalize 失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 创建会话
        /// </summary>
        public static IntPtr CreateSession()
        {
            if (_apiPtr == IntPtr.Zero)
                return IntPtr.Zero;

            try
            {
                IntPtr funcPtr = GetFunctionPointer(ApiOffset.CreateSession);
                if (funcPtr != IntPtr.Zero)
                {
                    var func = Marshal.GetDelegateForFunctionPointer<CreateSessionDelegate>(funcPtr);
                    return func();
                }
            }
            catch (Exception ex)
            {
                Log.Error("RimeNativeBindings", $"CreateSession 失败: {ex.Message}");
            }
            return IntPtr.Zero;
        }

        /// <summary>
        /// 销毁会话
        /// </summary>
        public static bool DestroySession(IntPtr sessionId)
        {
            if (_apiPtr == IntPtr.Zero)
                return false;

            try
            {
                IntPtr funcPtr = GetFunctionPointer(ApiOffset.DestroySession);
                if (funcPtr != IntPtr.Zero)
                {
                    var func = Marshal.GetDelegateForFunctionPointer<DestroySessionDelegate>(funcPtr);
                    return func(sessionId);
                }
            }
            catch (Exception ex)
            {
                Log.Error("RimeNativeBindings", $"DestroySession 失败: {ex.Message}");
            }
            return false;
        }

        /// <summary>
        /// 处理按键
        /// </summary>
        public static bool ProcessKey(IntPtr sessionId, int keycode, int mask)
        {
            Log.Info("RimeNativeBindings", $"ProcessKey: sessionId={sessionId}, keycode={keycode}, mask={mask}");

            if (_apiPtr == IntPtr.Zero)
            {
                Log.Warn("RimeNativeBindings", "ProcessKey: _apiPtr 为空");
                return false;
            }

            try
            {
                IntPtr funcPtr = GetFunctionPointer(ApiOffset.ProcessKey);
                Log.Info("RimeNativeBindings", $"ProcessKey funcPtr: {funcPtr}");

                if (funcPtr != IntPtr.Zero)
                {
                    var func = Marshal.GetDelegateForFunctionPointer<ProcessKeyDelegate>(funcPtr);
                    bool result = func(sessionId, keycode, mask);
                    Log.Info("RimeNativeBindings", $"ProcessKey native 返回: {result}");
                    return result;
                }
                else
                {
                    Log.Warn("RimeNativeBindings", "ProcessKey: funcPtr 为空");
                }
            }
            catch (Exception ex)
            {
                Log.Error("RimeNativeBindings", $"ProcessKey 失败: {ex.Message}");
            }
            return false;
        }

        /// <summary>
        /// 提交组合文本
        /// </summary>
        public static bool CommitComposition(IntPtr sessionId)
        {
            if (_apiPtr == IntPtr.Zero)
                return false;

            try
            {
                IntPtr funcPtr = GetFunctionPointer(ApiOffset.CommitComposition);
                if (funcPtr != IntPtr.Zero)
                {
                    var func = Marshal.GetDelegateForFunctionPointer<CommitCompositionDelegate>(funcPtr);
                    return func(sessionId);
                }
            }
            catch (Exception ex)
            {
                Log.Error("RimeNativeBindings", $"CommitComposition 失败: {ex.Message}");
            }
            return false;
        }

        /// <summary>
        /// 清除组合文本
        /// </summary>
        public static void ClearComposition(IntPtr sessionId)
        {
            if (_apiPtr == IntPtr.Zero)
                return;

            try
            {
                IntPtr funcPtr = GetFunctionPointer(ApiOffset.ClearComposition);
                if (funcPtr != IntPtr.Zero)
                {
                    var func = Marshal.GetDelegateForFunctionPointer<ClearCompositionDelegate>(funcPtr);
                    func(sessionId);
                }
            }
            catch (Exception ex)
            {
                Log.Error("RimeNativeBindings", $"ClearComposition 失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 选择输入方案
        /// </summary>
        public static bool SelectSchema(IntPtr sessionId, string schemaId)
        {
            if (_apiPtr == IntPtr.Zero)
                return false;

            try
            {
                IntPtr funcPtr = GetFunctionPointer(ApiOffset.SelectSchema);
                Log.Info("RimeNativeBindings", $"SelectSchema: sessionId={sessionId}, schemaId={schemaId}, funcPtr={funcPtr}");

                if (funcPtr != IntPtr.Zero)
                {
                    var func = Marshal.GetDelegateForFunctionPointer<SelectSchemaDelegate>(funcPtr);
                    bool result = func(sessionId, schemaId);
                    Log.Info("RimeNativeBindings", $"SelectSchema 返回: {result}");
                    return result;
                }
            }
            catch (Exception ex)
            {
                Log.Error("RimeNativeBindings", $"SelectSchema 失败: {ex.Message}");
            }
            return false;
        }

        /// <summary>
        /// 获取提交内容
        /// </summary>
        public static bool GetCommit(IntPtr sessionId, IntPtr commit)
        {
            if (_apiPtr == IntPtr.Zero)
                return false;

            try
            {
                IntPtr funcPtr = GetFunctionPointer(ApiOffset.GetCommit);
                if (funcPtr != IntPtr.Zero)
                {
                    var func = Marshal.GetDelegateForFunctionPointer<GetCommitDelegate>(funcPtr);
                    return func(sessionId, commit);
                }
            }
            catch (Exception ex)
            {
                Log.Error("RimeNativeBindings", $"GetCommit 失败: {ex.Message}");
            }
            return false;
        }

        /// <summary>
        /// 释放提交内容
        /// </summary>
        public static bool FreeCommit(IntPtr commit)
        {
            if (_apiPtr == IntPtr.Zero)
                return false;

            try
            {
                IntPtr funcPtr = GetFunctionPointer(ApiOffset.FreeCommit);
                if (funcPtr != IntPtr.Zero)
                {
                    var func = Marshal.GetDelegateForFunctionPointer<FreeCommitDelegate>(funcPtr);
                    return func(commit);
                }
            }
            catch (Exception ex)
            {
                Log.Error("RimeNativeBindings", $"FreeCommit 失败: {ex.Message}");
            }
            return false;
        }

        /// <summary>
        /// 获取上下文
        /// </summary>
        public static bool GetContext(IntPtr sessionId, IntPtr context)
        {
            if (_apiPtr == IntPtr.Zero)
                return false;

            try
            {
                IntPtr funcPtr = GetFunctionPointer(ApiOffset.GetContext);
                if (funcPtr != IntPtr.Zero)
                {
                    var func = Marshal.GetDelegateForFunctionPointer<GetContextDelegate>(funcPtr);
                    return func(sessionId, context);
                }
            }
            catch (Exception ex)
            {
                Log.Error("RimeNativeBindings", $"GetContext 失败: {ex.Message}");
            }
            return false;
        }

        /// <summary>
        /// 释放上下文
        /// </summary>
        public static bool FreeContext(IntPtr context)
        {
            if (_apiPtr == IntPtr.Zero)
                return false;

            try
            {
                IntPtr funcPtr = GetFunctionPointer(ApiOffset.FreeContext);
                if (funcPtr != IntPtr.Zero)
                {
                    var func = Marshal.GetDelegateForFunctionPointer<FreeContextDelegate>(funcPtr);
                    return func(context);
                }
            }
            catch (Exception ex)
            {
                Log.Error("RimeNativeBindings", $"FreeContext 失败: {ex.Message}");
            }
            return false;
        }

        /// <summary>
        /// 获取状态
        /// </summary>
        public static bool GetStatus(IntPtr sessionId, IntPtr status)
        {
            if (_apiPtr == IntPtr.Zero)
                return false;

            try
            {
                IntPtr funcPtr = GetFunctionPointer(ApiOffset.GetStatus);
                if (funcPtr != IntPtr.Zero)
                {
                    var func = Marshal.GetDelegateForFunctionPointer<GetStatusDelegate>(funcPtr);
                    return func(sessionId, status);
                }
            }
            catch (Exception ex)
            {
                Log.Error("RimeNativeBindings", $"GetStatus 失败: {ex.Message}");
            }
            return false;
        }

        /// <summary>
        /// 释放状态
        /// </summary>
        public static bool FreeStatus(IntPtr status)
        {
            if (_apiPtr == IntPtr.Zero)
                return false;

            try
            {
                IntPtr funcPtr = GetFunctionPointer(ApiOffset.FreeStatus);
                if (funcPtr != IntPtr.Zero)
                {
                    var func = Marshal.GetDelegateForFunctionPointer<FreeStatusDelegate>(funcPtr);
                    return func(status);
                }
            }
            catch (Exception ex)
            {
                Log.Error("RimeNativeBindings", $"FreeStatus 失败: {ex.Message}");
            }
            return false;
        }

        /// <summary>
        /// 设置选项
        /// </summary>
        public static bool SetOption(IntPtr sessionId, string option, bool value)
        {
            if (_apiPtr == IntPtr.Zero)
                return false;

            try
            {
                IntPtr funcPtr = GetFunctionPointer(ApiOffset.SetOption);
                if (funcPtr != IntPtr.Zero)
                {
                    var func = Marshal.GetDelegateForFunctionPointer<SetOptionDelegate>(funcPtr);
                    func(sessionId, option, value ? 1 : 0);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log.Error("RimeNativeBindings", $"SetOption 失败: {ex.Message}");
            }
            return false;
        }

        /// <summary>
        /// 获取选项
        /// </summary>
        public static bool GetOption(IntPtr sessionId, string option)
        {
            if (_apiPtr == IntPtr.Zero)
                return false;

            try
            {
                IntPtr funcPtr = GetFunctionPointer(ApiOffset.GetOption);
                if (funcPtr != IntPtr.Zero)
                {
                    var func = Marshal.GetDelegateForFunctionPointer<GetOptionDelegate>(funcPtr);
                    return func(sessionId, option);
                }
            }
            catch (Exception ex)
            {
                Log.Error("RimeNativeBindings", $"GetOption 失败: {ex.Message}");
            }
            return false;
        }

        /// <summary>
        /// 选择当前页候选词
        /// </summary>
        public static bool SelectCandidateOnCurrentPage(IntPtr sessionId, IntPtr index)
        {
            if (_apiPtr == IntPtr.Zero)
                return false;

            try
            {
                IntPtr funcPtr = GetFunctionPointer(ApiOffset.SelectCandidateOnCurrentPage);
                if (funcPtr != IntPtr.Zero)
                {
                    var func = Marshal.GetDelegateForFunctionPointer<SelectCandidateOnCurrentPageDelegate>(funcPtr);
                    return func(sessionId, (ulong)index);
                }
            }
            catch (Exception ex)
            {
                Log.Error("RimeNativeBindings", $"SelectCandidateOnCurrentPage 失败: {ex.Message}");
            }
            return false;
        }

        /// <summary>
        /// 候选词列表开始
        /// </summary>
        public static bool CandidateListBegin(IntPtr sessionId, IntPtr iterator)
        {
            if (_apiPtr == IntPtr.Zero)
                return false;

            try
            {
                IntPtr funcPtr = GetFunctionPointer(ApiOffset.CandidateListBegin);
                if (funcPtr != IntPtr.Zero)
                {
                    var func = Marshal.GetDelegateForFunctionPointer<CandidateListBeginDelegate>(funcPtr);
                    return func(sessionId, iterator);
                }
            }
            catch (Exception ex)
            {
                Log.Error("RimeNativeBindings", $"CandidateListBegin 失败: {ex.Message}");
            }
            return false;
        }

        /// <summary>
        /// 候选词列表下一项
        /// </summary>
        public static bool CandidateListNext(IntPtr iterator)
        {
            if (_apiPtr == IntPtr.Zero)
                return false;

            try
            {
                IntPtr funcPtr = GetFunctionPointer(ApiOffset.CandidateListNext);
                if (funcPtr != IntPtr.Zero)
                {
                    var func = Marshal.GetDelegateForFunctionPointer<CandidateListNextDelegate>(funcPtr);
                    return func(iterator);
                }
            }
            catch (Exception ex)
            {
                Log.Error("RimeNativeBindings", $"CandidateListNext 失败: {ex.Message}");
            }
            return false;
        }

        /// <summary>
        /// 候选词列表结束
        /// </summary>
        public static void CandidateListEnd(IntPtr iterator)
        {
            if (_apiPtr == IntPtr.Zero || iterator == IntPtr.Zero)
                return;

            try
            {
                IntPtr funcPtr = GetFunctionPointer(ApiOffset.CandidateListEnd);
                if (funcPtr != IntPtr.Zero)
                {
                    var func = Marshal.GetDelegateForFunctionPointer<CandidateListEndDelegate>(funcPtr);
                    func(iterator);
                }
            }
            catch (Exception ex)
            {
                Log.Error("RimeNativeBindings", $"CandidateListEnd 失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 翻页
        /// </summary>
        public static bool ChangePage(IntPtr sessionId, bool backward)
        {
            if (_apiPtr == IntPtr.Zero)
                return false;

            try
            {
                IntPtr funcPtr = GetFunctionPointer(ApiOffset.ChangePage);
                if (funcPtr != IntPtr.Zero)
                {
                    var func = Marshal.GetDelegateForFunctionPointer<ChangePageDelegate>(funcPtr);
                    return func(sessionId, backward ? 1 : 0);
                }
            }
            catch (Exception ex)
            {
                Log.Error("RimeNativeBindings", $"ChangePage 失败: {ex.Message}");
            }
            return false;
        }

        /// <summary>
        /// 模拟按键序列，作为 process_key 的兼容兜底。
        /// </summary>
        public static bool SimulateKeySequence(IntPtr sessionId, string keySequence)
        {
            if (_apiPtr == IntPtr.Zero || string.IsNullOrEmpty(keySequence))
                return false;

            try
            {
                IntPtr funcPtr = GetFunctionPointer(ApiOffset.SimulateKeySequence);
                if (funcPtr != IntPtr.Zero)
                {
                    var func = Marshal.GetDelegateForFunctionPointer<SimulateKeySequenceDelegate>(funcPtr);
                    bool result = func(sessionId, keySequence);
                    Log.Info("RimeNativeBindings", $"SimulateKeySequence: sessionId={sessionId}, sequence={keySequence}, result={result}");
                    return result;
                }
            }
            catch (Exception ex)
            {
                Log.Error("RimeNativeBindings", $"SimulateKeySequence 失败: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// 启动维护模式（强制部署检查）
        /// </summary>
        public static bool StartMaintenance(bool fullCheck)
        {
            if (_apiPtr == IntPtr.Zero)
                return false;

            try
            {
                IntPtr funcPtr = GetFunctionPointer(ApiOffset.StartMaintenance);
                if (funcPtr != IntPtr.Zero)
                {
                    var func = Marshal.GetDelegateForFunctionPointer<StartMaintenanceDelegate>(funcPtr);
                    return func(fullCheck);
                }
            }
            catch (Exception ex)
            {
                Log.Error("RimeNativeBindings", $"StartMaintenance 失败: {ex.Message}");
            }
            return false;
        }

        /// <summary>
        /// 检查是否处于维护模式
        /// </summary>
        public static bool IsMaintenanceMode()
        {
            if (_apiPtr == IntPtr.Zero)
                return false;

            try
            {
                IntPtr funcPtr = GetFunctionPointer(ApiOffset.IsMaintenanceMode);
                if (funcPtr != IntPtr.Zero)
                {
                    var func = Marshal.GetDelegateForFunctionPointer<IsMaintenanceModeDelegate>(funcPtr);
                    return func();
                }
            }
            catch (Exception ex)
            {
                Log.Error("RimeNativeBindings", $"IsMaintenanceMode 失败: {ex.Message}");
            }
            return false;
        }

        /// <summary>
        /// 部署
        /// </summary>
        public static bool Deploy()
        {
            if (_apiPtr == IntPtr.Zero)
                return false;

            try
            {
                IntPtr funcPtr = GetFunctionPointer(ApiOffset.Deploy);
                if (funcPtr != IntPtr.Zero)
                {
                    var func = Marshal.GetDelegateForFunctionPointer<DeployDelegate>(funcPtr);
                    return func();
                }
            }
            catch (Exception ex)
            {
                Log.Error("RimeNativeBindings", $"Deploy 失败: {ex.Message}");
            }
            return false;
        }

        /// <summary>
        /// 等待维护线程完成
        /// </summary>
        public static void JoinMaintenanceThread()
        {
            if (_apiPtr == IntPtr.Zero)
                return;

            try
            {
                IntPtr funcPtr = GetFunctionPointer(ApiOffset.JoinMaintenanceThread);
                if (funcPtr != IntPtr.Zero)
                {
                    var func = Marshal.GetDelegateForFunctionPointer<JoinMaintenanceThreadDelegate>(funcPtr);
                    func();
                }
            }
            catch (Exception ex)
            {
                Log.Error("RimeNativeBindings", $"JoinMaintenanceThread 失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 同步用户数据
        /// </summary>
        public static bool SyncUserData()
        {
            if (_apiPtr == IntPtr.Zero)
                return false;

            try
            {
                IntPtr funcPtr = GetFunctionPointer(ApiOffset.SyncUserData);
                if (funcPtr != IntPtr.Zero)
                {
                    var func = Marshal.GetDelegateForFunctionPointer<SyncUserDataDelegate>(funcPtr);
                    return func();
                }
            }
            catch (Exception ex)
            {
                Log.Error("RimeNativeBindings", $"SyncUserData 失败: {ex.Message}");
            }
            return false;
        }

        /// <summary>
        /// 获取用户数据目录
        /// </summary>
        public static string GetUserDataDir()
        {
            if (_apiPtr == IntPtr.Zero)
                return string.Empty;

            try
            {
                IntPtr funcPtr = GetFunctionPointer(ApiOffset.GetUserDataDir);
                if (funcPtr != IntPtr.Zero)
                {
                    var func = Marshal.GetDelegateForFunctionPointer<GetUserDataDirDelegate>(funcPtr);
                    IntPtr dirPtr = func();
                    return PtrToStringUtf8(dirPtr);
                }
            }
            catch (Exception ex)
            {
                Log.Error("RimeNativeBindings", $"GetUserDataDir 失败: {ex.Message}");
            }
            return string.Empty;
        }

        /// <summary>
        /// 获取同步目录
        /// </summary>
        public static string GetSyncDir()
        {
            if (_apiPtr == IntPtr.Zero)
                return string.Empty;

            try
            {
                IntPtr funcPtr = GetFunctionPointer(ApiOffset.GetSyncDir);
                if (funcPtr != IntPtr.Zero)
                {
                    var func = Marshal.GetDelegateForFunctionPointer<GetSyncDirDelegate>(funcPtr);
                    IntPtr dirPtr = func();
                    return PtrToStringUtf8(dirPtr);
                }
            }
            catch (Exception ex)
            {
                Log.Error("RimeNativeBindings", $"GetSyncDir 失败: {ex.Message}");
            }
            return string.Empty;
        }

        /// <summary>
        /// 获取共享数据目录
        /// </summary>
        public static string GetSharedDataDir()
        {
            if (_apiPtr == IntPtr.Zero)
                return string.Empty;

            try
            {
                IntPtr funcPtr = GetFunctionPointer(ApiOffset.GetSharedDataDir);
                if (funcPtr != IntPtr.Zero)
                {
                    var func = Marshal.GetDelegateForFunctionPointer<GetSharedDataDirDelegate>(funcPtr);
                    IntPtr dirPtr = func();
                    return PtrToStringUtf8(dirPtr);
                }
            }
            catch (Exception ex)
            {
                Log.Error("RimeNativeBindings", $"GetSharedDataDir 失败: {ex.Message}");
            }
            return string.Empty;
        }

        // 委托定义
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr GetVersionDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void SetupDelegate(IntPtr traits);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void InitializeDelegate(IntPtr traits);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void FinalizeDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr CreateSessionDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate bool DestroySessionDelegate(IntPtr sessionId);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate bool ProcessKeyDelegate(IntPtr sessionId, int keycode, int mask);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate bool CommitCompositionDelegate(IntPtr sessionId);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void ClearCompositionDelegate(IntPtr sessionId);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate bool GetCommitDelegate(IntPtr sessionId, IntPtr commit);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate bool FreeCommitDelegate(IntPtr commit);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate bool GetContextDelegate(IntPtr sessionId, IntPtr context);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate bool FreeContextDelegate(IntPtr context);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate bool GetStatusDelegate(IntPtr sessionId, IntPtr status);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate bool FreeStatusDelegate(IntPtr status);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void SetOptionDelegate(IntPtr sessionId, [MarshalAs(UnmanagedType.LPUTF8Str)] string option, int value);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate bool GetOptionDelegate(IntPtr sessionId, [MarshalAs(UnmanagedType.LPUTF8Str)] string option);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate bool SelectCandidateOnCurrentPageDelegate(IntPtr sessionId, ulong index);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate bool CandidateListBeginDelegate(IntPtr sessionId, IntPtr iterator);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate bool CandidateListNextDelegate(IntPtr iterator);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void CandidateListEndDelegate(IntPtr iterator);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate bool SimulateKeySequenceDelegate(IntPtr sessionId, [MarshalAs(UnmanagedType.LPUTF8Str)] string keySequence);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr FindModuleDelegate([MarshalAs(UnmanagedType.LPUTF8Str)] string moduleName);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate bool ChangePageDelegate(IntPtr sessionId, int backward);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate bool IsMaintenanceModeDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate bool StartMaintenanceDelegate(bool fullCheck);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate bool DeployDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void JoinMaintenanceThreadDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate bool SelectSchemaDelegate(IntPtr sessionId, [MarshalAs(UnmanagedType.LPUTF8Str)] string schemaId);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate bool SyncUserDataDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr GetUserDataDirDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr GetSyncDirDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr GetSharedDataDirDelegate();

        // 辅助方法
        private const int MaxStringLength = 64 * 1024;

        public static string PtrToStringUtf8(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero)
                return string.Empty;

            try
            {
                int length = 0;
                while (length < MaxStringLength && Marshal.ReadByte(ptr, length) != 0)
                    length++;
                byte[] buffer = new byte[length];
                Marshal.Copy(ptr, buffer, 0, length);
                return System.Text.Encoding.UTF8.GetString(buffer);
            }
            catch
            {
                return string.Empty;
            }
        }

        // 获取 Rime commit text 字段
        public static string? GetCommitText(IntPtr commit)
        {
            if (commit == IntPtr.Zero)
                return null;

            var commitStruct = Marshal.PtrToStructure<RimeCommit>(commit);
            return PtrToStringUtf8(commitStruct.text);
        }

        // 获取 Rime context composition preedit 字段
        public static string? GetCompositionPreedit(IntPtr context)
        {
            if (context == IntPtr.Zero)
                return null;

            var contextStruct = Marshal.PtrToStructure<RimeContext>(context);
            return PtrToStringUtf8(contextStruct.composition.preedit);
        }
    }
}
