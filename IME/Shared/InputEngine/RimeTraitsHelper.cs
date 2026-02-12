using System;
using System.Runtime.InteropServices;
using Android.Util;

namespace IME.Shared.InputEngine;

internal static class RimeTraitsHelper
{
    private const string Tag = "RimeTraitsHelper";

    public static IntPtr CreateTraits(string sharedDir, string userDir)
    {
        var traits = new RimeNativeBindings.RimeTraits
        {
            data_size = RimeNativeBindings.GetStructDataSize<RimeNativeBindings.RimeTraits>(),
            shared_data_dir = Marshal.StringToHGlobalAnsi(sharedDir),
            user_data_dir = Marshal.StringToHGlobalAnsi(userDir),
            distribution_name = Marshal.StringToHGlobalAnsi("IME"),
            distribution_code_name = Marshal.StringToHGlobalAnsi("ime"),
            distribution_version = Marshal.StringToHGlobalAnsi("1.0.0"),
            app_name = Marshal.StringToHGlobalAnsi("ime.app"),
            modules = IntPtr.Zero,
            min_log_level = 1,
            log_dir = IntPtr.Zero,
            prebuilt_data_dir = IntPtr.Zero,
            staging_dir = IntPtr.Zero
        };

        IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf<RimeNativeBindings.RimeTraits>());
        Marshal.StructureToPtr(traits, ptr, false);
        return ptr;
    }

    public static void FreeTraits(IntPtr traitsPtr)
    {
        if (traitsPtr == IntPtr.Zero) return;

        try
        {
            var traits = Marshal.PtrToStructure<RimeNativeBindings.RimeTraits>(traitsPtr);
            if (traits.shared_data_dir != IntPtr.Zero) Marshal.FreeHGlobal(traits.shared_data_dir);
            if (traits.user_data_dir != IntPtr.Zero) Marshal.FreeHGlobal(traits.user_data_dir);
            if (traits.distribution_name != IntPtr.Zero) Marshal.FreeHGlobal(traits.distribution_name);
            if (traits.distribution_code_name != IntPtr.Zero) Marshal.FreeHGlobal(traits.distribution_code_name);
            if (traits.distribution_version != IntPtr.Zero) Marshal.FreeHGlobal(traits.distribution_version);
            if (traits.app_name != IntPtr.Zero) Marshal.FreeHGlobal(traits.app_name);
            Marshal.FreeHGlobal(traitsPtr);
        }
        catch (Exception ex)
        {
            Log.Warn(Tag, $"FreeTraits failed: {ex.Message}");
        }
    }
}
