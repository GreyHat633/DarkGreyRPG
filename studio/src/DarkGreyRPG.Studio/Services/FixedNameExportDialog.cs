using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;

namespace DarkGreyRPG.Studio.Services;

/// <summary>Native file browser with a fixed output name; only the current directory is returned.</summary>
[ComVisible(true), ClassInterface(ClassInterfaceType.None)]
internal sealed class FixedNameExportDialog(string fileName) : IExportFileDialogEvents
{
    private const int Cancelled = unchecked((int)0x800704C7);
    private const uint FileSystemPath = 0x80058000;
    private readonly string _fileName = fileName;
    private bool _showing;
    private bool _nameLocked;
    private bool _updatingName;
    private IntPtr _nameWindow;
    private SubclassWindow? _nameGuard;
    private string? _selectedDirectory;

    internal static string? SelectDirectory(Window? owner, string fileName, string initialDirectory, string title)
    {
        var dialog = (IExportFileDialog)Activator.CreateInstance(Type.GetTypeFromCLSID(
            new Guid("C0B4E2F3-BA21-4773-8DBA-335EC946EB8B"), throwOnError: true)!)!;
        uint cookie = 0;
        var events = new FixedNameExportDialog(fileName);
        try
        {
            // Force a real filesystem location, show every file, and avoid probe writes and recent-file entries.
            dialog.SetOptions(0x40 | 0x800 | 0x10000 | 0x02000000);
            dialog.SetFileTypes(1, [new("所有文件", "*.*")]);
            dialog.SetFileName(fileName);
            dialog.SetFileNameLabel("文件名（固定）:");
            dialog.SetTitle(title);
            dialog.SetOkButtonLabel("导出到此文件夹");
            var iid = typeof(IExportShellItem).GUID;
            SHCreateItemFromParsingName(initialDirectory, IntPtr.Zero, ref iid, out var folder);
            try { dialog.SetFolder(folder); }
            finally { Marshal.ReleaseComObject(folder); }
            dialog.Advise(events, out cookie);
            events._showing = true;
            var result = dialog.Show(owner is null ? IntPtr.Zero : new WindowInteropHelper(owner).EnsureHandle());
            if (result == Cancelled) return null;
            Marshal.ThrowExceptionForHR(result);
            if (!events._nameLocked || events._selectedDirectory is null)
                throw new IOException("导出位置窗口未能锁定文件名，请重新打开窗口。");
            return events._selectedDirectory;
        }
        finally
        {
            events._showing = false;
            events.RemoveNameGuard();
            if (cookie != 0) dialog.Unadvise(cookie);
            // Event arguments can reuse this RCW; release the private dialog completely.
            Marshal.FinalReleaseComObject(dialog);
            GC.KeepAlive(events);
        }
    }

    public int OnFileOk(IExportFileDialog dialog)
    {
        try
        {
            LockName(dialog);
            if (!_nameLocked) return 1;
            dialog.GetFolder(out var folder);
            try
            {
                folder.GetDisplayName(FileSystemPath, out var path);
                var directory = Path.GetFullPath(path);
                if (!Directory.Exists(directory)) return 1;
                _selectedDirectory = directory;
            }
            finally { Marshal.ReleaseComObject(folder); }
            return 0;
        }
        catch (Exception error) { return Marshal.GetHRForException(error); }
    }

    public int OnFolderChanging(IExportFileDialog dialog, IExportShellItem folder) => 0;
    public int OnFolderChange(IExportFileDialog dialog) => UpdateName(dialog);
    public int OnSelectionChange(IExportFileDialog dialog) => UpdateName(dialog);
    public int OnTypeChange(IExportFileDialog dialog) => UpdateName(dialog);
    public int OnShareViolation(IExportFileDialog dialog, IExportShellItem item, out uint response)
    { response = 0; return 0; }
    public int OnOverwrite(IExportFileDialog dialog, IExportShellItem item, out uint response)
    { response = 0; return 0; }

    private int UpdateName(IExportFileDialog dialog)
    {
        try { if (_showing && !_updatingName) LockName(dialog); return 0; }
        catch (Exception error) { return Marshal.GetHRForException(error); }
    }

    private void LockName(IExportFileDialog dialog)
    {
        _updatingName = true;
        try
        {
            dialog.SetFileName(_fileName);
            ((IExportOleWindow)dialog).GetWindow(out var window);
            if (window == IntPtr.Zero) return;
            EnumChildWindows(window, (child, _) =>
            {
                var kind = new StringBuilder(64);
                GetClassName(child, kind, kind.Capacity);
                if (!kind.ToString().Equals("Edit", StringComparison.OrdinalIgnoreCase)) return true;
                var text = new StringBuilder(_fileName.Length + 1);
                GetWindowText(child, text, text.Capacity);
                if (!text.ToString().Equals(_fileName, StringComparison.Ordinal)) return true;
                // Read-only rejects typing/pasting; the guard also prevents the Shell from
                // replacing the field after its selection callback has returned.
                if (_nameWindow != child)
                {
                    RemoveNameGuard();
                    _nameGuard ??= GuardName;
                    if (!SetWindowSubclass(child, _nameGuard, 1, 0)) return true;
                    _nameWindow = child;
                }
                _nameLocked = SendMessage(child, 0x00CF, new IntPtr(1), IntPtr.Zero) != IntPtr.Zero;
                return !_nameLocked;
            }, IntPtr.Zero);
        }
        finally { _updatingName = false; }
    }

