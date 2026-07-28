using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace Zip711.Services;

// Native "virtual file" drag source, the way the real 7-Zip drags items out of an
// archive: instead of extracting up front (which stalls the drag for large files and
// makes Explorer show the no-drop cursor), we advertise CFSTR_FILEDESCRIPTORW +
// CFSTR_FILECONTENTS and only extract each entry when the drop target actually reads
// it. The drag therefore starts instantly and the bytes are produced on drop.
public sealed class ShellDrag
{
    // One file being dragged. Extract() must return the path of a real, fully-written
    // temp file holding the entry's bytes; it is invoked lazily, on drop.
    public sealed class Entry
    {
        public required string RelativeName;   // name shown at the drop target (may contain \ for folders)
        public required long Size;
        public required Func<string> Extract;  // extracts to temp, returns the temp file path
    }

    public static void DoDrag(IReadOnlyList<Entry> entries)
    {
        if (entries.Count == 0) return;
        TryOleInitialize();
        var data = new VirtualFileDataObject(entries);
        var src = new DropSource();
        // Blocks (runs the OLE modal drag loop, pumping messages) until the drop
        // completes or is cancelled. Explorer shows its own copy progress.
        _ = DoDragDrop(data, src, DROPEFFECT_COPY, out _);
    }

    // ---- OLE drag-drop -----------------------------------------------------------

    const int DROPEFFECT_COPY = 1;

    [DllImport("ole32.dll")]
    static extern int DoDragDrop(IDataObject pDataObj, IDropSource pDropSource, int dwOKEffects, out int pdwEffect);

    [DllImport("ole32.dll")]
    static extern int OleInitialize(IntPtr pvReserved);

    static void TryOleInitialize()
    {
        try { OleInitialize(IntPtr.Zero); } catch { /* already initialised for this thread */ }
    }

