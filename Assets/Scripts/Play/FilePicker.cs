using System;
using System.Runtime.InteropServices;

namespace Executor.Play
{
    /// <summary>
    /// Windows "열기" 대화 상자로 그림 파일을 고른다 (comdlg32 GetOpenFileName). Unity에는 실행 중에 쓸 파일 고르기 창이 없어서 직접 부른다.
    /// Windows가 아니면 null (고를 수 없음).
    /// </summary>
    public static class FilePicker
    {
        public static bool Supported =>
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            true;
#else
            false;
#endif

        /// <summary>그림 파일(PNG·JPG)을 고른다. 취소하면 null</summary>
        public static string PickImage(string title = "프로필 사진 고르기")
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            var ofn = new OpenFileName();
            ofn.structSize = Marshal.SizeOf(ofn);
            ofn.filter = "그림 파일 (*.png;*.jpg;*.jpeg)\0*.png;*.jpg;*.jpeg\0모든 파일\0*.*\0\0";
            ofn.file = new string(new char[1024]);
            ofn.maxFile = ofn.file.Length;
            ofn.fileTitle = new string(new char[256]);
            ofn.maxFileTitle = ofn.fileTitle.Length;
            ofn.title = title;
            ofn.flags = OfnExplorer | OfnFileMustExist | OfnPathMustExist | OfnNoChangeDir;
            ofn.dlgOwner = GetActiveWindow();
            return GetOpenFileName(ofn) ? ofn.file.TrimEnd('\0') : null;
#else
            return null;
#endif
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private const int OfnExplorer = 0x00080000, OfnFileMustExist = 0x00001000, OfnPathMustExist = 0x00000800, OfnNoChangeDir = 0x00000008;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private class OpenFileName
        {
            public int structSize;
            public IntPtr dlgOwner = IntPtr.Zero;
            public IntPtr instance = IntPtr.Zero;
            public string filter;
            public string customFilter;
            public int maxCustFilter;
            public int filterIndex;
            public string file;
            public int maxFile;
            public string fileTitle;
            public int maxFileTitle;
            public string initialDir;
            public string title;
            public int flags;
            public short fileOffset;
            public short fileExtension;
            public string defExt;
            public IntPtr custData = IntPtr.Zero;
            public IntPtr hook = IntPtr.Zero;
            public string templateName;
            public IntPtr reservedPtr = IntPtr.Zero;
            public int reservedInt;
            public int flagsEx;
        }

        [DllImport("comdlg32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool GetOpenFileName([In, Out] OpenFileName ofn);

        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();
#endif
    }
}
