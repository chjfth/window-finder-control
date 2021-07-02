using System;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;
using System.Data;
using System.Diagnostics;
using WindowFinder;

namespace TestControl
{
    /// <summary>
    /// Represents the test form.
    /// </summary>
    public sealed partial class MainForm : Form
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MainForm"/> class.
        /// </summary>
        public MainForm()
        {
            InitializeComponent();

            this.radiobtnAimingFrame.Checked = true;

            ckbScreenshot.CheckFromCode(true);
        }

        #region Event Handler Methods

        /// <summary>
        /// Handles the Click event of the btnClose control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        private void btnClose_Click(object sender, System.EventArgs e)
        {
            this.Close();
        }

        /// <summary>
        /// Handles the WindowHandleChanged event of the windowFinder control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        private void windowFinder_WindowHandleChanged(object sender, System.EventArgs e)
        {
            if(!windowHandleChanging)
                txtWindowHandle.Text = windowFinder.WindowHandleText;
            txtWindowClass.Text = windowFinder.WindowClass;
            txtWindowText.Text = windowFinder.WindowText;
            txtWindowCharset.Text = windowFinder.WindowCharset;

            Rectangle rt = windowFinder.WindowRect;
            if (rt == Rectangle.Empty)
                txtWindowRect.Text = "";
            else
                txtWindowRect.Text = $"[{rt.Width} x {rt.Height}] LT({rt.Left}, {rt.Top}) RB({rt.Right}, {rt.Bottom})";
        }

        /// <summary>
        /// Handles the TextChanged event of the txtWindowHandle control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        private void txtWindowHandle_TextChanged(object sender, System.EventArgs e)
        {
            IntPtr handle;

            try
            {
                handle = (IntPtr)Convert.ToInt32(txtWindowHandle.Text, 16);
            }
            catch
            {
                handle = IntPtr.Zero;
            }

            windowHandleChanging = true;
            try
            {
                windowFinder.SetWindowHandle(handle);
            }
            finally
            {
                windowHandleChanging = false;
            }
        }
        
        #endregion

        private bool windowHandleChanging = false;

        private void radiobtnInvertColor_CheckedChanged(object sender, EventArgs e)
        {
            this.windowFinder.tgwHighlightMethod = radiobtnInvertColor.Checked
                ? WindowFinder.WindowFinder.HighlightMethod.InvertColor
                : WindowFinder.WindowFinder.HighlightMethod.AimingFrame;

            RefreshUIByCfg();
        }

        private void MainForm_Shown(object sender, EventArgs e)
        {
            DpiUtilities.PROCESS_DPI_AWARENESS procdaw = DpiUtilities.PROCESS_DPI_AWARENESS.PROCESS_DPI_Unset;

            try
            {
                // only Win81+
                IntPtr hCurrentProcess = Process.GetCurrentProcess().Handle;
                DpiUtilities.GetProcessDpiAwareness(hCurrentProcess, out procdaw); 
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
            }

            if(procdaw == DpiUtilities.PROCESS_DPI_AWARENESS.PROCESS_DPI_Unset)
            {
                // For Win7
                bool win7daw = WindowFinder.DpiUtilities.IsProcessDPIAware();
                if (win7daw)
                    procdaw = DpiUtilities.PROCESS_DPI_AWARENESS.PROCESS_SYSTEM_DPI_AWARE;
                else
                    procdaw = DpiUtilities.PROCESS_DPI_AWARENESS.PROCESS_DPI_UNAWARE;
            }

            string hint = "";
            if (procdaw == DpiUtilities.PROCESS_DPI_AWARENESS.PROCESS_DPI_UNAWARE)
                hint += "DPI unaware";
            else if (procdaw == DpiUtilities.PROCESS_DPI_AWARENESS.PROCESS_SYSTEM_DPI_AWARE)
                hint += "System-DPI aware";
            else if (procdaw == DpiUtilities.PROCESS_DPI_AWARENESS.PROCESS_PER_MONITOR_DPI_AWARE)
                hint += "Per-monitor-DPI aware";

            lblDpiAwareness.Text = hint;
        }

        private void btnHelp_Click(object sender, EventArgs e)
        {
            Program.ShowParameterHelp();
        }

        private void RefreshUIByCfg()
        {
            if (this.windowFinder.tgwHighlightMethod == WindowFinder.WindowFinder.HighlightMethod.AimingFrame)
            {
                this.radiobtnAimingFrame.Checked = true;

                ckbScreenshot.Enabled = true;

                if (this.windowFinder.isCaptureToClipboard)
                    ckbScreenshot.CheckFromCode(true);
                else
                    ckbScreenshot.CheckFromCode(false);
            }
            else
            {
                this.radiobtnInvertColor.Checked = true;

                ckbScreenshot.Enabled = false;

                ckbScreenshot.CheckFromCode(false);
            }
        }

        private void ckbClipboard_CheckedChanged(object sender, EventArgs e)
        {
            this.windowFinder.isCaptureToClipboard = !this.windowFinder.isCaptureToClipboard;

            Debug.WriteLine($"isCaptureToClipboard={this.windowFinder.isCaptureToClipboard}");

            RefreshUIByCfg();
        }

    }

    public class MyCheckBox : CheckBox
    {
        public MyCheckBox()
        {
            this.CheckedChanged += MyCheckedChanged;
        }

        private bool _isCheckFromCode = false;

        public event EventHandler CheckedChanged_ByHuman;

        public void CheckFromCode(bool isChecked)
        {
            _isCheckFromCode = true;

            this.Checked = isChecked;

            _isCheckFromCode = false;
        }

        public bool IsCheckingFromCode()
        {
            if (_isCheckFromCode)
                return true;
            else
                return false;
        }

        private void MyCheckedChanged(object sender, EventArgs e)
        {
            if (_isCheckFromCode)
                return;

            CheckedChanged_ByHuman(sender, e);
        }
    }
}