    [ComImport, Guid("00000121-0000-0000-C000-000000000046"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IDropSource
    {
        [PreserveSig] int QueryContinueDrag(int fEscapePressed, uint grfKeyState);
        [PreserveSig] int GiveFeedback(uint dwEffect);
    }

    sealed class DropSource : IDropSource
    {
        const int S_OK = 0;
        const int DRAGDROP_S_DROP = 0x00040100;
        const int DRAGDROP_S_CANCEL = 0x00040101;
        const int DRAGDROP_S_USEDEFAULTCURSORS = 0x00040102;
        const uint MK_LBUTTON = 0x0001;

        public int QueryContinueDrag(int fEscapePressed, uint grfKeyState)
        {
            if (fEscapePressed != 0) return DRAGDROP_S_CANCEL;
            if ((grfKeyState & MK_LBUTTON) == 0) return DRAGDROP_S_DROP; // button released -> drop
            return S_OK;
        }

        public int GiveFeedback(uint dwEffect) => DRAGDROP_S_USEDEFAULTCURSORS;
    }

    // ---- IDataObject with virtual files -----------------------------------------

    sealed class VirtualFileDataObject : IDataObject
    {
        const int S_OK = 0;
        const int DV_E_FORMATETC = unchecked((int)0x80040064);
        const int DV_E_TYMED = unchecked((int)0x80040069);
        const int OLE_E_ADVISENOTSUPPORTED = unchecked((int)0x80040003);

        static readonly short CF_FILEDESCRIPTORW = (short)RegisterClipboardFormat("FileGroupDescriptorW");
        static readonly short CF_FILECONTENTS = (short)RegisterClipboardFormat("FileContents");

        readonly IReadOnlyList<Entry> _entries;
        readonly string?[] _extracted;   // cached temp paths per index

        public VirtualFileDataObject(IReadOnlyList<Entry> entries)
        {
            _entries = entries;
            _extracted = new string?[entries.Count];
        }

        public int QueryGetData(ref FORMATETC format)
        {
            if (format.cfFormat == CF_FILEDESCRIPTORW && (format.tymed & TYMED.TYMED_HGLOBAL) != 0) return S_OK;
            if (format.cfFormat == CF_FILECONTENTS && (format.tymed & TYMED.TYMED_ISTREAM) != 0) return S_OK;
            return DV_E_FORMATETC;
        }

        public void GetData(ref FORMATETC format, out STGMEDIUM medium)
        {
            medium = default;
            if (format.cfFormat == CF_FILEDESCRIPTORW && (format.tymed & TYMED.TYMED_HGLOBAL) != 0)
            {
                medium.tymed = TYMED.TYMED_HGLOBAL;
                medium.unionmember = BuildDescriptor();
                return;
            }
            if (format.cfFormat == CF_FILECONTENTS && (format.tymed & TYMED.TYMED_ISTREAM) != 0)
            {
                medium.tymed = TYMED.TYMED_ISTREAM;
                medium.unionmember = BuildContents(format.lindex);
                return;
            }
            Marshal.ThrowExceptionForHR(DV_E_FORMATETC);
        }

        IntPtr BuildDescriptor()
        {
            int fdSize = Marshal.SizeOf<FILEDESCRIPTORW>();
            int total = sizeof(uint) + _entries.Count * fdSize;
            IntPtr h = GlobalAlloc(GMEM_MOVEABLE | GMEM_ZEROINIT, (UIntPtr)total);
            IntPtr p = GlobalLock(h);
            try
            {
                Marshal.WriteInt32(p, _entries.Count);
                IntPtr cur = p + sizeof(uint);
                for (int i = 0; i < _entries.Count; i++)
                {
                    var e = _entries[i];
                    var fd = new FILEDESCRIPTORW
                    {
                        dwFlags = FD_FILESIZE | FD_PROGRESSUI | FD_ATTRIBUTES,
                        dwFileAttributes = FILE_ATTRIBUTE_NORMAL,
                        nFileSizeHigh = (uint)(e.Size >> 32),
                        nFileSizeLow = (uint)(e.Size & 0xFFFFFFFF),
                        cFileName = e.RelativeName,
                    };
                    Marshal.StructureToPtr(fd, cur, false);
                    cur += fdSize;
                }
            }
            finally { GlobalUnlock(h); }
            return h;
        }

        IntPtr BuildContents(int index)
        {
            if (index < 0 || index >= _entries.Count) Marshal.ThrowExceptionForHR(DV_E_FORMATETC);
            string path = _extracted[index] ??= _entries[index].Extract();
            const uint STGM_READ = 0x0;
            const uint STGM_SHARE_DENY_WRITE = 0x20;
            int hr = SHCreateStreamOnFileEx(path, STGM_READ | STGM_SHARE_DENY_WRITE,
                FILE_ATTRIBUTE_NORMAL, false, IntPtr.Zero, out IStream stream);
            if (hr != S_OK || stream is null) Marshal.ThrowExceptionForHR(hr == S_OK ? DV_E_TYMED : hr);
            return Marshal.GetComInterfaceForObject(stream!, typeof(IStream));
        }

        public IEnumFORMATETC EnumFormatEtc(DATADIR direction)
        {
            if (direction != DATADIR.DATADIR_GET) throw new NotSupportedException();
            return new FormatEnumerator(new[]
            {
                new FORMATETC { cfFormat = CF_FILEDESCRIPTORW, ptd = IntPtr.Zero, dwAspect = DVASPECT.DVASPECT_CONTENT, lindex = -1, tymed = TYMED.TYMED_HGLOBAL },
                new FORMATETC { cfFormat = CF_FILECONTENTS,    ptd = IntPtr.Zero, dwAspect = DVASPECT.DVASPECT_CONTENT, lindex = -1, tymed = TYMED.TYMED_ISTREAM },
            });
        }

        // Explorer writes CFSTR_PERFORMEDDROPEFFECT / "Paste Succeeded" back; accept and drop it.
        public void SetData(ref FORMATETC formatIn, ref STGMEDIUM medium, bool release)
        {
            if (release) ReleaseStgMedium(ref medium);
        }

        public void GetDataHere(ref FORMATETC format, ref STGMEDIUM medium) => Marshal.ThrowExceptionForHR(DV_E_FORMATETC);
        public int GetCanonicalFormatEtc(ref FORMATETC formatIn, out FORMATETC formatOut) { formatOut = default; return DV_E_FORMATETC; }
        public int DAdvise(ref FORMATETC pFormatetc, ADVF advf, IAdviseSink adviseSink, out int connection) { connection = 0; return OLE_E_ADVISENOTSUPPORTED; }
        public void DUnadvise(int connection) => Marshal.ThrowExceptionForHR(OLE_E_ADVISENOTSUPPORTED);
        public int EnumDAdvise(out IEnumSTATDATA enumAdvise) { enumAdvise = null!; return OLE_E_ADVISENOTSUPPORTED; }
    }

    sealed class FormatEnumerator : IEnumFORMATETC
    {
        readonly FORMATETC[] _formats;
        int _index;

        public FormatEnumerator(FORMATETC[] formats, int index = 0) { _formats = formats; _index = index; }

        public int Next(int celt, FORMATETC[] rgelt, int[]? pceltFetched)
        {
            int fetched = 0;
            while (fetched < celt && _index < _formats.Length)
                rgelt[fetched++] = _formats[_index++];
            if (pceltFetched is not null && pceltFetched.Length > 0) pceltFetched[0] = fetched;
            return fetched == celt ? 0 : 1; // S_OK / S_FALSE
        }

        public int Skip(int celt) { _index += celt; return _index <= _formats.Length ? 0 : 1; }
        public int Reset() { _index = 0; return 0; }
        public void Clone(out IEnumFORMATETC newEnum) => newEnum = new FormatEnumerator(_formats, _index);
    }

    // ---- structs / P-Invoke ------------------------------------------------------

    const uint FD_FILESIZE = 0x00000040;
    const uint FD_PROGRESSUI = 0x00004000;
    const uint FD_ATTRIBUTES = 0x00000020;
    const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;
    const uint GMEM_MOVEABLE = 0x0002;
    const uint GMEM_ZEROINIT = 0x0040;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct FILEDESCRIPTORW
    {
        public uint dwFlags;
        public Guid clsid;
        public int sizelCx;
        public int sizelCy;
        public int pointlX;
        public int pointlY;
        public uint dwFileAttributes;
        public long ftCreationTime;
        public long ftLastAccessTime;
        public long ftLastWriteTime;
        public uint nFileSizeHigh;
        public uint nFileSizeLow;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string cFileName;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern uint RegisterClipboardFormat(string lpszFormat);

    [DllImport("kernel32.dll")] static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);
    [DllImport("kernel32.dll")] static extern IntPtr GlobalLock(IntPtr hMem);
    [DllImport("kernel32.dll")] static extern bool GlobalUnlock(IntPtr hMem);

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    static extern int SHCreateStreamOnFileEx(string pszFile, uint grfMode, uint dwAttributes,
        bool fCreate, IntPtr pstmTemplate, out IStream ppstm);

    [DllImport("ole32.dll")] static extern void ReleaseStgMedium(ref STGMEDIUM pmedium);
}
