// diode - 一键把本机全部显示器调到最暗 / 最亮
// 控制链路：DDC/CI(外接显示器) -> WMI(笔记本内置屏) -> Gamma 软件调暗(兜底)
// 目标框架：.NET Framework 4.x，WinForms，单文件，无需安装

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Management;
using Microsoft.Win32;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("diode")]
[assembly: AssemblyProduct("diode")]
[assembly: AssemblyCompany("diode contributors")]
[assembly: AssemblyDescription("One click: every monitor to darkest. Another: back to brightest.")]
[assembly: AssemblyCopyright("Copyright (c) 2026 diode contributors")]
[assembly: AssemblyVersion("0.0.1.0")]
[assembly: AssemblyFileVersion("0.0.1.0")]
[assembly: AssemblyInformationalVersion("0.0.1")]

namespace diode
{
    // 版本号唯一来源：改这里，然后同步 CHANGELOG.md 与 README 的 release 徽章
    internal static class AppInfo
    {
        public const string Version = "0.0.1";
    }

    // ---------------- Win32 ----------------
    internal static class Native
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int left, top, right, bottom; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct MONITORINFOEX
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szDevice;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct DISPLAY_DEVICE
        {
            public int cb;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceString;
            public int StateFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceKey;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct PHYSICAL_MONITOR
        {
            public IntPtr hPhysicalMonitor;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szPhysicalMonitorDescription;
        }

        public delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

        [DllImport("user32.dll")]
        public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool GetMonitorInfo(IntPtr hmon, ref MONITORINFOEX mi);
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool EnumDisplayDevices(string lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

        [DllImport("dxva2.dll")]
        public static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, ref uint pdwNumberOfPhysicalMonitors);
        [DllImport("dxva2.dll")]
        public static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, uint dwPhysicalMonitorArraySize, [Out] PHYSICAL_MONITOR[] pPhysicalMonitorArray);
        [DllImport("dxva2.dll")]
        public static extern bool DestroyPhysicalMonitors(uint dwPhysicalMonitorArraySize, PHYSICAL_MONITOR[] pPhysicalMonitorArray);
        [DllImport("dxva2.dll")]
        public static extern bool GetMonitorBrightness(IntPtr hMonitor, ref uint pdwMinimumBrightness, ref uint pdwCurrentBrightness, ref uint pdwMaximumBrightness);
        [DllImport("dxva2.dll")]
        public static extern bool SetMonitorBrightness(IntPtr hMonitor, uint dwNewBrightness);
        [DllImport("dxva2.dll")]
        public static extern bool GetMonitorCapabilities(IntPtr hMonitor, ref uint pdwMonitorCapabilities, ref uint pdwSupportedColorTemperatures);

