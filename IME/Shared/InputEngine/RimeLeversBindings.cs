using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Android.Util;

namespace IME.Shared.InputEngine;

internal static class RimeLeversBindings
{
    private const string Tag = "RimeLeversBindings";
    private const string ModuleName = "levers";

    private static bool _initialized;
    private static bool _available;
    private static RimeLeversApi _api;

    public static bool IsAvailable
    {
        get
        {
            EnsureInitialized();
            return _available;
        }
    }

    public static List<string> ListUserDicts()
    {
        EnsureInitialized();
        if (!_available)
        {
            return new List<string>();
        }

        if (_api.user_dict_iterator_init == IntPtr.Zero || _api.next_user_dict == IntPtr.Zero)
        {
            return new List<string>();
        }

        var init = Marshal.GetDelegateForFunctionPointer<UserDictIteratorInitDelegate>(_api.user_dict_iterator_init);
        var next = Marshal.GetDelegateForFunctionPointer<NextUserDictDelegate>(_api.next_user_dict);
        var destroy = _api.user_dict_iterator_destroy != IntPtr.Zero
            ? Marshal.GetDelegateForFunctionPointer<UserDictIteratorDestroyDelegate>(_api.user_dict_iterator_destroy)
            : null;

        var results = new List<string>();
        var iter = new RimeUserDictIterator();
        if (init(ref iter) == 0)
        {
            return results;
        }

        try
        {
            while (true)
            {
                IntPtr namePtr = next(ref iter);
                if (namePtr == IntPtr.Zero)
                {
                    break;
                }

                string name = RimeNativeBindings.PtrToStringUtf8(namePtr);
                if (!string.IsNullOrEmpty(name))
                {
                    results.Add(name);
                }
            }
        }
        finally
        {
            destroy?.Invoke(ref iter);
        }

        return results;
    }

    public static int ExportUserDict(string dictName, string outputPath)
    {
        EnsureInitialized();
        if (!_available || string.IsNullOrWhiteSpace(dictName) || string.IsNullOrWhiteSpace(outputPath))
        {
            return -1;
        }

        if (_api.export_user_dict == IntPtr.Zero)
        {
            return -1;
        }

        var export = Marshal.GetDelegateForFunctionPointer<ExportUserDictDelegate>(_api.export_user_dict);
        return export(dictName, outputPath);
    }

    public static int ImportUserDict(string dictName, string inputPath)
    {
        EnsureInitialized();
        if (!_available || string.IsNullOrWhiteSpace(dictName) || string.IsNullOrWhiteSpace(inputPath))
        {
            return -1;
        }

        if (_api.import_user_dict == IntPtr.Zero)
        {
            return -1;
        }

        var import = Marshal.GetDelegateForFunctionPointer<ImportUserDictDelegate>(_api.import_user_dict);
        return import(dictName, inputPath);
    }

    private static void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;

        try
        {
            IntPtr modulePtr = RimeNativeBindings.FindModule(ModuleName);
            if (modulePtr == IntPtr.Zero)
            {
                Log.Warn(Tag, "Levers module not found.");
                return;
            }

            var module = Marshal.PtrToStructure<RimeModule>(modulePtr);
            if (module.get_api == IntPtr.Zero)
            {
                Log.Warn(Tag, "Levers get_api not available.");
                return;
            }

            var getApi = Marshal.GetDelegateForFunctionPointer<GetApiDelegate>(module.get_api);
            IntPtr apiPtr = getApi();
            if (apiPtr == IntPtr.Zero)
            {
                Log.Warn(Tag, "Levers api pointer is null.");
                return;
            }

            _api = Marshal.PtrToStructure<RimeLeversApi>(apiPtr);
            _available = true;
        }
        catch (Exception ex)
        {
            Log.Warn(Tag, $"Initialize failed: {ex.Message}");
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RimeModule
    {
        public int data_size;
        public IntPtr module_name;
        public IntPtr initialize;
        public IntPtr finalize;
        public IntPtr get_api;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RimeLeversApi
    {
        public int data_size;
        public IntPtr custom_settings_init;
        public IntPtr custom_settings_destroy;
        public IntPtr load_settings;
        public IntPtr save_settings;
        public IntPtr customize_bool;
        public IntPtr customize_int;
        public IntPtr customize_double;
        public IntPtr customize_string;
        public IntPtr is_first_run;
        public IntPtr settings_is_modified;
        public IntPtr settings_get_config;

        public IntPtr switcher_settings_init;
        public IntPtr get_available_schema_list;
        public IntPtr get_selected_schema_list;
        public IntPtr schema_list_destroy;
        public IntPtr get_schema_id;
        public IntPtr get_schema_name;
        public IntPtr get_schema_version;
        public IntPtr get_schema_author;
        public IntPtr get_schema_description;
        public IntPtr get_schema_file_path;
        public IntPtr select_schemas;
        public IntPtr get_hotkeys;
        public IntPtr set_hotkeys;

        public IntPtr user_dict_iterator_init;
        public IntPtr user_dict_iterator_destroy;
        public IntPtr next_user_dict;
        public IntPtr backup_user_dict;
        public IntPtr restore_user_dict;
        public IntPtr export_user_dict;
        public IntPtr import_user_dict;

        public IntPtr customize_item;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RimeUserDictIterator
    {
        public IntPtr ptr;
        public UIntPtr i;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr GetApiDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int UserDictIteratorInitDelegate(ref RimeUserDictIterator iter);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void UserDictIteratorDestroyDelegate(ref RimeUserDictIterator iter);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr NextUserDictDelegate(ref RimeUserDictIterator iter);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int ExportUserDictDelegate(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string dictName,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string textFile);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int ImportUserDictDelegate(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string dictName,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string textFile);
}
