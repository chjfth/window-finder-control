using System;
using System.Reflection;
using System.Collections;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Diagnostics;
using System.Windows.Forms;

namespace WindowFinder
{
    [DefaultEvent("WindowHandleChanged")]
    [ToolboxBitmap(typeof(ResourceFinder), "WindowFinder.Resources.WindowFinder.bmp")]
    [Designer(typeof(WindowFinderDesigner))]
    public sealed partial class WindowFinder : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WindowFinder"/> class.
        /// </summary>
        public WindowFinder()
        {
            // This call is required by the Windows.Forms Form Designer.
            InitializeComponent();

            SetStyle(ControlStyles.FixedWidth, true);
            SetStyle(ControlStyles.FixedHeight, true);
            SetStyle(ControlStyles.StandardClick, false);
            SetStyle(ControlStyles.StandardDoubleClick, false);
            SetStyle(ControlStyles.Selectable, false);

            picTarget.Size = new Size(31, 28);
            Size = picTarget.Size;

            _timerCheckKey = new Timer();
            _timerCheckKey.Interval = 100;
            _timerCheckKey.Tick += new EventHandler(TimerCheckKey);
        }

        void TimerCheckKey(object obj, EventArgs ea)
        {
            bool isCtrlKeyDown = Control.ModifierKeys == Keys.Control;

            if (isCtrlKeyDown != _wasCtrlKeyDown)
            {
                _wasCtrlKeyDown = isCtrlKeyDown;
                RetargetMyHwnd();
            }
        }

        #region Public Properties

        /// <summary>
        /// Called when the WindowHandle property is changed.
        /// </summary>
        public event EventHandler WindowHandleChanged;

        /// <summary>
        /// Handle of the window found.
        /// </summary>
        [Browsable(false)]
        public IntPtr WindowHandle
        {
            get
            {
                return windowHandle;
            }
        }

        /// <summary>
        /// Handle text of the window found.
        /// </summary>
        [Browsable(false)]
        public string WindowHandleText
        {
            get
            {
                return windowHandleText;
            }
        }

        /// <summary>
        /// Class of the window found.
        /// </summary>
        [Browsable(false)]
        public string WindowClass
        {
            get
            {
                return windowClass;
            }
        }

        /// <summary>
        /// Text of the window found.
        /// </summary>
        [Browsable(false)]
        public string WindowText
        {
            get
            {
                return windowText;
            }
        }

        /// <summary>
        /// Whether or not the found window is unicode.
        /// </summary>
        [Browsable(false)]
        public bool IsWindowUnicode
        {
            get
            {
                return isWindowUnicode;
            }
        }

        /// <summary>
        /// Whether or not the found window is unicode, via text.
        /// </summary>
        [Browsable(false)]
        public string WindowCharset
        {
            get
            {
                return windowCharset;
            }
        }

        /// <summary>
        /// If true, only top-level window can be highlighted.
        /// </summary>
        [Browsable(true)]
        public bool isFindOnlyTopLevel { get; set; } = false;

        #endregion

        #region Event Handler Methods

        /// <summary>
        /// Handles the Load event of the WindowFinder control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        private void WindowFinder_Load(object sender, System.EventArgs e)
        {
            this.Size = picTarget.Size;
            try
            {
                // Load cursors
                Assembly assembly = Assembly.GetExecutingAssembly();
                cursorTarget = new Cursor(assembly.GetManifestResourceStream("WindowFinder.curTarget.cur"));
                bitmapFind = new Bitmap(assembly.GetManifestResourceStream("WindowFinder.bmpFind.bmp"));
                bitmapFinda = new Bitmap(assembly.GetManifestResourceStream("WindowFinder.bmpFinda.bmp"));
            }
            catch(Exception x)
            {
                // Show error
                MessageBox.Show(this, "Failed to load resources.\n\n" + x.ToString(), "WindowFinder");

                // Attempt to use backup cursor
                if(cursorTarget == null)
                    cursorTarget = Cursors.Cross;
            }

            // Set default values
            picTarget.Image = bitmapFind;
        }

        /// <summary>
        /// Handles the MouseDown event of the picTarget control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.Forms.MouseEventArgs"/> instance containing the event data.</param>
        private void picTarget_MouseDown(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            // Set capture image and cursor
            picTarget.Image = bitmapFinda;
            picTarget.Cursor = cursorTarget;

            // Set capture
            Win32.SetCapture(picTarget.Handle);

            // Begin targeting
            isTargeting = true;
            targetWindow = IntPtr.Zero;

            // Show info
            SetWindowHandle(picTarget.Handle);

            _wasCtrlKeyDown = false;
            _timerCheckKey.Start();
        }

        private Timer _timerCheckKey = null;
        private bool _wasCtrlKeyDown = false;
        private int _mousex = -1;
        private int _mousey = -1;

        /// <summary>
        /// Handles the MouseMove event of the picTarget control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.Forms.MouseEventArgs"/> instance containing the event data.</param>
        private void picTarget_MouseMove(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            // Make sure targeting before highlighting windows
            if(!isTargeting)
                return;

            _mousex = e.X;
            _mousey = e.Y;

            RetargetMyHwnd();
        }

