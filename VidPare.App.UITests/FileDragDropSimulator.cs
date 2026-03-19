using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Windows.Forms;

namespace VidPare.App.UITests;

/// <summary>
/// Simulates a shell file drag-and-drop into a window by performing a real OLE
/// DoDragDrop operation. WinUI 3 registers an IDropTarget for its HWND when
/// AllowDrop=True, so OLE calls it exactly as Explorer would when dragging a file.
/// </summary>
internal static class FileDragDropSimulator
{
    private const int S_OK = 0;
    private const int DRAGDROP_S_DROP = 0x00040100;
    private const int DRAGDROP_S_CANCEL = 0x00040101;
    private const int DRAGDROP_S_USEDEFAULTCURSORS = unchecked((int)0x80040404);
    private const uint DROPEFFECT_COPY = 1;
    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;

    [DllImport("ole32.dll")]
    private static extern int OleInitialize(IntPtr pvReserved);

    [DllImport("ole32.dll")]
    private static extern void OleUninitialize();

    [DllImport("ole32.dll", PreserveSig = true)]
    private static extern int DoDragDrop(IDataObject pDataObj, IDropSource pDropSource, uint dwOKEffects, out uint pdwEffect);

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint cButtons, UIntPtr dwExtraInfo);

    [ComImport, Guid("00000121-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDropSource
    {
        [PreserveSig] int QueryContinueDrag([MarshalAs(UnmanagedType.Bool)] bool fEscapePressed, uint grfKeyState);
        [PreserveSig] int GiveFeedback(uint dwEffect);
    }

    [ClassInterface(ClassInterfaceType.None)]
    private class SimpleDropSource : IDropSource
    {
        private volatile bool _drop;

        /// <summary>Signals the drag loop to end with a drop after <paramref name="delayMs"/> ms.</summary>
        public void ScheduleDrop(int delayMs) =>
            Task.Delay(delayMs).ContinueWith(_ => _drop = true);

        public int QueryContinueDrag(bool fEscapePressed, uint grfKeyState)
        {
            if (fEscapePressed) return DRAGDROP_S_CANCEL;
            if (_drop) return DRAGDROP_S_DROP;
            return S_OK;
        }

        public int GiveFeedback(uint dwEffect) => DRAGDROP_S_USEDEFAULTCURSORS;
    }

    /// <summary>
    /// Simulates dropping <paramref name="filePath"/> onto the screen point
    /// <paramref name="target"/>. Runs on a dedicated STA thread as required by OLE.
    /// </summary>
    public static void Drop(string filePath, System.Drawing.Point target, int holdMs = 400)
    {
        Exception? error = null;

        var thread = new Thread(() =>
        {
            try
            {
                OleInitialize(IntPtr.Zero);

                // Position cursor and simulate button-down before starting the drag
                SetCursorPos(target.X, target.Y);
                mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
                Thread.Sleep(50);

                // Build CF_HDROP data object using WinForms (handles DROPFILES structure)
                var dataObject = new DataObject();
                dataObject.SetData(DataFormats.FileDrop, new string[] { filePath });

                var dropSource = new SimpleDropSource();
                dropSource.ScheduleDrop(holdMs);  // release after holdMs

                DoDragDrop((IDataObject)dataObject, dropSource, DROPEFFECT_COPY, out _);

                mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
                OleUninitialize();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(TimeSpan.FromSeconds(15));

        if (error != null)
            throw new InvalidOperationException("Drag-drop simulation failed.", error);
    }
}
