using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace SldFile.Resources
{
    /// <summary>
    /// Utility class to detect and locate the local SolidWorks installation directory and style resource DLLs.
    /// </summary>
    public sealed class SolidWorksInstallation
    {
        private static readonly object _syncLock = new object();
        private static SolidWorksInstallation? _current;
        private static bool _detected;
        private static string? _customPath;

        /// <summary>
        /// Custom directory path to override automatic SolidWorks installation detection.
        /// </summary>
        public static string? CustomPath
        {
            get => _customPath;
            set
            {
                lock (_syncLock)
                {
                    _customPath = value;
                    _detected = false;
                    _current = null;
                }
            }
        }

        /// <summary>
        /// Gets the detected local SolidWorks installation, or null if SolidWorks is not detected.
        /// </summary>
        public static SolidWorksInstallation? Current
        {
            get
            {
                if (!_detected)
                {
                    lock (_syncLock)
                    {
                        if (!_detected)
                        {
                            _current = Detect(_customPath);
                            _detected = true;
                        }
                    }
                }
                return _current;
            }
        }

        /// <summary>
        /// Resets the cached detection result so that subsequent calls re-scan the system.
        /// </summary>
        public static void Reset()
        {
            lock (_syncLock)
            {
                _detected = false;
                _current = null;
            }
        }

        /// <summary>
        /// The root installation directory of SolidWorks (e.g., "C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS").
        /// </summary>
        public string InstallationDirectory { get; }

        /// <summary>
        /// Full path to swStyleBlueu.dll (the primary high-DPI PNG icon theme library), if present.
        /// </summary>
        public string? SwStyleBluePath { get; }

        /// <summary>
        /// Full path to swStyleColoru.dll (the classic color PNG icon theme library), if present.
        /// </summary>
        public string? SwStyleColorPath { get; }

        /// <summary>
        /// Gets whether swStyleBlueu.dll is found and accessible.
        /// </summary>
        public bool HasStyleBlue => !string.IsNullOrEmpty(SwStyleBluePath) && File.Exists(SwStyleBluePath);

        /// <summary>
        /// Gets whether swStyleColoru.dll is found and accessible.
        /// </summary>
        public bool HasStyleColor => !string.IsNullOrEmpty(SwStyleColorPath) && File.Exists(SwStyleColorPath);

        internal SolidWorksInstallation(string installationDirectory)
        {
            InstallationDirectory = installationDirectory;

            string blue = Path.Combine(installationDirectory, "swStyleBlueu.dll");
            if (File.Exists(blue))
                SwStyleBluePath = blue;

            string color = Path.Combine(installationDirectory, "swStyleColoru.dll");
            if (File.Exists(color))
                SwStyleColorPath = color;
        }

        /// <summary>
        /// Attempts to detect the SolidWorks installation directory.
        /// </summary>
        /// <param name="customPath">Optional explicit path to check first.</param>
        /// <returns>A <see cref="SolidWorksInstallation"/> instance if found; otherwise, null.</returns>
        public static SolidWorksInstallation? Detect(string? customPath = null)
        {
            // 1. Explicit path: if specified, test only this path
            if (!string.IsNullOrWhiteSpace(customPath))
            {
                return Directory.Exists(customPath) ? TryCreate(customPath!) : null;
            }

            // 2. Environment variables
            string[] envVars = { "SOLIDWORKS_DIR", "SOLIDWORKS_HOME", "SLDWORKS_HOME", "SLDWORKS_PATH" };
            foreach (var ev in envVars)
            {
                string? envVal = Environment.GetEnvironmentVariable(ev);
                if (!string.IsNullOrWhiteSpace(envVal) && Directory.Exists(envVal))
                {
                    var inst = TryCreate(envVal!);
                    if (inst != null) return inst;
                }
            }

            // 3. Windows Registry (if running on Windows)
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                string? regPath = FindInstallationFromRegistry();
                if (!string.IsNullOrWhiteSpace(regPath) && Directory.Exists(regPath))
                {
                    var inst = TryCreate(regPath!);
                    if (inst != null) return inst;
                }
            }

            // 4. Common default installation paths
            string[] commonPaths =
            {
                @"C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS",
                @"C:\Program Files\SOLIDWORKS Corp (2)\SOLIDWORKS",
                @"C:\Program Files\SOLIDWORKS",
                @"D:\Program Files\SOLIDWORKS Corp\SOLIDWORKS",
                @"D:\Program Files\SOLIDWORKS",
                @"E:\Program Files\SOLIDWORKS Corp\SOLIDWORKS",
                @"E:\Program Files\SOLIDWORKS"
            };

            foreach (var path in commonPaths)
            {
                if (Directory.Exists(path))
                {
                    var inst = TryCreate(path);
                    if (inst != null) return inst;
                }
            }

            return null;
        }

        private static SolidWorksInstallation? TryCreate(string dirPath)
        {
            try
            {
                string normalized = dirPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string blue = Path.Combine(normalized, "swStyleBlueu.dll");
                string sldworksExe = Path.Combine(normalized, "SLDWORKS.exe");

                // Valid if either swStyleBlueu.dll or SLDWORKS.exe exists in the folder
                if (File.Exists(blue) || File.Exists(sldworksExe))
                {
                    return new SolidWorksInstallation(normalized);
                }
            }
            catch
            {
                // Ignore any path format or access exceptions
            }

            return null;
        }

        #region Win32 Registry P/Invoke

        private static readonly IntPtr HKEY_LOCAL_MACHINE = new IntPtr(unchecked((int)0x80000002));
        private static readonly IntPtr HKEY_CURRENT_USER = new IntPtr(unchecked((int)0x80000001));
        private const int KEY_READ = 0x20019;
        private const int KEY_WOW64_64KEY = 0x0100;
        private const int KEY_WOW64_32KEY = 0x0200;

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegOpenKeyExW", SetLastError = true)]
        private static extern int RegOpenKeyEx(IntPtr hKey, string lpSubKey, int ulOptions, int samDesired, out IntPtr phkResult);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegQueryValueExW", SetLastError = true)]
        private static extern int RegQueryValueEx(IntPtr hKey, string lpValueName, int lpReserved, out int lpType, StringBuilder lpData, ref int lpcbData);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegEnumKeyExW", SetLastError = true)]
        private static extern int RegEnumKeyEx(IntPtr hKey, int dwIndex, StringBuilder lpName, ref int lpcbName, IntPtr lpReserved, IntPtr lpClass, IntPtr lpcbClass, IntPtr lpftLastWriteTime);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern int RegCloseKey(IntPtr hKey);

        private static string? FindInstallationFromRegistry()
        {
            // Search order:
            // 1. HKLM 64-bit Software\SolidWorks
            // 2. HKCU Software\SolidWorks
            // 3. HKLM 32-bit (WOW6432Node) Software\SolidWorks
            string? found = SearchSolidWorksRegistryHive(HKEY_LOCAL_MACHINE, KEY_READ | KEY_WOW64_64KEY);
            if (!string.IsNullOrEmpty(found)) return found;

            found = SearchSolidWorksRegistryHive(HKEY_CURRENT_USER, KEY_READ);
            if (!string.IsNullOrEmpty(found)) return found;

            found = SearchSolidWorksRegistryHive(HKEY_LOCAL_MACHINE, KEY_READ | KEY_WOW64_32KEY);
            return found;
        }

        private static string? SearchSolidWorksRegistryHive(IntPtr rootKey, int samDesired)
        {
            if (RegOpenKeyEx(rootKey, @"SOFTWARE\SolidWorks", 0, samDesired, out IntPtr hSw) != 0)
                return null;

            try
            {
                var subkeys = new List<string>();
                int index = 0;
                var nameBuf = new StringBuilder(256);
                int nameLen = nameBuf.Capacity;

                while (RegEnumKeyEx(hSw, index++, nameBuf, ref nameLen, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero) == 0)
                {
                    string sub = nameBuf.ToString();
                    nameLen = nameBuf.Capacity;
                    if (sub.StartsWith("SOLIDWORKS", StringComparison.OrdinalIgnoreCase))
                    {
                        subkeys.Add(sub);
                    }
                }

                // Sort descending so newer versions (e.g. SOLIDWORKS 2026 > SOLIDWORKS 2024) are checked first
                subkeys.Sort(StringComparer.OrdinalIgnoreCase);
                subkeys.Reverse();

                foreach (var swVersionKey in subkeys)
                {
                    string setupPath = swVersionKey + @"\Setup";
                    if (RegOpenKeyEx(hSw, setupPath, 0, samDesired, out IntPtr hSetup) == 0)
                    {
                        try
                        {
                            var valBuf = new StringBuilder(512);
                            int valSize = valBuf.Capacity * 2;
                            if (RegQueryValueEx(hSetup, "SolidWorks Folder", 0, out _, valBuf, ref valSize) == 0)
                            {
                                string folder = valBuf.ToString().TrimEnd('\\', '/');
                                if (Directory.Exists(folder))
                                    return folder;
                            }
                        }
                        finally
                        {
                            RegCloseKey(hSetup);
                        }
                    }
                }
            }
            finally
            {
                RegCloseKey(hSw);
            }

            return null;
        }

        #endregion
    }
}
