using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using Native;

namespace ImageProcessing.Core
{
    // ProcessingService (4/4) : 템플릿 매칭
    // DIFF, CORR, COEFF 모두 같은 순서로 동작한다.
    //   1) 원본/템플릿을 24bpp로 복사  2) 두 영상 LockBits  3) C++ 매칭 호출(시간 측정)
    //   4) UnlockBits  5) 결과 좌표 저장 + 빨간 사각형 그리기 (SaveMatchResult)
    public partial class ProcessingService
    {
        // DIFF
        public void TemplateMatchDiff(Bitmap originalBitmap, Bitmap templateBitmap)
        {
            Bitmap image = CopyProcessingSource(originalBitmap);
            Bitmap template = CopyTo24bpp(templateBitmap);

            BitmapData imgData = Lock24(image, ImageLockMode.ReadOnly);
            BitmapData tmplData = Lock24(template, ImageLockMode.ReadOnly);

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            TemplateMatchProcessor processor = new TemplateMatchProcessor();
            double[] matchResult = processor.TemplateMatchDiff(
                imgData.Scan0, imgData.Width, imgData.Height, imgData.Stride,
                tmplData.Scan0, tmplData.Width, tmplData.Height, tmplData.Stride);

            stopwatch.Stop();

            image.UnlockBits(imgData);
            template.UnlockBits(tmplData);
            template.Dispose();

            SaveMatchResult(image, templateBitmap, matchResult, stopwatch.ElapsedMilliseconds);
        }

        // CORR
        public void TemplateMatchCorr(Bitmap originalBitmap, Bitmap templateBitmap)
        {
            Bitmap image = CopyProcessingSource(originalBitmap);
            Bitmap template = CopyTo24bpp(templateBitmap);

            BitmapData imgData = Lock24(image, ImageLockMode.ReadOnly);
            BitmapData tmplData = Lock24(template, ImageLockMode.ReadOnly);

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            TemplateMatchProcessor processor = new TemplateMatchProcessor();
            double[] matchResult = processor.TemplateMatchCorr(
                imgData.Scan0, imgData.Width, imgData.Height, imgData.Stride,
                tmplData.Scan0, tmplData.Width, tmplData.Height, tmplData.Stride);

            stopwatch.Stop();

            image.UnlockBits(imgData);
            template.UnlockBits(tmplData);
            template.Dispose();

            SaveMatchResult(image, templateBitmap, matchResult, stopwatch.ElapsedMilliseconds);
        }

        // COEFF
        public void TemplateMatchCoeff(Bitmap originalBitmap, Bitmap templateBitmap)
        {
            Bitmap image = CopyProcessingSource(originalBitmap);
            Bitmap template = CopyTo24bpp(templateBitmap);

            BitmapData imgData = Lock24(image, ImageLockMode.ReadOnly);
            BitmapData tmplData = Lock24(template, ImageLockMode.ReadOnly);

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            TemplateMatchProcessor processor = new TemplateMatchProcessor();
            double[] matchResult = processor.TemplateMatchCoeff(
                imgData.Scan0, imgData.Width, imgData.Height, imgData.Stride,
                tmplData.Scan0, tmplData.Width, tmplData.Height, tmplData.Stride);

            stopwatch.Stop();

            image.UnlockBits(imgData);
            template.UnlockBits(tmplData);
            template.Dispose();

            SaveMatchResult(image, templateBitmap, matchResult, stopwatch.ElapsedMilliseconds);
        }

        // 매칭 결과 저장 + 결과 영상에 빨간 사각형 그리기 (세 매칭 방식 공통)
        // matchResult : [0] = X, [1] = Y, [2] = 점수  (C++에서 돌려준 배열)
        private void SaveMatchResult(Bitmap image, Bitmap templateBitmap, double[] matchResult, long elapsedMs)
        {
            BestMatchX = (int)matchResult[0];
            BestMatchY = (int)matchResult[1];
            BestMatchScore = matchResult[2];

            TemplateMatchWidth = templateBitmap.Width;
            TemplateMatchHeight = templateBitmap.Height;
            LastProcessingTimeMs = elapsedMs;

            // 찾은 위치에 빨간 사각형 그리기
            using (Graphics g = Graphics.FromImage(image))
            {
                using (Pen pen = new Pen(Color.Red, 2))
                {
                    g.DrawRectangle(pen, BestMatchX, BestMatchY, TemplateMatchWidth, TemplateMatchHeight);
                }
            }

            ReplaceResult(image);
        }
    }
}
