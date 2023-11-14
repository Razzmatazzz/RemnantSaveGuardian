using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Wpf.Ui.Appearance;
using Wpf.Ui.Interop;

namespace RemnantSaveGuardian.Helpers
{
    internal class WindowDwmHelper
    {
        internal class Win32API
        {
            /// <summary>
            /// Determines whether the specified window handle identifies an existing window.
            /// </summary>
            /// <param name="hWnd">A handle to the window to be tested.</param>
            /// <returns>If the window handle identifies an existing window, the return value is nonzero.</returns>
            [DllImport("user32.dll", CharSet = CharSet.Auto)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool IsWindow([In] IntPtr hWnd);
        }
        internal class Utilities
        {
            private static readonly PlatformID _osPlatform = Environment.OSVersion.Platform;

            private static readonly Version _osVersion = Environment.OSVersion.Version;

            /// <summary>
            /// Whether the operating system is NT or newer. 
            /// </summary>
            public static bool IsNT => _osPlatform == PlatformID.Win32NT;

            /// <summary>
            /// Whether the operating system version is greater than or equal to 6.0.
            /// </summary>
            public static bool IsOSVistaOrNewer => _osVersion >= new Version(6, 0);

            /// <summary>
            /// Whether the operating system version is greater than or equal to 6.1.
            /// </summary>
            public static bool IsOSWindows7OrNewer => _osVersion >= new Version(6, 1);

            /// <summary>
            /// Whether the operating system version is greater than or equal to 6.2.
            /// </summary>
            public static bool IsOSWindows8OrNewer => _osVersion >= new Version(6, 2);

            /// <summary>
            /// Whether the operating system version is greater than or equal to 10.0* (build 10240).
            /// </summary>
            public static bool IsOSWindows10OrNewer => _osVersion.Build >= 10240;

            /// <summary>
            /// Whether the operating system version is greater than or equal to 10.0* (build 22000).
            /// </summary>
            public static bool IsOSWindows11OrNewer => _osVersion.Build >= 22000;

            /// <summary>
            /// Whether the operating system version is greater than or equal to 10.0* (build 22523).
            /// </summary>
            public static bool IsOSWindows11Insider1OrNewer => _osVersion.Build >= 22523;

            /// <summary>
            /// Whether the operating system version is greater than or equal to 10.0* (build 22557).
            /// </summary>
            public static bool IsOSWindows11Insider2OrNewer => _osVersion.Build >= 22557;
        }
        internal enum UXMaterials
        {
            None = BackgroundType.None,
            Mica = BackgroundType.Mica,
            Acrylic = BackgroundType.Acrylic
        }
        internal static Color transparentColor = Color.FromArgb(0x1, 0x80, 0x80, 0x80);
        internal static Brush transparentBrush = new SolidColorBrush(transparentColor);
        internal static bool IsSupported(UXMaterials type)
        {
            return type switch
            {
                UXMaterials.Mica => Utilities.IsOSWindows11OrNewer,
                UXMaterials.Acrylic => Utilities.IsOSWindows7OrNewer,
                UXMaterials.None => true,
                _ => false
            };
        }
        internal static WindowInteropHelper GetWindow(Window window)
        {
            return new WindowInteropHelper(window);
        }
        internal static bool ApplyDwm(Window window, UXMaterials type)
        {
            IntPtr handle = GetWindow(window).Handle;

            if (type == UXMaterials.Mica && !Utilities.IsOSWindows11Insider1OrNewer)
            {
                type = UXMaterials.Acrylic;
            }
            
            if (!IsSupported(type))
                return false;

            if (handle == IntPtr.Zero)
                return false;

            if (!Win32API.IsWindow(handle))
                return false;

            if (type == UXMaterials.None)
            {
                RestoreBackground(window);
                return UnsafeNativeMethods.RemoveWindowBackdrop(handle);
            }
            // First release of Windows 11
            if (!Utilities.IsOSWindows11Insider1OrNewer)
            {
                if (type == UXMaterials.Mica)
                {
                    RemoveBackground(window);
                    return UnsafeNativeMethods.ApplyWindowLegacyMicaEffect(handle);
                }

                if (type == UXMaterials.Acrylic)
                {
                    return UnsafeNativeMethods.ApplyWindowLegacyMicaEffect(handle);
                }

                return false;
            }

            // Newer Windows 11 versions
            RemoveBackground(window);
            return UnsafeNativeMethods.ApplyWindowBackdrop(handle, (BackgroundType)type);
        }

        /// <summary>
        /// Tries to remove background from <see cref="Window"/> and it's composition area.
        /// </summary>
        /// <param name="window">Window to manipulate.</param>
        /// <returns><see langword="true"/> if operation was successful.</returns>
        internal static void RemoveBackground(Window window)
        {
            if (window == null)
                return;

            // Remove background from visual root
            window.Background = transparentBrush;
        }
        internal static void RestoreBackground(Window window)
        {
            if (window == null)
                return;

            var backgroundBrush = window.Resources["ApplicationBackgroundBrush"];

            // Manual fallback
            if (backgroundBrush is not SolidColorBrush)
                backgroundBrush = GetFallbackBackgroundBrush();

            window.Background = (SolidColorBrush)backgroundBrush;
        }
        private static Brush GetFallbackBackgroundBrush()
        {
            return Theme.GetAppTheme() == ThemeType.Dark
                ? new SolidColorBrush(Color.FromArgb(0xFF, 0x20, 0x20, 0x20))
                : new SolidColorBrush(Color.FromArgb(0xFF, 0xFA, 0xFA, 0xFA));
        }
    }
}