        [DllImport("gdi32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr CreateDC(string lpszDriver, string lpszDevice, string lpszOutput, IntPtr lpInitData);
        [DllImport("gdi32.dll")]
        public static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")]
        public static extern bool GetDeviceGammaRamp(IntPtr hDC, ushort[] lpRamp);
        [DllImport("gdi32.dll")]
        public static extern bool SetDeviceGammaRamp(IntPtr hDC, ushort[] lpRamp);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        // WinExe 没有控制台，命令行调用时先挂到父进程的控制台再输出
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool AttachConsole(int dwProcessId);
        public const int ATTACH_PARENT_PROCESS = -1;

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X; public int Y; }

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);
        [DllImport("shcore.dll")]
        public static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);
        public const int MDT_EFFECTIVE_DPI = 0;
        public const uint MONITOR_DEFAULTTONEAREST = 2;

        // 实测当前线程的 DPI 感知级别（-4=PerMonitorV2 上下文）
        [DllImport("user32.dll")]
        public static extern IntPtr GetThreadDpiAwarenessContext();
        [DllImport("user32.dll")]
        public static extern int GetAwarenessFromDpiAwarenessContext(IntPtr value);

        public const int WM_HOTKEY = 0x0312;
        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT = 0x0004;
        public const uint MOD_WIN = 0x0008;
        public const uint MOD_NOREPEAT = 0x4000;
    }

    // ---------------- 显示器条目 ----------------
    internal class MonItem
    {
        public string DeviceName = "";      // \\.\DISPLAY1
        public string DisplayName = "";     // 显示器友好名
        public string InstancePath = "";    // DISPLAY\XXX\...
        public List<IntPtr> Physical = new List<IntPtr>();
        public List<string> PhysicalDesc = new List<string>();
        public bool Ddc;                    // DDC/CI 可用
        public uint Min, Max, Cur;
        public bool Wmi;                    // WMI 可用
        public bool WmiFallback;            // WMI 是兜底匹配上的（ID 对不上）
        public string Status = "";          // 本次操作结果
    }

    // ---------------- DPI ----------------
    internal static class Dpi
    {
        // 主显示器（进程所在线程）的缩放比例，WinForms 的 SystemFonts 也是按它返回的
        public static float SystemScale()
        {
            try
            {
                using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
                {
                    float v = g.DpiX / 96f;
                    // Windows 有效缩放范围约 100%~500%，超出视为驱动返回的垃圾值，回退 100%
                    if (v < 0.75f || v > 5f) return 1f;
                    return v;
                }
            }
            catch { return 1f; }
        }

        // 调试用：未钳制的原始 DpiX（保留供诊断扩展）
        public static float RawDpiX()
        {
            try { using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) return g.DpiX; }
            catch { return -1f; }
        }

        // 鼠标当前所在显示器的缩放比例（多屏不同 DPI 时按那块屏来渲染菜单）
        public static float ScaleAtCursor()
        {
            try
            {
                Native.POINT pt = new Native.POINT();
                pt.X = Cursor.Position.X;
                pt.Y = Cursor.Position.Y;
                IntPtr hmon = Native.MonitorFromPoint(pt, Native.MONITOR_DEFAULTTONEAREST);
                uint dx, dy;
                if (hmon != IntPtr.Zero && Native.GetDpiForMonitor(
                        hmon, Native.MDT_EFFECTIVE_DPI, out dx, out dy) == 0 && dx > 0)
                {
                    float v = dx / 96f;
                    if (v >= 0.75f && v <= 5f) return v;
                }
            }
            catch { }
            return SystemScale();
        }
    }

    // ---------------- 托盘图标（HiDPI） ----------------
    internal static class TrayIcon
    {
        // 通知区域基准 16px，乘当前系统缩放后挑最接近的候选尺寸，刚好给到系统，避免拉伸模糊。
        // 注意：这里刻意不用静态 int[]（RVA 数据初始化在本机某安全钩子下会被篡改），改用 IL 常量比较。
        public static int PickSize()
        {
            int want = (int)Math.Round(16f * Dpi.SystemScale());
            if (want <= 16) return 16;
            if (want <= 20) return 20;
            if (want <= 24) return 24;
            if (want <= 28) return 28;
            if (want <= 32) return 32;
            if (want <= 40) return 40;
            if (want <= 48) return 48;
            if (want <= 64) return 64;
            if (want <= 80) return 80;
            return 128;
        }

        // 按 32px 坐标系绘制二极管符号：亮态=琥珀发光，暗态=灰蓝
        public static Bitmap Render(int size, bool dark)
        {
            Bitmap bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            float u = size / 32f;
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                Color body = dark ? Color.FromArgb(110, 128, 178) : Color.FromArgb(255, 178, 44);
                if (!dark)
                {
                    using (SolidBrush glow = new SolidBrush(Color.FromArgb(60, 255, 178, 44)))
                        g.FillEllipse(glow, 3f * u, 2f * u, 26f * u, 28f * u);
                }
                using (SolidBrush br = new SolidBrush(body))
                {
                    g.FillRectangle(br, 3.5f * u, 14.9f * u, 7.5f * u, 2.2f * u);   // 左引线
                    g.FillRectangle(br, 26f * u, 14.9f * u, 2.5f * u, 2.2f * u);    // 右引线
                    PointF[] tri = new PointF[]
                    {
                        new PointF(10.5f * u, 7f * u),
                        new PointF(10.5f * u, 25f * u),
                        new PointF(21.5f * u, 16f * u)
                    };
                    g.FillPolygon(br, tri);                                          // 三角
                    g.FillRectangle(br, 22.8f * u, 7f * u, 3.2f * u, 18f * u);       // 竖条
                }
            }
            return bmp;
        }

        public static Icon Create(bool dark)
        {
            int picked = PickSize();
            using (Bitmap bmp = Render(picked, dark))
                return Icon.FromHandle(bmp.GetHicon());
        }
    }

    // ---------------- 亮度控制 ----------------
    internal static class Bright
    {
        private static readonly HashSet<string> GammaApplied = new HashSet<string>();
        private static readonly Dictionary<string, ushort[]> GammaSaved = new Dictionary<string, ushort[]>();

        public static List<MonItem> Enumerate()
        {
            List<MonItem> list = new List<MonItem>();
            try
            {
                Native.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, delegate (IntPtr hMon, IntPtr hdc, ref Native.RECT rc, IntPtr data)
                {
                    MonItem m = new MonItem();
                    Native.MONITORINFOEX mi = new Native.MONITORINFOEX();
                    mi.cbSize = Marshal.SizeOf(typeof(Native.MONITORINFOEX));
                    if (Native.GetMonitorInfo(hMon, ref mi))
                        m.DeviceName = mi.szDevice;

                    // 通过适配器名找到该显示器对应的 PnP 设备 ID
                    Native.DISPLAY_DEVICE dd = new Native.DISPLAY_DEVICE();
                    dd.cb = Marshal.SizeOf(typeof(Native.DISPLAY_DEVICE));
                    for (uint i = 0; Native.EnumDisplayDevices(null, i, ref dd, 0); i++)
                    {
                        if (!string.Equals(dd.DeviceName, m.DeviceName, StringComparison.OrdinalIgnoreCase)) continue;
                        Native.DISPLAY_DEVICE mdd = new Native.DISPLAY_DEVICE();
                        mdd.cb = Marshal.SizeOf(typeof(Native.DISPLAY_DEVICE));
                        if (Native.EnumDisplayDevices(dd.DeviceName, 0, ref mdd, 0))
                        {
                            m.DisplayName = mdd.DeviceString;
                            m.InstancePath = mdd.DeviceID;
                        }
                        break;
                    }
                    if (string.IsNullOrEmpty(m.DisplayName)) m.DisplayName = m.DeviceName;

                    // 物理监视器句柄 (DDC/CI)
                    uint n = 0;
                    if (Native.GetNumberOfPhysicalMonitorsFromHMONITOR(hMon, ref n) && n > 0)
                    {
                        Native.PHYSICAL_MONITOR[] pm = new Native.PHYSICAL_MONITOR[n];
                        if (Native.GetPhysicalMonitorsFromHMONITOR(hMon, n, pm))
                        {
                            for (int k = 0; k < pm.Length; k++)
                            {
                                m.Physical.Add(pm[k].hPhysicalMonitor);
                                m.PhysicalDesc.Add(pm[k].szPhysicalMonitorDescription);
                                uint mn = 0, cu = 0, mx = 0;
                                if (Native.GetMonitorBrightness(pm[k].hPhysicalMonitor, ref mn, ref cu, ref mx) && mx > mn)
                                {
                                    m.Ddc = true; m.Min = mn; m.Max = mx; m.Cur = cu;
                                }
                            }
                        }
                    }
                    list.Add(m);
                    return true;
                }, IntPtr.Zero);
            }
            catch { }

            // WMI 映射
            try
            {
                List<string> paths = new List<string>();
                List<int> currents = new List<int>();
                using (ManagementObjectSearcher s = new ManagementObjectSearcher("root\\WMI", "SELECT * FROM WmiMonitorBrightness"))
                {
                    foreach (ManagementObject mo in s.Get())
                    {
                        string inst = Convert.ToString(mo["InstanceName"]);
                        int cur = 0;
                        try { cur = Convert.ToInt32(mo["CurrentBrightness"]); } catch { }
                        paths.Add(inst == null ? "" : inst);
                        currents.Add(cur);
                    }
                }
                for (int i = 0; i < paths.Count; i++)
                {
                    MonItem target = MatchMonitor(list, paths[i]);
                    if (target == null && paths.Count == 1)
                    {
                        // 只有一路 WMI 亮度、又匹配不上时，挂到唯一没有硬件 DDC 的那台
                        foreach (MonItem m in list)
                            if (!m.Ddc) { target = m; target.WmiFallback = true; break; }
                    }
                    if (target != null)
                    {
                        target.Wmi = true;
                        if (!target.Ddc) { target.Cur = (uint)currents[i]; target.Min = 0; target.Max = 100; }
                    }
                }
            }
            catch { }

            return list;
        }

        // 设备实例路径形如 MONITOR\AUO6DA8\{4d36...}\0001
        // WMI 实例名形如   DISPLAY\AUO6DA8\5&bdf25b6&0&UID256_0
        // 两者枚举器不同，但中间那段硬件 ID 一致，用它做匹配
        private static string HardwareId(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            string[] parts = path.Split('\\');
            return parts.Length >= 2 ? parts[1] : path;
        }

        private static MonItem MatchMonitor(List<MonItem> list, string wmiInstance)
        {
            string key = HardwareId(wmiInstance);
            if (key.Length == 0) return null;
            foreach (MonItem m in list)
                if (HardwareId(m.InstancePath).Equals(key, StringComparison.OrdinalIgnoreCase)) return m;
            return null;
        }

        public static void Release(List<MonItem> mons)
        {
            try
            {
                foreach (MonItem m in mons)
                {
                    if (m.Physical.Count == 0) continue;
                    Native.PHYSICAL_MONITOR[] pm = new Native.PHYSICAL_MONITOR[m.Physical.Count];
                    for (int i = 0; i < m.Physical.Count; i++) pm[i].hPhysicalMonitor = m.Physical[i];
                    Native.DestroyPhysicalMonitors((uint)pm.Length, pm);
                    m.Physical.Clear();
                }
            }
            catch { }
        }

        // 读取当前平均亮度百分比，无法读取时返回 -1
        public static int CurrentPercent(List<MonItem> mons)
        {
            int sum = 0, cnt = 0;
            foreach (MonItem m in mons)
            {
                if (m.Ddc && m.Max > m.Min) { sum += (int)Math.Round((m.Cur - m.Min) * 100.0 / (m.Max - m.Min)); cnt++; }
                else if (m.Wmi) { sum += (int)m.Cur; cnt++; }
            }
            return cnt == 0 ? -1 : sum / cnt;
        }

        public static string SetAll(int percent, double gammaMin)
        {
            List<MonItem> mons = Enumerate();
            int hw = 0, wm = 0, gm = 0, fail = 0;
            StringBuilder detail = new StringBuilder();
            try
            {
                foreach (MonItem m in mons)
                {
                    bool ok = false;

                    if (m.Ddc)
                    {
                        uint target = m.Min + (uint)Math.Round((m.Max - m.Min) * percent / 100.0);
                        foreach (IntPtr h in m.Physical)
                        {
                            if (Native.SetMonitorBrightness(h, target)) { ok = true; }
                        }
                        if (ok) { hw++; m.Status = "硬件 DDC/CI -> " + target + " / " + m.Max; }
                    }
                    if (!ok && m.Wmi)
                    {
                        if (SetWmi(m, percent, m.WmiFallback)) { wm++; ok = true; m.Status = "WMI -> " + percent + "%"; }
                    }
                    if (!ok)
                    {
                        double scale = percent >= 100 ? 1.0 : Math.Max(gammaMin, percent / 100.0);
                        if (SetGamma(m, scale)) { gm++; ok = true; m.Status = "软件 Gamma -> " + scale.ToString("0.00", CultureInfo.InvariantCulture); }
                    }
                    if (!ok) { fail++; m.Status = "未能控制"; }

                    // 硬件可用时，清理此前遗留的软件 Gamma
                    if (ok && (m.Ddc || m.Wmi) && GammaApplied.Contains(m.DeviceName))
                        ResetGamma(m);

                    detail.AppendLine("  " + m.DisplayName + " (" + m.DeviceName + ")：" + m.Status);
                }
            }
            finally { Release(mons); }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("共 " + mons.Count + " 台显示器，目标亮度 " + percent + "%");
            sb.AppendLine("硬件控制 " + hw + " 台 | WMI " + wm + " 台 | 软件Gamma " + gm + " 台" + (fail > 0 ? " | 失败 " + fail + " 台" : ""));
            sb.Append(detail.ToString());
            return sb.ToString();
        }

        private static bool SetWmi(MonItem m, int percent, bool forceFirst)
        {
            try
            {
                using (ManagementObjectSearcher s = new ManagementObjectSearcher("root\\WMI", "SELECT * FROM WmiMonitorBrightnessMethods"))
                {
                    foreach (ManagementObject mo in s.Get())
                    {
                        string inst = Convert.ToString(mo["InstanceName"]);
                        if (string.IsNullOrEmpty(inst)) continue;
                        bool match = HardwareId(inst).Equals(HardwareId(m.InstancePath), StringComparison.OrdinalIgnoreCase)
                                     && HardwareId(inst).Length > 0;
                        if (!match && !forceFirst) continue;
                        ManagementBaseObject inParams = mo.GetMethodParameters("WmiSetBrightness");
                        inParams["Timeout"] = (uint)1;
                        inParams["Brightness"] = (byte)Math.Max(0, Math.Min(100, percent));
                        mo.InvokeMethod("WmiSetBrightness", inParams, null);
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        public static bool SetGamma(MonItem m, double scale)
        {
            IntPtr hdc = IntPtr.Zero;
            try
            {
                hdc = Native.CreateDC(null, m.DeviceName, null, IntPtr.Zero);
                if (hdc == IntPtr.Zero) hdc = Native.CreateDC("DISPLAY", m.DeviceName, null, IntPtr.Zero);
                if (hdc == IntPtr.Zero) return false;

                if (!GammaSaved.ContainsKey(m.DeviceName))
                {
                    ushort[] cur = new ushort[768];
                    if (Native.GetDeviceGammaRamp(hdc, cur)) GammaSaved[m.DeviceName] = cur;
                }

                ushort[] ramp = new ushort[768];
                for (int i = 0; i < 256; i++)
                {
                    int v = (int)Math.Round(i * 257 * scale);
                    if (v < 0) v = 0; if (v > 65535) v = 65535;
                    ushort u = (ushort)v;
                    ramp[i] = u; ramp[i + 256] = u; ramp[i + 512] = u;
                }
                bool ok = Native.SetDeviceGammaRamp(hdc, ramp);
                if (ok)
                {
                    if (scale >= 0.999) GammaApplied.Remove(m.DeviceName);
                    else GammaApplied.Add(m.DeviceName);
                }
                return ok;
            }
            catch { return false; }
            finally { if (hdc != IntPtr.Zero) Native.DeleteDC(hdc); }
        }

        public static void ResetGamma(MonItem m)
        {
            ushort[] saved;
            if (GammaSaved.TryGetValue(m.DeviceName, out saved))
            {
                IntPtr hdc = Native.CreateDC(null, m.DeviceName, null, IntPtr.Zero);
                if (hdc == IntPtr.Zero) hdc = Native.CreateDC("DISPLAY", m.DeviceName, null, IntPtr.Zero);
                if (hdc != IntPtr.Zero)
                {
                    try { Native.SetDeviceGammaRamp(hdc, saved); } catch { }
                    Native.DeleteDC(hdc);
                }
            }
            else SetGamma(m, 1.0);
            GammaApplied.Remove(m.DeviceName);
        }

        public static void ResetAllGamma()
        {
            foreach (string dev in new List<string>(GammaApplied))
            {
                MonItem m = new MonItem();
                m.DeviceName = dev;
                ResetGamma(m);
            }
        }

        public static string Diagnostics()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("diode 诊断报告  v" + AppInfo.Version + "   " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("程序版本：" + AppInfo.Version);
            sb.AppendLine("程序路径：" + Application.ExecutablePath);
            float sc = Dpi.SystemScale();
            sb.AppendLine("DPI 感知：PerMonitorV2（由嵌入 manifest 声明）");
            int aw = -99;
            try { aw = Native.GetAwarenessFromDpiAwarenessContext(Native.GetThreadDpiAwarenessContext()); } catch { }
            string awDesc = aw == 2 ? "已生效 (PerMonitor)"
                          : aw == 1 ? "未生效！仅 System 感知"
                          : aw == 0 ? "未生效！Unaware"
                          : "未知(" + aw + ")";
            sb.AppendLine("DPI 感知实测：" + awDesc + "    系统缩放：" + Math.Round(sc * 100) + "%    托盘图标尺寸：" + TrayIcon.PickSize() + "px");
            sb.AppendLine("");
            List<MonItem> mons = Enumerate();
            try
            {
                sb.AppendLine("检测到 " + mons.Count + " 台显示器：");
                foreach (MonItem m in mons)
                {
                    sb.AppendLine("----------------------------------------");
                    sb.AppendLine("名称      ：" + m.DisplayName);
                    sb.AppendLine("设备      ：" + m.DeviceName);
                    sb.AppendLine("设备 ID   ：" + m.InstancePath);
                    sb.AppendLine("物理句柄  ：" + m.Physical.Count + " 个" + (m.PhysicalDesc.Count > 0 ? " (" + string.Join(", ", m.PhysicalDesc.ToArray()) + ")" : ""));
                    sb.AppendLine("DDC/CI    ：" + (m.Ddc ? ("可用  当前 " + m.Cur + "  范围 " + m.Min + "~" + m.Max) : "不可用"));
                    sb.AppendLine("WMI       ：" + (m.Wmi ? ("可用" + (m.Ddc ? "" : "  当前 " + m.Cur)) : "不可用"));
                    sb.AppendLine("软件Gamma ：" + (SetGammaProbe(m) ? "可用" : "不可用"));
                }
            }
            finally { Release(mons); }
            sb.AppendLine("----------------------------------------");
            sb.AppendLine("说明：DDC/CI 或 WMI 任一可用即为硬件调光；两者都不可用时自动回退到软件 Gamma。");
            return sb.ToString();
        }

        private static bool SetGammaProbe(MonItem m)
        {
            IntPtr hdc = Native.CreateDC(null, m.DeviceName, null, IntPtr.Zero);
            if (hdc == IntPtr.Zero) hdc = Native.CreateDC("DISPLAY", m.DeviceName, null, IntPtr.Zero);
            if (hdc == IntPtr.Zero) return false;
            try
            {
                ushort[] cur = new ushort[768];
                bool ok = Native.GetDeviceGammaRamp(hdc, cur);
                if (ok) ok = Native.SetDeviceGammaRamp(hdc, cur);
                return ok;
            }
            catch { return false; }
            finally { Native.DeleteDC(hdc); }
        }
    }

    // ---------------- 配置 ----------------
    internal class Config
    {
        public int Dim = 0;
        public int Bright = 100;
        public string HotKey = "B";
        public double GammaMin = 0.10;

        private static string FilePath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini"); }
        }

        public static Config Load()
        {
            Config c = new Config();
            try
            {
                if (!File.Exists(FilePath))
                {
                    c.Save();
                }
                else
                {
                    foreach (string raw in File.ReadAllLines(FilePath, Encoding.UTF8))
                    {
                        string s = raw.Trim();
                        if (s.Length == 0 || s.StartsWith(";") || s.StartsWith("#")) continue;
                        int i = s.IndexOf('=');
                        if (i <= 0) continue;
                        string k = s.Substring(0, i).Trim().ToLowerInvariant();
                        string v = s.Substring(i + 1).Trim();
                        int iv; double dv;
                        switch (k)
                        {
                            case "dim": if (int.TryParse(v, out iv)) c.Dim = iv; break;
                            case "bright": if (int.TryParse(v, out iv)) c.Bright = iv; break;
                            case "hotkey": c.HotKey = v; break;
                            case "gammamin": if (double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out dv)) c.GammaMin = dv; break;
                        }
                    }
                }
            }
            catch { }
            c.Dim = Math.Max(0, Math.Min(100, c.Dim));
            c.Bright = Math.Max(0, Math.Min(100, c.Bright));
            c.GammaMin = Math.Max(0.02, Math.Min(1.0, c.GammaMin));
            return c;
        }

        public void Save()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("; diode 配置；修改后保存即可生效（无需重启）");
                sb.AppendLine("; 点一下托盘图标时切换到的“最暗”亮度百分比");
                sb.AppendLine("Dim=" + Dim);
                sb.AppendLine("; “最亮”亮度百分比");
                sb.AppendLine("Bright=" + Bright);
                sb.AppendLine("; 全局热键 Ctrl+Alt+<此键>，单个字母或数字，留空则禁用热键");
                sb.AppendLine("HotKey=" + HotKey);
                sb.AppendLine("; 硬件调光不可用时的软件 Gamma 最低系数（0.02~1.0，越小越暗）");
                sb.AppendLine("GammaMin=" + GammaMin.ToString("0.00", CultureInfo.InvariantCulture));
                File.WriteAllText(FilePath, sb.ToString(), new UTF8Encoding(false));
            }
            catch { }
        }
    }

    // ---------------- 隐藏窗体（承载全局热键） ----------------
    internal class HiddenForm : Form
    {
        public event EventHandler HotKeyPressed;
        public event EventHandler DisplayChanged;

        public HiddenForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Location = new Point(-5000, -5000);
            Size = new Size(1, 1);
        }

        protected override void SetVisibleCore(bool value) { base.SetVisibleCore(false); }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_HOTKEY && HotKeyPressed != null) HotKeyPressed(this, EventArgs.Empty);
            if (m.Msg == 0x007E && DisplayChanged != null) DisplayChanged(this, EventArgs.Empty); // WM_DISPLAYCHANGE
            base.WndProc(ref m);
        }
    }

    // ---------------- 托盘应用 ----------------
    internal class TrayApp : ApplicationContext
    {
        private NotifyIcon ni;
        private HiddenForm form;
        private Config cfg;
        private bool isDark = false;
        private bool busy = false;
        private Font menuFont = null;
        private readonly object sync = new object();
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValue = "diode";

        public TrayApp()
        {
            cfg = Config.Load();
            form = new HiddenForm();
            form.HotKeyPressed += delegate { Toggle(); };
            form.DisplayChanged += delegate { RefreshTip(); RefreshIcon(); };
            IntPtr dummy = form.Handle; // 强制创建句柄

            ni = new NotifyIcon();
            ni.Text = "diode（左键切换明暗）";
            ni.Icon = MakeIcon(false);
            ni.ContextMenuStrip = BuildMenu();
            ni.Visible = true;
            ni.MouseClick += OnIconClick;
            ni.MouseDoubleClick += OnIconClick;

            RegisterHot();
            RefreshTip();
        }

        private ContextMenuStrip BuildMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("调到最暗 (" + cfg.Dim + "%)", null, delegate { Apply(cfg.Dim); });
            menu.Items.Add("调到最亮 (" + cfg.Bright + "%)", null, delegate { Apply(cfg.Bright); });
            menu.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem miRun = new ToolStripMenuItem("开机自动启动");
            miRun.Checked = IsAutoStart();
            miRun.Click += delegate { SetAutoStart(!IsAutoStart()); miRun.Checked = IsAutoStart(); };
            menu.Items.Add(miRun);
            menu.Items.Add("编辑配置文件", null, delegate
            {
                try { System.Diagnostics.Process.Start("notepad.exe", Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini")); }
                catch { }
            });
            menu.Items.Add("诊断信息", null, delegate { ShowDiag(); });
            menu.Items.Add(new ToolStripSeparator());
            ToolStripItem miVer = menu.Items.Add("diode v" + AppInfo.Version);
            miVer.Enabled = false;
            menu.Items.Add("退出", null, delegate { Exit(); });
            menu.ShowImageMargin = false;
            menu.Opening += OnMenuOpening;
            return menu;
        }

        // WinForms 的 ContextMenuStrip 不会自己跟随屏幕 DPI 变化，
        // 每次弹出前按当前所在屏的缩放比重算字体与图像尺寸。
        private void OnMenuOpening(object sender, CancelEventArgs e)
        {
            ContextMenuStrip menu = sender as ContextMenuStrip;
            if (menu == null) return;
            try
            {
                float scale = Dpi.ScaleAtCursor();      // 弹出位置所在屏幕
                float sysScale = Dpi.SystemScale();     // SystemFonts 的基准
                Font mf = SystemFonts.MenuFont;
                float basePt = sysScale > 0.01f ? mf.SizeInPoints / sysScale : mf.SizeInPoints;
                Font want = new Font(mf.FontFamily, basePt * scale, mf.Style, GraphicsUnit.Point);

                if (menu.Font == null || Math.Abs(menu.Font.Size - want.Size) > 0.01f)
                {
                    Font prev = menuFont;
                    menuFont = want;
                    menu.Font = want;
                    if (prev != null) { try { prev.Dispose(); } catch { } }
                }
                else { want.Dispose(); }

                int px = (int)Math.Round(16f * scale);
                if (px < 16) px = 16;
                if (menu.ImageScalingSize.Width != px) menu.ImageScalingSize = new Size(px, px);
            }
            catch { }
        }

        private void RegisterHot()
        {
            try
            {
                if (string.IsNullOrEmpty(cfg.HotKey)) return;
                char c = cfg.HotKey.Trim().ToUpperInvariant()[0];
                Native.RegisterHotKey(form.Handle, 1, Native.MOD_CONTROL | Native.MOD_ALT | Native.MOD_NOREPEAT, (uint)c);
            }
            catch { }
        }

        private void OnIconClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) Toggle();
        }

        private void Toggle()
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                int target;
                try
                {
                    List<MonItem> mons = Bright.Enumerate();
                    int cur = Bright.CurrentPercent(mons);
                    Bright.Release(mons);
                    int mid = (cfg.Dim + cfg.Bright) / 2;
                    bool goDark;
                    if (cur < 0) goDark = !isDark;      // 读不到当前亮度时按上次状态切换
                    else goDark = cur > mid;            // 现在偏亮 -> 调暗；现在偏暗 -> 调亮
                    target = goDark ? cfg.Dim : cfg.Bright;
                }
                catch { target = isDark ? cfg.Bright : cfg.Dim; }
                ApplyCore(target);
            });
        }

        private void Apply(int percent)
        {
            ThreadPool.QueueUserWorkItem(delegate { ApplyCore(percent); });
        }

        private void ApplyCore(int percent)
        {
            lock (sync) { if (busy) return; busy = true; }
            try
            {
                string result = Bright.SetAll(percent, cfg.GammaMin);
                isDark = percent <= (cfg.Dim + cfg.Bright) / 2;
                form.BeginInvoke(new MethodInvoker(delegate
                {
                    SetIcon(isDark);
                    RefreshTip();
                    ni.ShowBalloonTip(2500, isDark ? "已调至最暗" : "已调至最亮", result, ToolTipIcon.Info);
                }));
            }
            catch (Exception ex)
            {
                try
                {
                    form.BeginInvoke(new MethodInvoker(delegate
                    {
                        ni.ShowBalloonTip(4000, "diode 出错", ex.Message, ToolTipIcon.Error);
                    }));
                }
                catch { }
            }
            finally { lock (sync) { busy = false; } }
        }

        // DPI / 显示器配置变化时重建托盘图标，保证不糊
        private void RefreshIcon()
        {
            try { SetIcon(isDark); } catch { }
        }

        private void SetIcon(bool dark)
        {
            try
            {
                Icon old = ni.Icon;
                ni.Icon = MakeIcon(dark);
                if (old != null) old.Dispose();
            }
            catch { }
        }

        private void RefreshTip()
        {
            try { ni.Text = "diode —— 左键切换最暗/最亮，Ctrl+Alt+" + cfg.HotKey.ToUpperInvariant(); }
            catch { }
        }

        private void ShowDiag()
        {
            try
            {
                string text = Bright.Diagnostics();
                string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "diode-诊断.txt");
                File.WriteAllText(path, text, new UTF8Encoding(true));
                System.Diagnostics.Process.Start("notepad.exe", path);
            }
            catch { }
        }

        private bool IsAutoStart()
        {
            try
            {
                using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey, false))
                {
                    if (k == null) return false;
                    object v = k.GetValue(RunValue);
                    return v != null;
                }
            }
            catch { return false; }
        }

        private void SetAutoStart(bool on)
        {
            try
            {
                using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (k == null) return;
                    if (on) k.SetValue(RunValue, "\"" + Application.ExecutablePath + "\"");
                    else k.DeleteValue(RunValue, false);
                }
            }
            catch { }
        }

        private static Icon MakeIcon(bool dark)
        {
            return TrayIcon.Create(dark);
        }

        private void Exit()
        {
            try { Native.UnregisterHotKey(form.Handle, 1); } catch { }
            try { Bright.ResetAllGamma(); } catch { }
            if (menuFont != null) { try { menuFont.Dispose(); } catch { } menuFont = null; }
            if (ni != null) { ni.Visible = false; ni.Dispose(); }
            ExitThread();
        }

        protected override void Dispose(bool disposing)
        {
            try { Bright.ResetAllGamma(); } catch { }
            base.Dispose(disposing);
        }
    }

    // ---------------- 入口 ----------------
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            if (args != null && args.Length > 0)
            {
                string a0 = args[0].ToLowerInvariant();
                if (a0 == "--version" || a0 == "/version" || a0 == "-v")
                {
                    bool attached = false;
                    try { attached = Native.AttachConsole(Native.ATTACH_PARENT_PROCESS); } catch { }
                    if (attached) Console.WriteLine("diode " + AppInfo.Version);
                    else MessageBox.Show("diode v" + AppInfo.Version, "diode",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                if (a0 == "--diag" || a0 == "/diag")
                {
                    string text = Bright.Diagnostics();
                    string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "diode-诊断.txt");
                    try { File.WriteAllText(path, text, new UTF8Encoding(true)); } catch { }
                    if (!(args.Length > 1 && args[1] == "silent"))
                        MessageBox.Show(text, "diode 诊断", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                if ((a0 == "--set" || a0 == "/set") && args.Length > 1)
                {
                    int p;
                    if (int.TryParse(args[1], out p))
                    {
                        Config c = Config.Load();
                        Bright.SetAll(Math.Max(0, Math.Min(100, p)), c.GammaMin);
                    }
                    return;
                }
            }

            bool createdNew;
            using (Mutex m = new Mutex(true, "diode_SingleInstance_Mutex", out createdNew))
            {
                if (!createdNew) return;
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new TrayApp());
            }
        }
    }
}
