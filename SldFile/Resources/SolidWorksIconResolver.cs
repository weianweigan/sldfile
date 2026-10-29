using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace SldFile.Resources
{
    /// <summary>
    /// Utility class to extract and resolve feature icons from SolidWorks UI resource libraries (swStyleBlueu.dll, swStyleColoru.dll).
    /// </summary>
    public static class SolidWorksIconResolver
    {
        private static readonly ConcurrentDictionary<string, byte[]?> _cache = new ConcurrentDictionary<string, byte[]?>(StringComparer.OrdinalIgnoreCase);

        #region Win32 API

        private const uint LOAD_LIBRARY_AS_DATAFILE = 0x00000002;
        private const uint LOAD_LIBRARY_AS_IMAGE_RESOURCE = 0x00000020;

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, uint dwFlags);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr FindResource(IntPtr hModule, string lpName, string lpType);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LoadResource(IntPtr hModule, IntPtr hResInfo);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LockResource(IntPtr hResData);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint SizeofResource(IntPtr hModule, IntPtr hResInfo);

        #endregion

        /// <summary>
        /// Clears all cached icon bytes.
        /// </summary>
        public static void ClearCache()
        {
            _cache.Clear();
        }

        /// <summary>
        /// Extracts raw byte data for a specific named resource from a PE library.
        /// </summary>
        /// <param name="dllPath">Full path to the DLL.</param>
        /// <param name="resourceName">The name of the resource (e.g. "PNG_FEATURES_EXTRUDE_BOSS").</param>
        /// <param name="resourceType">The resource type (default "PNG").</param>
        /// <returns>Resource byte array, or null if not found or cannot be loaded.</returns>
        public static byte[]? ExtractResource(string dllPath, string resourceName, string resourceType = "PNG")
        {
            if (string.IsNullOrWhiteSpace(dllPath) || !File.Exists(dllPath))
                return null;

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return null;

            string cacheKey = $"{dllPath}::{resourceType}::{resourceName}";
            if (_cache.TryGetValue(cacheKey, out var cached))
                return cached;

            byte[]? data = ExtractResourceInternal(dllPath, resourceName, resourceType);
            _cache[cacheKey] = data;
            return data;
        }

        private static byte[]? ExtractResourceInternal(string dllPath, string resourceName, string resourceType)
        {
            IntPtr hMod = LoadLibraryEx(dllPath, IntPtr.Zero, LOAD_LIBRARY_AS_DATAFILE | LOAD_LIBRARY_AS_IMAGE_RESOURCE);
            if (hMod == IntPtr.Zero)
                return null;

            try
            {
                IntPtr hRes = FindResource(hMod, resourceName, resourceType);
                if (hRes == IntPtr.Zero)
                    return null;

                uint size = SizeofResource(hMod, hRes);
                if (size == 0)
                    return null;

                IntPtr hGlobal = LoadResource(hMod, hRes);
                if (hGlobal == IntPtr.Zero)
                    return null;

                IntPtr pData = LockResource(hGlobal);
                if (pData == IntPtr.Zero)
                    return null;

                byte[] buffer = new byte[size];
                Marshal.Copy(pData, buffer, 0, (int)size);
                return buffer;
            }
            catch
            {
                return null;
            }
            finally
            {
                FreeLibrary(hMod);
            }
        }

        /// <summary>
        /// Gets icon byte array for the specified base resource name, automatically handling size variant suffixes.
        /// </summary>
        /// <param name="baseResourceName">Base resource name (e.g. "PNG_FEATURES_EXTRUDE_BOSS").</param>
        /// <param name="preferredSize">Requested pixel size (e.g. 20, 32, 40, 64, 96, 128).</param>
        /// <param name="theme">Theme to load from (Blue or Color).</param>
        /// <returns>PNG byte array or null.</returns>
        public static byte[]? GetIcon(string baseResourceName, int preferredSize = 20, SolidWorksIconTheme theme = SolidWorksIconTheme.Blue)
        {
            var inst = SolidWorksInstallation.Current;
            if (inst == null)
                return null;

            string? primaryDll = theme == SolidWorksIconTheme.Color && inst.HasStyleColor
                ? inst.SwStyleColorPath
                : inst.SwStyleBluePath;

            if (string.IsNullOrEmpty(primaryDll))
                return null;

            // Generate size candidates in order of preference
            var candidates = GetSizeCandidates(baseResourceName, preferredSize);

            foreach (var resName in candidates)
            {
                byte[]? bytes = ExtractResource(primaryDll!, resName, "PNG");
                if (bytes != null) return bytes;
            }

            // Fallback to Blue theme if Color theme didn't have this particular icon
            if (theme == SolidWorksIconTheme.Color && inst.HasStyleBlue && primaryDll != inst.SwStyleBluePath)
            {
                foreach (var resName in candidates)
                {
                    byte[]? bytes = ExtractResource(inst.SwStyleBluePath!, resName, "PNG");
                    if (bytes != null) return bytes;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the feature icon corresponding to the specified FeatureKind and optional feature name.
        /// </summary>
        public static byte[]? GetFeatureIcon(FeatureKind kind, string? featureName = null, int preferredSize = 20, SolidWorksIconTheme theme = SolidWorksIconTheme.Blue)
        {
            string? resName = ResolveResourceName(kind, featureName);
            if (string.IsNullOrEmpty(resName))
                return null;

            return GetIcon(resName!, preferredSize, theme);
        }

        /// <summary>
        /// Gets the icon corresponding to a feature name, type name, or keyword.
        /// </summary>
        public static byte[]? GetFeatureIcon(string featureNameOrType, int preferredSize = 20, SolidWorksIconTheme theme = SolidWorksIconTheme.Blue)
        {
            string? resName = ResolveResourceName(featureNameOrType);
            if (string.IsNullOrEmpty(resName))
                return null;

            return GetIcon(resName!, preferredSize, theme);
        }

        /// <summary>
        /// Maps a <see cref="FeatureKind"/> and optional name to the corresponding SolidWorks base resource name.
        /// </summary>
        public static string? ResolveResourceName(FeatureKind kind, string? featureName = null)
        {
            string name = featureName ?? string.Empty;

            // Check for specific name-based overrides first (e.g. Origin, Cut, 3D Sketch)
            if (name.IndexOf("原点", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Origin", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "PNG_TREE_ORIGIN";
            }

            switch (kind)
            {
                case FeatureKind.ReferencePlane:
                    return "PNG_REFGEO_PLANE";

                case FeatureKind.Sketch:
                    return (name.IndexOf("3D", StringComparison.OrdinalIgnoreCase) >= 0)
                        ? "PNG_SKETCH_3D_SKETCH"
                        : "PNG_SKETCH_SKETCH";

                case FeatureKind.Extrusion:
                    return IsCut(name)
                        ? "PNG_FEATURES_EXTRUDE_CUT"
                        : "PNG_FEATURES_EXTRUDE_BOSS";

                case FeatureKind.Cut:
                    if (name.IndexOf("旋转", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("Revolv", StringComparison.OrdinalIgnoreCase) >= 0)
                        return "PNG_FEATURES_REVOLVED_CUT";
                    if (name.IndexOf("扫描", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("Sweep", StringComparison.OrdinalIgnoreCase) >= 0)
                        return "PNG_FEATURES_SWEPT_CUT";
                    if (name.IndexOf("放样", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("Loft", StringComparison.OrdinalIgnoreCase) >= 0)
                        return "PNG_FEATURES_LOFTED_CUT";
                    return "PNG_FEATURES_EXTRUDE_CUT";

                case FeatureKind.Revolve:
                    return IsCut(name) ? "PNG_FEATURES_REVOLVED_CUT" : "PNG_FEATURES_REVOLVED_BOSS_BASE";

                case FeatureKind.Sweep:
                    return IsCut(name) ? "PNG_FEATURES_SWEPT_CUT" : "PNG_FEATURES_SWEPT_BOSS";

                case FeatureKind.Loft:
                    return IsCut(name) ? "PNG_FEATURES_LOFTED_CUT" : "PNG_FEATURES_LOFTED_BOSS";

                case FeatureKind.Fillet:
                    return "PNG_FEATURES_FILLET";

                case FeatureKind.Chamfer:
                    return "PNG_FEATURES_CHAMFER";

                case FeatureKind.HoleWizard:
                    return "PNG_FEATURES_HOLE_WIZARD";

                case FeatureKind.Pattern:
                    if (name.IndexOf("圆周", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("Cir", StringComparison.OrdinalIgnoreCase) >= 0)
                        return "PNG_FEATURES_CIRCULAR_PATTERN";
                    if (name.IndexOf("曲线", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("Curve", StringComparison.OrdinalIgnoreCase) >= 0)
                        return "PNG_FEATURES_CURVE_DRIVEN_PATTERN";
                    if (name.IndexOf("草图", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("Sketch", StringComparison.OrdinalIgnoreCase) >= 0)
                        return "PNG_FEATURES_SKETCH_DRIVEN_PATTERN";
                    return "PNG_FEATURES_LINEAR_PATTERN";

                case FeatureKind.Mirror:
                    return "PNG_FEATURES_MIRROR";

                case FeatureKind.Rib:
                    return "PNG_FEATURES_RIB";

                case FeatureKind.Shell:
                    return "PNG_FEATURES_SHELL";

                case FeatureKind.Draft:
                    return "PNG_FEATURES_DRAFT";

                case FeatureKind.Thread:
                    return "PNG_FEATURES_THREAD_WIZARD";

                case FeatureKind.Folder:
                    return "PNG_FEAT_EMPTY_FOLDER";

                case FeatureKind.Dimension:
                    return "PNG_SKETCH_ADD_DIM";

                default:
                    // If Other, attempt string resolution on featureName
                    return !string.IsNullOrEmpty(featureName) ? ResolveResourceName(featureName!) : null;
            }
        }

        /// <summary>
        /// Maps an arbitrary feature name, type name, or keyword string to the corresponding SolidWorks base resource name.
        /// </summary>
        public static string? ResolveResourceName(string featureNameOrType)
        {
            if (string.IsNullOrWhiteSpace(featureNameOrType))
                return null;

            string s = featureNameOrType.Trim();

            // Direct resource name support (if caller passed "PNG_FEATURES_EXTRUDE_BOSS", etc.)
            if (s.StartsWith("PNG_", StringComparison.OrdinalIgnoreCase) ||
                s.StartsWith("IDB_", StringComparison.OrdinalIgnoreCase) ||
                s.StartsWith("IDR_", StringComparison.OrdinalIgnoreCase))
            {
                return s;
            }

            // Keyword based matching
            if (s.IndexOf("原点", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("Origin", StringComparison.OrdinalIgnoreCase) >= 0)
                return "PNG_TREE_ORIGIN";

            if (s.IndexOf("基准面", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("Plane", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("前视", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("上视", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("右视", StringComparison.OrdinalIgnoreCase) >= 0)
                return "PNG_REFGEO_PLANE";

            if (s.IndexOf("3D草图", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("3DSketch", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("3D Sketch", StringComparison.OrdinalIgnoreCase) >= 0)
                return "PNG_SKETCH_3D_SKETCH";

            if (s.IndexOf("草图", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("Sketch", StringComparison.OrdinalIgnoreCase) >= 0)
                return "PNG_SKETCH_SKETCH";

            if (s.IndexOf("拉伸", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("Extru", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("凸台", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("Boss", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return IsCut(s) ? "PNG_FEATURES_EXTRUDE_CUT" : "PNG_FEATURES_EXTRUDE_BOSS";
            }

            if (s.IndexOf("切除", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("Cut", StringComparison.OrdinalIgnoreCase) >= 0)
                return "PNG_FEATURES_EXTRUDE_CUT";

            if (s.IndexOf("旋转", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("Revolv", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return IsCut(s) ? "PNG_FEATURES_REVOLVED_CUT" : "PNG_FEATURES_REVOLVED_BOSS_BASE";
            }

            if (s.IndexOf("扫描", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("Sweep", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return IsCut(s) ? "PNG_FEATURES_SWEPT_CUT" : "PNG_FEATURES_SWEPT_BOSS";
            }

            if (s.IndexOf("放样", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("Loft", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return IsCut(s) ? "PNG_FEATURES_LOFTED_CUT" : "PNG_FEATURES_LOFTED_BOSS";
            }

            if (s.IndexOf("圆角", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("Fillet", StringComparison.OrdinalIgnoreCase) >= 0)
                return "PNG_FEATURES_FILLET";

            if (s.IndexOf("倒角", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("Chamfer", StringComparison.OrdinalIgnoreCase) >= 0)
                return "PNG_FEATURES_CHAMFER";

            if (s.IndexOf("异型孔", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("孔向导", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("HoleWizard", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("Hole Wizard", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("Hole", StringComparison.OrdinalIgnoreCase) >= 0)
                return "PNG_FEATURES_HOLE_WIZARD";

            if (s.IndexOf("圆周阵列", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("CirPattern", StringComparison.OrdinalIgnoreCase) >= 0)
                return "PNG_FEATURES_CIRCULAR_PATTERN";

            if (s.IndexOf("阵列", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("Pattern", StringComparison.OrdinalIgnoreCase) >= 0)
                return "PNG_FEATURES_LINEAR_PATTERN";

            if (s.IndexOf("镜像", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("Mirror", StringComparison.OrdinalIgnoreCase) >= 0)
                return "PNG_FEATURES_MIRROR";

            if (s.IndexOf("筋", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("Rib", StringComparison.OrdinalIgnoreCase) >= 0)
                return "PNG_FEATURES_RIB";

            if (s.IndexOf("抽壳", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("Shell", StringComparison.OrdinalIgnoreCase) >= 0)
                return "PNG_FEATURES_SHELL";

            if (s.IndexOf("拔模", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("Draft", StringComparison.OrdinalIgnoreCase) >= 0)
                return "PNG_FEATURES_DRAFT";

            if (s.IndexOf("螺纹", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("Thread", StringComparison.OrdinalIgnoreCase) >= 0)
                return "PNG_FEATURES_THREAD_WIZARD";

            if (s.IndexOf("圆顶", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("Dome", StringComparison.OrdinalIgnoreCase) >= 0)
                return "PNG_FEATURES_DOME";

            if (s.IndexOf("组合", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("Combine", StringComparison.OrdinalIgnoreCase) >= 0)
                return "PNG_FEATURES_COMBINE";

            if (s.IndexOf("相交", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("Intersect", StringComparison.OrdinalIgnoreCase) >= 0)
                return "PNG_FEATURES_INTERSECT";

            if (s.IndexOf("分割", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("Split", StringComparison.OrdinalIgnoreCase) >= 0)
                return "PNG_FEATURES_SPLIT";

            if (s.IndexOf("加厚", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("Thicken", StringComparison.OrdinalIgnoreCase) >= 0)
                return IsCut(s) ? "PNG_FEATURES_THICKENED_CUT" : "PNG_FEATURES_THICKEN";

            if (s.IndexOf("边界", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("Boundary", StringComparison.OrdinalIgnoreCase) >= 0)
                return IsCut(s) ? "PNG_FEATURES_BOUNDARY_CUT" : "PNG_FEATURES_BOUNDARY_BOSS";

            if (s.IndexOf("活页夹", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("Folder", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("文件夹", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("特征管理器", StringComparison.OrdinalIgnoreCase) >= 0)
                return "PNG_FEAT_EMPTY_FOLDER";

            if (s.IndexOf("材质", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("Material", StringComparison.OrdinalIgnoreCase) >= 0)
                return "PNG_TREE_MATERIAL_FOLDER";

            if (s.IndexOf("坐标系", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("Coordinate", StringComparison.OrdinalIgnoreCase) >= 0)
                return "PNG_COORD_SYS_FOLDER";

            if (s.IndexOf("配合", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("Mate", StringComparison.OrdinalIgnoreCase) >= 0)
                return "PNG_ASSEMBLIES_MATE";

            if (s.IndexOf("装配", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("Assembly", StringComparison.OrdinalIgnoreCase) >= 0)
                return "PNG_ASSEMBLY";

            if (s.IndexOf("零件", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("Part", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("组件", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("Component", StringComparison.OrdinalIgnoreCase) >= 0)
                return "PNG_ASSEMBLIES_NEW_PART";

            if (s.IndexOf("图纸", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("Sheet", StringComparison.OrdinalIgnoreCase) >= 0)
                return "PNG_ACTIVE_SHEET_LEFT";

            if (s.IndexOf("自定义属性", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("Properties", StringComparison.OrdinalIgnoreCase) >= 0)
                return "PNG_CALLOUT_BLOCKPART";

            if (s.IndexOf("方程式", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("Equation", StringComparison.OrdinalIgnoreCase) >= 0)
                return "PNG_SKETCH_EQ_DRIVEN_CURVE";

            if (s.IndexOf("预览", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("Preview", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("几何", StringComparison.OrdinalIgnoreCase) >= 0)
                return "PNG_I3DVIEW_DATUM_REF_PLANE";

            return null;
        }

        private static bool IsCut(string text)
        {
            return text.IndexOf("切除", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("Cut", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static List<string> GetSizeCandidates(string baseName, int size)
        {
            var list = new List<string>();

            // If baseName already has an explicit size suffix, test it first
            if (baseName.EndsWith("_20") || baseName.EndsWith("_32") || baseName.EndsWith("_40") ||
                baseName.EndsWith("_64") || baseName.EndsWith("_96") || baseName.EndsWith("_128"))
            {
                list.Add(baseName);
                return list;
            }

            if (size <= 24)
            {
                list.Add(baseName);           // 20x20 default
                list.Add(baseName + "_32");
                list.Add(baseName + "_40");
            }
            else if (size <= 32)
            {
                list.Add(baseName + "_32");
                list.Add(baseName);
                list.Add(baseName + "_40");
            }
            else if (size <= 48)
            {
                list.Add(baseName + "_40");
                list.Add(baseName + "_32");
                list.Add(baseName + "_64");
                list.Add(baseName);
            }
            else if (size <= 64)
            {
                list.Add(baseName + "_64");
                list.Add(baseName + "_40");
                list.Add(baseName + "_96");
                list.Add(baseName + "_32");
                list.Add(baseName);
            }
            else
            {
                list.Add(baseName + "_128");
                list.Add(baseName + "_96");
                list.Add(baseName + "_64");
                list.Add(baseName + "_40");
                list.Add(baseName + "_32");
                list.Add(baseName);
            }

            return list;
        }
    }
}