        /// <summary>
        /// Handles the MouseUp event of the picTarget control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.Forms.MouseEventArgs"/> instance containing the event data.</param>
        private void picTarget_MouseUp(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            // End targeting
            isTargeting = false;

            // Unhighlight window
            if (targetWindow != IntPtr.Zero)
            {
                Win32.HighlightWindow(targetWindow);

                targetWindow = IntPtr.Zero;
            }

            // Reset capture image and cursor
            picTarget.Cursor = Cursors.Default;
            picTarget.Image = bitmapFind;

            // Release capture
            Win32.SetCapture(IntPtr.Zero);

            _timerCheckKey.Stop();
        }

        private void RetargetMyHwnd()
        {
            System.Drawing.Point pt = new Point(_mousex, _mousey);

            Win32.ClientToScreen(picTarget.Handle, ref pt);

            // Get screen coords from client coords and window handle
            IntPtr hChild1 = Win32.WindowFromPoint(IntPtr.Zero, pt.X, pt.Y);
            // -- We name it "child" bcz it must be a child-or-grand-child of the Desktop window.

            // Get real window
            if (hChild1 != IntPtr.Zero)
            {
                // MSDN undoc: 
                // Case 1: Under normal situation, WindowFromPoint() gives us most visible child-window HWND.
                // Case 2: If top-level window X brings up a modal dialog(About box etc), then a child window
                //         of X will not be reported by WindowFromPoint() but X is reported instead.
                // To cope with Case 2, we have to call ChildWindowFromPointEx() recursively until we find
                // the most visible window.

                IntPtr hParent = IntPtr.Zero;

                while (true)
                {
                    Win32.MapWindowPoints(hParent, hChild1, ref pt, 1);

                    IntPtr hChild2 = (IntPtr)Win32.ChildWindowFromPointEx(hChild1, pt,
                        Win32.ChildWindowFromPointFlags.CWP_SKIPINVISIBLE);

                    if (hChild2 == IntPtr.Zero)
                        break;
                    if (hChild1 == hChild2)
                        break;

                    hParent = hChild1;
                    hChild1 = hChild2;
                }
            }

            if (isFindOnlyTopLevel || _wasCtrlKeyDown)
            {
                hChild1 = Win32.GetAncestor(hChild1, Win32.GetAncestorFlags.GetRoot);
            }

            // Show info
            SetWindowHandle(hChild1);

            // Highlight valid window
            HighlightValidWindow(hChild1, this.Handle);
        }

        private void picTarget_KeyDown(object sender, System.Windows.Forms.KeyEventArgs e)
        {
            // Chj: This cannot be triggered! Why?

            Debug.WriteLine($"picTarget_KeyDown({e.KeyCode})...");
            if (e.KeyCode == Keys.Control)
            {
                RetargetMyHwnd();
            }
        }

        private void picTarget_KeyUp(object sender, System.Windows.Forms.KeyEventArgs e)
        {
            Debug.WriteLine($"picTarget_KeyUp({e.KeyCode})...");
            if (e.KeyCode == Keys.Control)
            {
                RetargetMyHwnd();
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Sets the window handle if handle is a valid window.
        /// </summary>
        /// <param name="handle">The handle to set to.</param>
        public void SetWindowHandle(IntPtr handle)
        {
            if((Win32.IsWindow(handle) == 0) || (Win32.IsRelativeWindow(handle, this.Handle, true)))
            {
                // Clear window information
                windowHandle = IntPtr.Zero;
                windowHandleText = string.Empty;
                windowClass = string.Empty;
                windowText = string.Empty;
                isWindowUnicode = false;
                windowCharset = string.Empty;
            }
            else
            {
                // Set window information
                windowHandle = handle;
                windowHandleText = Convert.ToString(handle.ToInt32(), 16).ToUpper().PadLeft(8, '0');
                windowClass = Win32.GetClassName(handle);
                windowText = Win32.GetWindowText(handle);
                isWindowUnicode = Win32.IsWindowUnicode(handle) != 0;
                windowCharset = ((isWindowUnicode) ? ("Unicode") : ("Ansi"));
            }

            if(WindowHandleChanged != null)
                WindowHandleChanged(this, EventArgs.Empty);
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Highlights the specified window, but only if it is a valid window in relation to the specified owner window.
        /// </summary>
        private void HighlightValidWindow(IntPtr hWnd, IntPtr hOwner)
        {
            // Check for valid highlight
            if(targetWindow == hWnd)
                return;

            // Check for relative
            if(Win32.IsRelativeWindow(hWnd, hOwner, true))
            {
                // Unhighlight last window
                if(targetWindow != IntPtr.Zero)
                {
                    Win32.HighlightWindow(targetWindow);
                    targetWindow = IntPtr.Zero;
                }

                return;
            }

            // Unhighlight last window
            Win32.HighlightWindow(targetWindow);

            // Set as current target
            targetWindow = hWnd;

            // Highlight window
            Win32.HighlightWindow(hWnd);
        }

        #endregion

        private bool isTargeting = false;
        private Cursor cursorTarget = null;
        private Bitmap bitmapFind = null;
        private Bitmap bitmapFinda = null;
        private IntPtr targetWindow = IntPtr.Zero;
        private IntPtr windowHandle = IntPtr.Zero;
        private string windowHandleText = string.Empty;
        private string windowClass = string.Empty;
        private string windowText = string.Empty;
        private bool isWindowUnicode = false;
        private string windowCharset = string.Empty;
    }
}
