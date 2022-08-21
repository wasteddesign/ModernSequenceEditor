using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using ModernSequenceEditor;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;

namespace WDE.ModernSequenceEditor
{
    public class CustomSequencerWindow : NativeWindow, IDisposable
    {
        public SequenceEditor SequenceEditor { get; internal set; }
        public SequencerEditorWindowUserControl SequenceEditorWindow { get; }

        public static bool OneInstanceCreated = false;
        public CustomSequencerWindow(SequenceEditor seq)
        {
            SequenceEditor = seq;

            SequenceEditorWindow = new SequencerEditorWindowUserControl();
            SequenceEditorWindow.WindowStyle = WindowStyle.None;
            SequenceEditorWindow.ShowInTaskbar = false;
            SequenceEditorWindow.BorderThickness = new Thickness(0);
            SequenceEditorWindow.AllowsTransparency = false;
            SequenceEditorWindow.ResizeMode = ResizeMode.NoResize;
            SequenceEditorWindow.Effect = null;
            SequenceEditorWindow.Topmost = true;
            SequenceEditorWindow.MinHeight = 40;
            SequenceEditorWindow.MinWidth = 40;

            // This listens to Classic Sequencer Editor hwnd events
            IntPtr buzzClassicSequenceEditorHwnd = FindClassicSequenceEditor(); //FindBuzzMDIClient();
            if (buzzClassicSequenceEditorHwnd != IntPtr.Zero)
            {
                WindowInteropHelper helper = new WindowInteropHelper(SequenceEditorWindow);
                helper.Owner = buzzClassicSequenceEditorHwnd;

                SendMessage(GetChildWindows(buzzClassicSequenceEditorHwnd)[0], WM_SETREDRAW, false, 0); // Disble window updates

                this.AssignHandle(buzzClassicSequenceEditorHwnd);
            }

            Global.Buzz.PropertyChanged += Buzz_PropertyChanged;
            
            SequenceEditorWindow.Content = seq;
            seq.SetVisibility(true);

            SequenceEditorWindow.PreviewKeyDown += SequenceEditorWindow_PreviewKeyDown;

            OneInstanceCreated = true;

            // InitHook();
        }

