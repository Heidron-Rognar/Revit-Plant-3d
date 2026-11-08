using System;
using System.Windows.Interop;
using Autodesk.Revit.UI;
using PlantDraw.Core;

namespace PlantDraw.UI
{
    /// <summary>Owns the single modeless pad window.</summary>
    public static class DirectionPadHost
    {
        private static DirectionPadWindow _window;
        private static bool _ownerApplied;

        public static bool IsOpen
        {
            get { return _window != null && _window.IsVisible; }
        }

        public static void Show(UIApplication application)
        {
            if (_window == null)
            {
                _window = new DirectionPadWindow();
                _window.Closed += OnWindowClosed;
                _ownerApplied = false;
            }

            if (!_ownerApplied)
            {
                IntPtr handle = GetMainWindowHandle(application);
                if (handle != IntPtr.Zero)
                {
                    try
                    {
                        new WindowInteropHelper(_window).Owner = handle;
                        _ownerApplied = true;
                    }
                    catch
                    {
                        // Owning is cosmetic; keep the pad independent if it fails.
                    }
                }
            }

            if (!_window.IsVisible)
            {
                _window.Show();
            }
            else
            {
                _window.Activate();
            }

            _window.RequestRefresh();
        }

        public static void Refresh()
        {
            if (_window != null && _window.IsVisible)
            {
                _window.RequestRefresh();
            }
        }

        public static void Close()
        {
            if (_window == null)
            {
                return;
            }

            try
            {
                _window.Closed -= OnWindowClosed;
                _window.Close();
            }
            catch
            {
                // Revit is shutting down; nothing to clean up.
            }

            _window = null;
            _ownerApplied = false;
        }

        private static void OnWindowClosed(object sender, EventArgs e)
        {
            _window = null;
            _ownerApplied = false;
        }

        private static IntPtr GetMainWindowHandle(UIApplication application)
        {
            try
            {
                return application == null ? IntPtr.Zero : application.MainWindowHandle;
            }
            catch
            {
                return IntPtr.Zero;
            }
        }
    }
}
