using Microsoft.Win32;
using System;
using System.IO;
using System.Runtime.InteropServices;

public static class FileTypeRegister
{
    private const string Extension = ".rrc";
    private const string ProgId = "RunRecap.RRC";
    private const string Description = "Run Recap File";

    // this is all GPT-5
    public static void EnsureRRCFileIcon(string iconPath)
    {
        // --- 1. Check if extension is already registered ---
        using (RegistryKey extKey =
               Registry.CurrentUser.OpenSubKey(@"Software\Classes\" + Extension, writable: false))
        {
            if (extKey != null)
            {
                string existingProgId = extKey.GetValue("") as string;

                // If extension points to some ProgID already and it has an icon, stop
                if (!string.IsNullOrEmpty(existingProgId))
                {
                    using (RegistryKey existingProgKey =
                           Registry.CurrentUser.OpenSubKey(@"Software\Classes\" + existingProgId))
                    {
                        if (existingProgKey?.OpenSubKey("DefaultIcon") != null)
                            return; // Already registered — do nothing
                    }
                }
            }
        }

        // --- 2. Register extension if needed ---
        using (RegistryKey extKey =
               Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + Extension))
        {
            extKey.SetValue("", ProgId);
        }

        // --- 3. Register ProgID if needed ---
        using (RegistryKey progIdKey =
               Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + ProgId))
        {
            progIdKey.SetValue("", Description);

            using (RegistryKey defaultIcon =
                   progIdKey.CreateSubKey("DefaultIcon"))
            {
                defaultIcon.SetValue("", iconPath);
            }
        }

        // --- 4. Tell Windows to update its icon cache ---
        SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
    }

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(
        uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);
}
