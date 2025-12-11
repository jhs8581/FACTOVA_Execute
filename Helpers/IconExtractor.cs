using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FACTOVA_Execute.Helpers
{
    /// <summary>
    /// 실행 파일에서 아이콘을 추출하는 헬퍼 클래스
    /// </summary>
    public static class IconExtractor
    {
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr ExtractIcon(IntPtr hInst, string lpszExeFileName, int nIconIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        /// <summary>
        /// 실행 파일에서 아이콘을 추출하여 ImageSource로 반환
        /// </summary>
        /// <param name="exePath">실행 파일 경로</param>
        /// <returns>추출된 아이콘의 ImageSource, 실패 시 null</returns>
        public static ImageSource? ExtractIconFromFile(string exePath)
        {
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                return null;

            IntPtr hIcon = IntPtr.Zero;

            try
            {
                // 실행 파일에서 첫 번째 아이콘 추출
                hIcon = ExtractIcon(IntPtr.Zero, exePath, 0);

                if (hIcon == IntPtr.Zero || hIcon == new IntPtr(1)) // 1은 아이콘이 없음을 의미
                    return null;

                // Icon 객체로 변환
                using (Icon icon = Icon.FromHandle(hIcon))
                {
                    // Bitmap으로 변환
                    using (Bitmap bitmap = icon.ToBitmap())
                    {
                        // WPF ImageSource로 변환
                        return BitmapToImageSource(bitmap);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"아이콘 추출 실패 ({exePath}): {ex.Message}");
                return null;
            }
            finally
            {
                // 아이콘 핸들 해제
                if (hIcon != IntPtr.Zero && hIcon != new IntPtr(1))
                {
                    DestroyIcon(hIcon);
                }
            }
        }

        /// <summary>
        /// Bitmap을 WPF ImageSource로 변환
        /// </summary>
        private static ImageSource? BitmapToImageSource(Bitmap bitmap)
        {
            try
            {
                IntPtr hBitmap = bitmap.GetHbitmap();
                try
                {
                    var imageSource = Imaging.CreateBitmapSourceFromHBitmap(
                        hBitmap,
                        IntPtr.Zero,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());

                    // 메모리 누수 방지를 위해 freeze
                    imageSource.Freeze();
                    return imageSource;
                }
                finally
                {
                    // GDI 객체 해제
                    DeleteObject(hBitmap);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ImageSource 변환 실패: {ex.Message}");
                return null;
            }
        }

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);
    }
}
