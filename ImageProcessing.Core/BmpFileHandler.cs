using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media.Imaging;

namespace ImageProcessing.Core
{
    public class BmpFileHandler
    {
        public Bitmap OriginalBitmap { get; private set; } // 원본이미지
        public Bitmap TemplateBitmap { get; private set; } // 템플릿이미지

        // 이미지 열기
        public BitmapSource Open(string filePath)
        {
            if (OriginalBitmap != null) // 새 이미지 열 때 기존이미지 해제
                OriginalBitmap.Dispose();

            OriginalBitmap = new Bitmap(filePath); // 파일 경로에서 가져와 새로운 Bitmap 생성
            return ToBitmapSource(OriginalBitmap); // WPF용 이미지로 변환
        }

        // 이미지 저장
        public void Save(Bitmap bitmap, string filePath) 
        {
            bitmap.Save(filePath, ImageFormat.Bmp); // 지정된 위치에 BMP로 저장
        }

        // ROI를 템플릿으로 등록
        public void RegisterTemplate(Bitmap sourceBitmap, Rectangle roi)
        {
            if (TemplateBitmap != null) // 기존 템플릿 이미지 해제
                TemplateBitmap.Dispose();

            TemplateBitmap = sourceBitmap.Clone(roi, sourceBitmap.PixelFormat); // ROI 영역 잘라 새 Bitmap 생성
        }

        // Bitmap -> BitmapSource
        public BitmapSource ToBitmapSource(Bitmap bitmap)
        {
            using (MemoryStream stream = new MemoryStream()) // 데이터 저장 공간(MermoryStream)
            {
                bitmap.Save(stream, ImageFormat.Bmp); // Bitmap을 MermoryStream에 저장
                stream.Position = 0; // 위치 초기화

                BitmapImage image = new BitmapImage(); // 표시용 BitmapImage 생성
                image.BeginInit(); // 초기 설정
                image.CacheOption = BitmapCacheOption.OnLoad; // 실행 후 자동으로 Dispose
                image.StreamSource = stream; // 이미지 데이터의 출처로 지정
                image.EndInit();
                image.Freeze(); // 읽기 전용 상태
                return image; // BitmapSource로 반환
            }
        }
    }
}
