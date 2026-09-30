using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using Native;

namespace ImageProcessing.Core
{
    // ProcessingService (3/4) : 필터
    // 가우시안, 라플라시안, 소벨
    // 세 필터 모두 "source를 읽고 result에 쓰는" 같은 순서로 동작한다.
    //   1) 입력 영상 복사  2) 두 영상 LockBits  3) C++ 필터 호출(시간 측정)
    //   4) UnlockBits      5) 결과 저장
    public partial class ProcessingService
    {
        // 가우시안
        public void Gaussian(Bitmap originalBitmap, int kernelSize, double sigma)
        {
            Bitmap source = CopyProcessingSource(originalBitmap);
            Bitmap result = CopyTo24bpp(source);

            BitmapData srcData = Lock24(source, ImageLockMode.ReadOnly);
            BitmapData dstData = Lock24(result, ImageLockMode.ReadWrite);

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            FilterProcessor processor = new FilterProcessor();
            processor.Gaussian(srcData.Scan0, dstData.Scan0, srcData.Width, srcData.Height, srcData.Stride,
                kernelSize, sigma);

            stopwatch.Stop();

            source.UnlockBits(srcData);
            result.UnlockBits(dstData);
            source.Dispose();

            LastProcessingTimeMs = stopwatch.ElapsedMilliseconds;
            ReplaceResult(result);
        }

        // 라플라시안
        public void Laplacian(Bitmap originalBitmap)
        {
            Bitmap source = CopyProcessingSource(originalBitmap);
            Bitmap result = CopyTo24bpp(source);

            BitmapData srcData = Lock24(source, ImageLockMode.ReadOnly);
            BitmapData dstData = Lock24(result, ImageLockMode.ReadWrite);

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            FilterProcessor processor = new FilterProcessor();
            processor.Laplacian(srcData.Scan0, dstData.Scan0, srcData.Width, srcData.Height, srcData.Stride);

            stopwatch.Stop();

            source.UnlockBits(srcData);
            result.UnlockBits(dstData);
            source.Dispose();

            LastProcessingTimeMs = stopwatch.ElapsedMilliseconds;
            ReplaceResult(result);
        }

        // 소벨
        public void Sobel(Bitmap originalBitmap)
        {
            Bitmap source = CopyProcessingSource(originalBitmap);
            Bitmap result = CopyTo24bpp(source);

            BitmapData srcData = Lock24(source, ImageLockMode.ReadOnly);
            BitmapData dstData = Lock24(result, ImageLockMode.ReadWrite);

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            FilterProcessor processor = new FilterProcessor();
            processor.Sobel(srcData.Scan0, dstData.Scan0, srcData.Width, srcData.Height, srcData.Stride);

            stopwatch.Stop();

            source.UnlockBits(srcData);
            result.UnlockBits(dstData);
            source.Dispose();

            LastProcessingTimeMs = stopwatch.ElapsedMilliseconds;
            ReplaceResult(result);
        }
    }
}