    private IntPtr GuardName(IntPtr window, uint message, IntPtr wParam, IntPtr lParam, nuint id, nuint data)
    {
        if (message == 0x000C && !string.Equals(Marshal.PtrToStringUni(lParam), _fileName, StringComparison.Ordinal))
            return new IntPtr(1); // WM_SETTEXT: ignore names coming from the selected file.
        if (message == 0x0082) RemoveNameGuard(); // WM_NCDESTROY
        return DefSubclassProc(window, message, wParam, lParam);
    }

    private void RemoveNameGuard()
    {
        if (_nameWindow != IntPtr.Zero && _nameGuard is not null)
            RemoveWindowSubclass(_nameWindow, _nameGuard, 1);
        _nameWindow = IntPtr.Zero;
    }

    private delegate bool EnumWindow(IntPtr window, IntPtr parameter);
    private delegate IntPtr SubclassWindow(IntPtr window, uint message, IntPtr wParam, IntPtr lParam, nuint id, nuint data);
    [DllImport("comctl32.dll")] private static extern bool SetWindowSubclass(IntPtr window, SubclassWindow callback, nuint id, nuint data);
    [DllImport("comctl32.dll")] private static extern bool RemoveWindowSubclass(IntPtr window, SubclassWindow callback, nuint id);
    [DllImport("comctl32.dll")] private static extern IntPtr DefSubclassProc(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool EnumChildWindows(IntPtr window, EnumWindow callback, IntPtr parameter);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder text, int length);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr window, StringBuilder text, int length);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHCreateItemFromParsingName(string path, IntPtr context, ref Guid iid, out IExportShellItem item);
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal readonly struct ExportFilter(string name, string pattern)
{
    [MarshalAs(UnmanagedType.LPWStr)] public readonly string Name = name;
    [MarshalAs(UnmanagedType.LPWStr)] public readonly string Pattern = pattern;
}

// IFileDialog's complete native vtable, shared by the standard Save dialog.
[ComImport, Guid("42F85136-DB7E-439C-85F1-E4075D135FC8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IExportFileDialog
{
    [PreserveSig] int Show(IntPtr owner);
    void SetFileTypes(uint count, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] ExportFilter[] filters);
    void SetFileTypeIndex(uint index);
    void GetFileTypeIndex(out uint index);
    void Advise(IExportFileDialogEvents events, out uint cookie);
    void Unadvise(uint cookie);
    void SetOptions(uint options);
    void GetOptions(out uint options);
    void SetDefaultFolder(IExportShellItem folder);
    void SetFolder(IExportShellItem folder);
    void GetFolder(out IExportShellItem folder);
    void GetCurrentSelection(out IExportShellItem item);
    void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);
    void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string name);
    void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);
    void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string label);
    void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string label);
    void GetResult(out IExportShellItem item);
    void AddPlace(IExportShellItem item, uint alignment);
    void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string extension);
    void Close(int result);
    void SetClientGuid(ref Guid guid);
    void ClearClientData();
    void SetFilter(IntPtr filter);
}

[ComVisible(true), Guid("973510DB-7D7F-452B-8975-74A85828D354"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IExportFileDialogEvents
{
    [PreserveSig] int OnFileOk(IExportFileDialog dialog);
    [PreserveSig] int OnFolderChanging(IExportFileDialog dialog, IExportShellItem folder);
    [PreserveSig] int OnFolderChange(IExportFileDialog dialog);
    [PreserveSig] int OnSelectionChange(IExportFileDialog dialog);
    [PreserveSig] int OnShareViolation(IExportFileDialog dialog, IExportShellItem item, out uint response);
    [PreserveSig] int OnTypeChange(IExportFileDialog dialog);
    [PreserveSig] int OnOverwrite(IExportFileDialog dialog, IExportShellItem item, out uint response);
}

[ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IExportShellItem
{
    void BindToHandler(IntPtr context, ref Guid handler, ref Guid iid, out IntPtr result);
    void GetParent(out IExportShellItem parent);
    void GetDisplayName(uint nameType, [MarshalAs(UnmanagedType.LPWStr)] out string name);
    void GetAttributes(uint mask, out uint attributes);
    void Compare(IExportShellItem other, uint hint, out int order);
}

[ComImport, Guid("00000114-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IExportOleWindow
{
    void GetWindow(out IntPtr window);
    void ContextSensitiveHelp([MarshalAs(UnmanagedType.Bool)] bool enabled);
}
