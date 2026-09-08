using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using NuvioPlayer.Helpers;
using NuvioPlayer.Interfaces;

namespace NuvioPlayer.Services;

public class WindowsFileAssociationService : IFileAssociationService
{
    private readonly ILogger<WindowsFileAssociationService> _logger;

    public IReadOnlyList<string> SupportedExtensions => MediaFileHelper.VideoExtensions;

    [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    private const uint SHCNE_ASSOCCHANGED = 0x08000000;
    private const uint SHCNF_IDLIST = 0x0000;

    public WindowsFileAssociationService(ILogger<WindowsFileAssociationService> logger)
    {
        _logger = logger;
    }

    private string GetExecutablePath()
    {
        return Environment.ProcessPath 
            ?? Process.GetCurrentProcess().MainModule?.FileName 
            ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NuvioPlayer.exe");
    }

    private string GetProgId(string extension)
    {
        string cleanExt = extension.TrimStart('.').ToLowerInvariant();
        return $"NuvioPlayer.AssocFile.{cleanExt}";
    }

    public Task<Dictionary<string, bool>> GetAssociationStatusesAsync()
    {
        return Task.Run(() =>
        {
            var dict = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            string exePath = GetExecutablePath();

            foreach (var ext in SupportedExtensions)
            {
                bool isAssociated = false;
                try
                {
                    string progId = GetProgId(ext);
                    using var key = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{progId}\shell\open\command");
                    if (key != null)
                    {
                        var value = key.GetValue(null) as string;
                        if (!string.IsNullOrEmpty(value) && value.Contains(exePath, StringComparison.OrdinalIgnoreCase))
                        {
                            isAssociated = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to check registry association for {Ext}", ext);
                }

                dict[ext] = isAssociated;
            }

            return dict;
        });
    }

    public Task<bool> RegisterAssociationAsync(string extension)
    {
        return Task.Run(() =>
        {
            try
            {
                string exePath = GetExecutablePath();
                string progId = GetProgId(extension);
                string cleanExt = extension.StartsWith(".") ? extension : $".{extension}";

                // 1. Create ProgID key
                using (var progIdKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{progId}"))
                {
                    if (progIdKey == null) return false;
                    progIdKey.SetValue(null, $"Nuvio {cleanExt.ToUpperInvariant().TrimStart('.')} Media File");

                    using var iconKey = progIdKey.CreateSubKey("DefaultIcon");
                    iconKey?.SetValue(null, $"\"{exePath}\",0");

                    using var commandKey = progIdKey.CreateSubKey(@"shell\open\command");
                    commandKey?.SetValue(null, $"\"{exePath}\" \"%1\"");
                }

                // 2. Add to OpenWithProgids for the extension
                using (var openWithKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{cleanExt}\OpenWithProgids"))
                {
                    openWithKey?.SetValue(progId, string.Empty);
                }

                // 3. Register under Applications
                using (var appKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\Applications\NuvioPlayer.exe\SupportedTypes"))
                {
                    appKey?.SetValue(cleanExt, string.Empty);
                }

                using (var appCmdKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\Applications\NuvioPlayer.exe\shell\open\command"))
                {
                    appCmdKey?.SetValue(null, $"\"{exePath}\" \"%1\"");
                }

                // Notify shell of association update
                SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
                _logger.LogInformation("Successfully registered file association for {Ext}", cleanExt);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register file association for {Ext}", extension);
                return false;
            }
        });
    }

    public Task<bool> UnregisterAssociationAsync(string extension)
    {
        return Task.Run(() =>
        {
            try
            {
                string progId = GetProgId(extension);
                string cleanExt = extension.StartsWith(".") ? extension : $".{extension}";

                // Remove from OpenWithProgids
                using (var openWithKey = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{cleanExt}\OpenWithProgids", writable: true))
                {
                    openWithKey?.DeleteValue(progId, throwOnMissingValue: false);
                }

                // Delete ProgID tree
                Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\{progId}", throwOnMissingSubKey: false);

                // Notify shell
                SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
                _logger.LogInformation("Successfully unregistered file association for {Ext}", cleanExt);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to unregister file association for {Ext}", extension);
                return false;
            }
        });
    }

    public async Task<bool> RegisterAllAssociationsAsync()
    {
        bool allSuccess = true;
        foreach (var ext in SupportedExtensions)
        {
            bool success = await RegisterAssociationAsync(ext);
            if (!success) allSuccess = false;
        }
        return allSuccess;
    }

    public async Task<bool> UnregisterAllAssociationsAsync()
    {
        bool allSuccess = true;
        foreach (var ext in SupportedExtensions)
        {
            bool success = await UnregisterAssociationAsync(ext);
            if (!success) allSuccess = false;
        }
        return allSuccess;
    }
}
