using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using Native;

namespace ImageProcessing.Core
{
    // ProcessingService (2/4) : 기본 영상처리
    // 히스토그램 계산, 이진화, 히스토그램 평활화, 팽창, 수축
    public partial class ProcessingService
    {
        // 히스토그램 계산 (결과는 RHistogram, GHistogram, BHistogram에 저장)
        public void CalculateHistogram(Bitmap sourceBitmap)
        {
            Bitmap target = CopyTo24bpp(sourceBitmap);
            BitmapData data = Lock24(target, ImageLockMode.ReadOnly);

            RHistogram = new int[256];
            GHistogram = new int[256];
            BHistogram = new int[256];

            PixelProcessor processor = new PixelProcessor();
            processor.CalculateHistogram(data.Scan0, data.Width, data.Height, data.Stride,
                RHistogram, GHistogram, BHistogram);

            target.UnlockBits(data); // 메모리 잠금 해제
            target.Dispose();        // 비트맵 객체 정리
        }

        // 이진화 (입력 영상 하나를 그 자리에서 바꾸는 방식)
        public void Binarize(Bitmap originalBitmap, int threshold)
        {
            Bitmap result = CopyProcessingSource(originalBitmap);
            BitmapData data = Lock24(result, ImageLockMode.ReadWrite);

            // 처리 시간 측정 시작
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            PixelProcessor processor = new PixelProcessor();
            processor.Binarize(data.Scan0, data.Width, data.Height, data.Stride, threshold);

            // 처리 시간 측정 정지
            stopwatch.Stop();

            result.UnlockBits(data);
            LastProcessingTimeMs = stopwatch.ElapsedMilliseconds; // ms(1/1000초) 단위
            ReplaceResult(result);
        }

        // 히스토그램 평활화 (입력 영상 하나를 그 자리에서 바꾸는 방식)
        public void EqualizeHistogram(Bitmap originalBitmap)
        {
            Bitmap result = CopyProcessingSource(originalBitmap);
            BitmapData data = Lock24(result, ImageLockMode.ReadWrite);

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            PixelProcessor processor = new PixelProcessor();
            processor.EqualizeHistogram(data.Scan0, data.Width, data.Height, data.Stride);

            stopwatch.Stop();

            result.UnlockBits(data);
            LastProcessingTimeMs = stopwatch.ElapsedMilliseconds;
            ReplaceResult(result);
        }

        // 팽창 (원본 source를 읽고, 결과 result에 쓰는 방식)
        public void Dilation(Bitmap originalBitmap)
        {
            Bitmap source = CopyProcessingSource(originalBitmap); // 처리 전 이미지
            Bitmap result = CopyTo24bpp(source);                  // 결과 이미지

            BitmapData srcData = Lock24(source, ImageLockMode.ReadOnly);  // source의 픽셀 메모리
            BitmapData dstData = Lock24(result, ImageLockMode.ReadWrite); // result의 픽셀 메모리

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            MorphologyProcessor processor = new MorphologyProcessor();
            processor.Dilation(srcData.Scan0, dstData.Scan0, srcData.Width, srcData.Height, srcData.Stride);

            stopwatch.Stop();

            source.UnlockBits(srcData);
            result.UnlockBits(dstData);
            source.Dispose(); // 처리 전 이미지는 더 이상 필요 없음

            LastProcessingTimeMs = stopwatch.ElapsedMilliseconds;
            ReplaceResult(result);
        }

        // 수축 (원본 source를 읽고, 결과 result에 쓰는 방식)
        public void Erosion(Bitmap originalBitmap)
        {
            Bitmap source = CopyProcessingSource(originalBitmap);
            Bitmap result = CopyTo24bpp(source);

            BitmapData srcData = Lock24(source, ImageLockMode.ReadOnly);
            BitmapData dstData = Lock24(result, ImageLockMode.ReadWrite);

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            MorphologyProcessor processor = new MorphologyProcessor();
            processor.Erosion(srcData.Scan0, dstData.Scan0, srcData.Width, srcData.Height, srcData.Stride);

            stopwatch.Stop();

            source.UnlockBits(srcData);
            result.UnlockBits(dstData);
            source.Dispose();

            LastProcessingTimeMs = stopwatch.ElapsedMilliseconds;
            ReplaceResult(result);
        }
    }
}