        public void SequenceEditorWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (e.Key == Key.Up || e.Key == Key.Down)
                {
                    e.Handled = true;
                }
                else if (e.Key == Key.S)
                {
                    Global.Buzz.ExecuteCommand(BuzzCommand.SaveFile);
                    e.Handled = true;
                }
                else if (e.Key == Key.O)
                {
                    Global.Buzz.ExecuteCommand(BuzzCommand.OpenFile);
                    e.Handled = true;
                }
                else if (e.Key == Key.N)
                {
                    Global.Buzz.ExecuteCommand(BuzzCommand.NewFile);
                    e.Handled = true;
                }
                //else if (e.Key == Key.W)
                //{
                //    CreateNewSeqencerWindow();
                //    e.Handled = true;
                //}
            }
            else
            {
                if (e.Key == Key.F2)
                {
                    Global.Buzz.ActiveView = BuzzView.PatternView;
                    e.Handled = true;
                }
                else if (e.Key == Key.F3)
                {
                    Global.Buzz.ActiveView = BuzzView.MachineView;
                    e.Handled = true;
                }
                else if (e.Key == Key.F4)
                {
                    Global.Buzz.ActiveView = BuzzView.SequenceView;
                    e.Handled = true;
                }
                else if (e.Key == Key.F5)
                {

                    Global.Buzz.Playing = true;
                    e.Handled = true;
                }
                else if (e.Key == Key.F6)
                {
                    if (SequenceEditor != null)
                    {
                        SequenceEditor.PlayCursor();
                    }
                    e.Handled = true;
                }
                else if (e.Key == Key.F7)
                {
                    Global.Buzz.Recording = true;
                    e.Handled = true;
                }
                else if (e.Key == Key.F8)
                {
                    Global.Buzz.Recording = false;
                    Global.Buzz.Playing = false;
                    e.Handled = true;
                }
                else if (e.Key == Key.F9)
                {
                    Global.Buzz.ActiveView = BuzzView.WaveTableView;
                    e.Handled = true;
                }
                else if (e.Key == Key.F10 || e.SystemKey == Key.F10)
                {
                    Global.Buzz.ActiveView = BuzzView.SongInfoView;
                    e.Handled = true;
                }
                else if (e.Key == Key.F12 || e.SystemKey == Key.F12)
                {
                    Global.Buzz.AudioDeviceDisabled = !Global.Buzz.AudioDeviceDisabled;
                    e.Handled = true;
                }
            }
        }

        private void SetWindowZOrder()
        {
            WindowInteropHelper wif = new WindowInteropHelper(SequenceEditorWindow);

            IntPtr buzzOldSeq = FindClassicSequenceEditor();
            wif.Owner = buzzOldSeq;
            SetParent(wif.Handle, buzzOldSeq);
        }

        private void MainWindowObserver_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            RECT wRect;
            GetWindowRect(FindClassicSequenceEditor(), out wRect);
            UpdateWindow(wRect);
        }

        private void UpdateSize()
        {
            RECT wRect;
            GetWindowRect(FindClassicSequenceEditor(), out wRect);
            UpdateWindow(wRect);
        }

        private void Buzz_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "ActiveView")
            {
                UpdateView();
            }
        }

        public void UpdateView()
        {
            if (Global.Buzz.ActiveView != BuzzGUI.Interfaces.BuzzView.SequenceView)
            {
                SequenceEditorWindow.Visibility = Visibility.Collapsed;
            }
            else if (Global.Buzz.ActiveView == BuzzGUI.Interfaces.BuzzView.SequenceView)
            {
                RECT wRect;
                GetWindowRect(Handle, out wRect);
                UpdateWindow(wRect);
                if (!SequenceEditorWindow.IsActive)
                {
                    SetWindowZOrder();
                    SequenceEditorWindow.Show();
                }

                SequenceEditorWindow.Visibility = Visibility.Visible;
                SequenceEditorWindow.Focus();

                SequenceEditorWindow.BringIntoView();
                SequenceEditorWindow.BringToTop();

                SequenceEditor.SetVisibility(true);
                SequenceEditor.Focus();
            }
        }

        [System.Security.Permissions.PermissionSet(System.Security.Permissions.SecurityAction.Demand, Name = "FullTrust")]
        protected override void WndProc(ref Message m)
        {
            // Listen for operating system messages
            if (Global.Buzz.ActiveView == BuzzGUI.Interfaces.BuzzView.SequenceView)
            {   
                if (m.Msg == WM_PAINT)
                {
                    base.WndProc(ref m);
                }
                else if (m.Msg == WM_PRINTCLIENT)
                {
                    base.WndProc(ref m);
                }
                else if (m.Msg == WS_EX_COMPOSITED)
                {
                    base.WndProc(ref m);
                }
                else if (m.Msg == WM_SETFOCUS)
                {
                    base.WndProc(ref m);
                }
                else if (m.Msg == WM_NCACTIVATE)
                {
                    base.WndProc(ref m);
                }
                else if (m.Msg == WM_DESTROY)
                {
                    Dispose();
                    base.WndProc(ref m);
                }
                else if ( m.Msg == WM_SIZE)
                {
                    UpdateSize();
                    base.WndProc(ref m);
                }
                else
                {
                    //Global.Buzz.DCWriteLine("WM: " + m.Msg.ToString("X"));
                    base.WndProc(ref m);
                }
            }
            else
            {
                base.WndProc(ref m);
            }
        }

        private void UpdateWindow(RECT wRect)
        {   
            UpdateWindow(0, 0, Math.Max(0, wRect.Right - wRect.Left - SystemParameters.VerticalScrollBarWidth), Math.Max(0, wRect.Bottom - wRect.Top - 2 * SystemParameters.HorizontalScrollBarHeight));
        }

        private void UpdateWindow(double top, double left, double width, double height)
        {
            if (Global.Buzz.ActiveView != BuzzGUI.Interfaces.BuzzView.SequenceView)
            {
                SequenceEditorWindow.Visibility = Visibility.Collapsed;
                return;
            }

            if (!SequenceEditorWindow.IsActive)
            {
                SequenceEditorWindow.Show();
                SetWindowZOrder();
            }

            SequenceEditorWindow.Visibility = Visibility.Visible;
            SequenceEditorWindow.Topmost = true;
            SequenceEditorWindow.BringToTop();

            WindowInteropHelper wif = new WindowInteropHelper(SequenceEditorWindow);
            
            SetWindowPos(wif.Handle, IntPtr.Zero, (int)top, (int)left, (int)width, (int)height, 0);
        }

        public IntPtr FindBuzzMDIClient()
        {
            IntPtr result;
            IntPtr hWndParent = GetBuzzWindow();
            result = EnumAllWindows(hWndParent, "MDIClient").FirstOrDefault();

            return result;
        }

        public static IntPtr FindClassicSequenceEditor()
        {
            IntPtr result;
            IntPtr hWndParent = GetBuzzWindow();
            result = EnumAllWindowCaptions(hWndParent, "Sequence Editor").FirstOrDefault();

            return result;
        }

        public void Dispose()
        {
            SequenceEditorWindow.PreviewKeyDown -= SequenceEditorWindow_PreviewKeyDown;
            SequenceEditorWindow.Close();
            SequenceEditor.Release();
            SequenceEditor = null;
            this.ReleaseHandle();
            Global.Buzz.PropertyChanged -= Buzz_PropertyChanged;

            IntPtr buzzClassicSequenceEditorHwnd = FindClassicSequenceEditor();
            SendMessage(GetChildWindows(buzzClassicSequenceEditorHwnd)[0], WM_SETREDRAW, true, 0); // Enalbe window updates

            OneInstanceCreated = false;
        }


        public static IntPtr GetBuzzWindow()
        {
            // Beautiful!
            IntPtr buzzWnd = Global.Buzz.MachineViewHWND;
            //Global.Buzz.DCWriteLine("HWND Caption: " + GetWinCaption(buzzWnd));
            buzzWnd = GetParent(buzzWnd);
            //Global.Buzz.DCWriteLine("HWND Caption: " + GetWinCaption(buzzWnd));
            buzzWnd = GetParent(buzzWnd);
            //Global.Buzz.DCWriteLine("HWND Caption: " + GetWinCaption(buzzWnd));
            buzzWnd = GetParent(buzzWnd);
            //Global.Buzz.DCWriteLine("HWND Caption: " + GetWinCaption(buzzWnd));

            return buzzWnd;
        }

        // Try to listen events
        private HookProc myCallbackDelegate = null;

        public void InitHook()
        {
            // initialize our delegate
            this.myCallbackDelegate = new HookProc(this.HookCallbackFunction);

            // setup a keyboard hook
            SetWindowsHookEx(HookType.WH_KEYBOARD, this.myCallbackDelegate, IntPtr.Zero, (uint)AppDomain.GetCurrentThreadId());
        }

        private int HookCallbackFunction(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code < 0)
            {
                //you need to call CallNextHookEx without further processing
                //and return the value returned by CallNextHookEx
                return CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
            }
            // we can convert the 2nd parameter (the key code) to a System.Windows.Forms.Keys enum constant
            Keys keyPressed = (Keys)wParam.ToInt32();
            if (keyPressed == Keys.F6)
            {
                if (SequenceEditor != null)
                    SequenceEditor.PlayCursor();
            }
            //return the value returned by CallNextHookEx
            return 0; //CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
        }

        #region Parameters

        public const int WM_DESTROY = 2;
        public const int WM_MOVE = 0x3;
        public const int WM_SIZE = 0x5;
        public const int WM_SETFOCUS = 0x7;
        public const int WM_NCACTIVATE = 0x86;
        public const int WM_MDIACTIVATE = 0x222;
        public const int WM_MDIDEACTIVATE = 0x229;
        public const int WM_PAINT = 0xf; 
        public const int WM_PRINTCLIENT = 0x318;
        public const int WS_EX_COMPOSITED = 0x2000000;
        public const int SW_SHOWNORMAL = 1;

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        public struct PAINTSTRUCT
        {   
            public IntPtr hdc;
            public bool fErase;
            public RECT rcPaint;
            public bool fRestore;
            public bool fIncUpdate;
        }

        #endregion

        #region user32_calls_and_structs
        // Stuff

        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = false)]
        internal static extern IntPtr BeginPaint(HandleRef hWnd, [In][Out] ref PAINTSTRUCT lpPaint);

        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = false)]
        internal static extern bool EndPaint(HandleRef hWnd, ref PAINTSTRUCT lpPaint);

        [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Auto)]
        public static extern IntPtr GetParent(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        static readonly IntPtr HWND_TOP = new IntPtr(0);
        static readonly IntPtr HWND_BOTTOM = new IntPtr(1);

        /// <summary>
        /// Window handles (HWND) used for hWndInsertAfter
        /// </summary>
        public static class HWND
        {
            public static IntPtr
            NoTopMost = new IntPtr(-2),
            TopMost = new IntPtr(-1),
            Top = new IntPtr(0),
            Bottom = new IntPtr(1);
        }

        /// <summary>
        /// SetWindowPos Flags
        /// </summary>
        public struct SetWindowPosFlags
        {
            public static readonly int
            NOSIZE = 0x0001,
            NOMOVE = 0x0002,
            NOZORDER = 0x0004,
            NOREDRAW = 0x0008,
            NOACTIVATE = 0x0010,
            DRAWFRAME = 0x0020,
            FRAMECHANGED = 0x0020,
            SHOWWINDOW = 0x0040,
            HIDEWINDOW = 0x0080,
            NOCOPYBITS = 0x0100,
            NOOWNERZORDER = 0x0200,
            NOREPOSITION = 0x0200,
            NOSENDCHANGING = 0x0400,
            DEFERERASE = 0x2000,
            ASYNCWINDOWPOS = 0x4000;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int SendMessage(IntPtr hWnd, Int32 wMsg, bool wParam, Int32 lParam);
        private const int WM_SETREDRAW = 11;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

        public delegate bool Win32Callback(IntPtr hwnd, IntPtr lParam);
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder strText, int maxCount);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool AllowSetForegroundWindow(uint dwProcessId);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumChildWindows(IntPtr parentHandle, Win32Callback callback, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static public extern IntPtr GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        private static bool EnumWindow(IntPtr handle, IntPtr pointer)
        {
            GCHandle gch = GCHandle.FromIntPtr(pointer);
            List<IntPtr> list = gch.Target as List<IntPtr>;
            if (list == null)
                throw new InvalidCastException("GCHandle Target could not be cast as List<IntPtr>");
            list.Add(handle);
            return true;
        }

        public static List<IntPtr> GetChildWindows(IntPtr parent)
        {
            List<IntPtr> result = new List<IntPtr>();
            GCHandle listHandle = GCHandle.Alloc(result);
            try
            {
                Win32Callback childProc = new Win32Callback(EnumWindow);
                EnumChildWindows(parent, childProc, GCHandle.ToIntPtr(listHandle));
            }
            finally
            {
                if (listHandle.IsAllocated)
                    listHandle.Free();
            }
            return result;
        }

        public static string GetWinClass(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
                return null;
            StringBuilder classname = new StringBuilder(100);
            IntPtr result = GetClassName(hwnd, classname, classname.Capacity);
            if (result != IntPtr.Zero)
                return classname.ToString();
            return null;
        }

        public static string GetWinCaption(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
                return null;
            StringBuilder classname = new StringBuilder(100);
            int result = GetWindowText(hwnd, classname, classname.Capacity);
            if (result > 0)
                return classname.ToString();
   
            return null;
        }

        public static IEnumerable<IntPtr> EnumAllWindows(IntPtr hwnd, string childClassName)
        {
            List<IntPtr> children = GetChildWindows(hwnd);
            if (children == null)
                yield break;
            foreach (IntPtr child in children)
            {
                if (GetWinClass(child) == childClassName)
                    yield return child;
                foreach (var childchild in EnumAllWindows(child, childClassName))
                    yield return childchild;
            }
        }

        public static IEnumerable<IntPtr> EnumAllWindowCaptions(IntPtr hwnd, string childClassName)
        {
            List<IntPtr> children = GetChildWindows(hwnd);
            if (children == null)
                yield break;
            foreach (IntPtr child in children)
            {
                if (GetWinCaption(child) == childClassName)
                    yield return child;
                foreach (var childchild in EnumAllWindowCaptions(child, childClassName))
                    yield return childchild;
            }
        }

        public enum HookType : int
        {
            /// <summary>
            /// Installs a hook procedure that monitors messages generated as a result of an input event in a dialog box,
            /// message box, menu, or scroll bar. For more information, see the MessageProc hook procedure.
            /// </summary>
            WH_MSGFILTER = -1,
            /// <summary>
            /// Installs a hook procedure that records input messages posted to the system message queue. This hook is
            /// useful for recording macros. For more information, see the JournalRecordProc hook procedure.
            /// </summary>
            WH_JOURNALRECORD = 0,
            /// <summary>
            /// Installs a hook procedure that posts messages previously recorded by a WH_JOURNALRECORD hook procedure.
            /// For more information, see the JournalPlaybackProc hook procedure.
            /// </summary>
            WH_JOURNALPLAYBACK = 1,
            /// <summary>
            /// Installs a hook procedure that monitors keystroke messages. For more information, see the KeyboardProc
            /// hook procedure.
            /// </summary>
            WH_KEYBOARD = 2,
            /// <summary>
            /// Installs a hook procedure that monitors messages posted to a message queue. For more information, see the
            /// GetMsgProc hook procedure.
            /// </summary>
            WH_GETMESSAGE = 3,
            /// <summary>
            /// Installs a hook procedure that monitors messages before the system sends them to the destination window
            /// procedure. For more information, see the CallWndProc hook procedure.
            /// </summary>
            WH_CALLWNDPROC = 4,
            /// <summary>
            /// Installs a hook procedure that receives notifications useful to a CBT application. For more information,
            /// see the CBTProc hook procedure.
            /// </summary>
            WH_CBT = 5,
            /// <summary>
            /// Installs a hook procedure that monitors messages generated as a result of an input event in a dialog box,
            /// message box, menu, or scroll bar. The hook procedure monitors these messages for all applications in the
            /// same desktop as the calling thread. For more information, see the SysMsgProc hook procedure.
            /// </summary>
            WH_SYSMSGFILTER = 6,
            /// <summary>
            /// Installs a hook procedure that monitors mouse messages. For more information, see the MouseProc hook
            /// procedure.
            /// </summary>
            WH_MOUSE = 7,
            /// <summary>
            ///
            /// </summary>
            WH_HARDWARE = 8,
            /// <summary>
            /// Installs a hook procedure useful for debugging other hook procedures. For more information, see the
            /// DebugProc hook procedure.
            /// </summary>
            WH_DEBUG = 9,
            /// <summary>
            /// Installs a hook procedure that receives notifications useful to shell applications. For more information,
            /// see the ShellProc hook procedure.
            /// </summary>
            WH_SHELL = 10,
            /// <summary>
            /// Installs a hook procedure that will be called when the application's foreground thread is about to become
            /// idle. This hook is useful for performing low priority tasks during idle time. For more information, see the
            /// ForegroundIdleProc hook procedure.
            /// </summary>
            WH_FOREGROUNDIDLE = 11,
            /// <summary>
            /// Installs a hook procedure that monitors messages after they have been processed by the destination window
            /// procedure. For more information, see the CallWndRetProc hook procedure.
            /// </summary>
            WH_CALLWNDPROCRET = 12,
            /// <summary>
            /// Installs a hook procedure that monitors low-level keyboard input events. For more information, see the
            /// LowLevelKeyboardProc hook procedure.
            /// </summary>
            WH_KEYBOARD_LL = 13,
            /// <summary>
            /// Installs a hook procedure that monitors low-level mouse input events. For more information, see the
            /// LowLevelMouseProc hook procedure.
            /// </summary>
            WH_MOUSE_LL = 14
        }

        delegate int HookProc(int code, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr SetWindowsHookEx(HookType hookType, HookProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll")]
        static extern int CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        #endregion
    }
}
