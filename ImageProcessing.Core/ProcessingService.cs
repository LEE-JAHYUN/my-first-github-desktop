using System.Drawing;
using System.Drawing.Imaging;
using Native;

namespace ImageProcessing.Core
{
    // ProcessingService (1/4) : 영상 준비 및 결과 상태 관리
    //
    // partial : 하나의 클래스를 여러 파일에 나누어 작성할 때 쓰는 키워드.
    //           ProcessingService.cs / .Basic.cs / .Filter.cs / .TemplateMatch.cs 네 파일이
    //           합쳐져서 하나의 ProcessingService 클래스가 된다. (멤버 변수를 서로 공유함)
    public partial class ProcessingService
    {
        // 영상처리 후 결과 Bitmap (ImageViewer2에 표시되는 영상)
        public Bitmap ResultBitmap { get; private set; }

        // R/G/B 모드에서 채널 밝기만 남긴 흑백 영상. Color 모드에서는 null.
        public Bitmap ChannelBitmap { get; private set; }

        // 현재 선택된 채널 (Color / R / G / B)
        public ChannelMode CurrentChannelMode { get; set; }

        // 히스토그램 (밝기 0~255 각각의 개수)
        public int[] RHistogram { get; private set; }
        public int[] GHistogram { get; private set; }
        public int[] BHistogram { get; private set; }

        // 처리 시간 (ms)
        public long LastProcessingTimeMs { get; private set; }

        // 템플릿 매칭 결과
        public int BestMatchX { get; private set; }
        public int BestMatchY { get; private set; }
        public double BestMatchScore { get; private set; }
        public int TemplateMatchWidth { get; private set; }
        public int TemplateMatchHeight { get; private set; }

        // 템플릿 매칭 결과(ResultBitmap)에는 빨간 사각형이 픽셀로 그려져 있다.
        // 다음 처리에 사각형이 섞이지 않도록, 사각형을 그리기 "전" 영상을 따로 보관한다.
        // 직전 처리가 템플릿 매칭이 아니면 null.
        private Bitmap resultWithoutBox;

        // 지금 결과(ResultBitmap)가 템플릿 매칭 결과인지 알려준다.
        // 템플릿 매칭 직후에만 resultWithoutBox가 채워져 있으므로 이것으로 판단한다.
        // (화면에서 Viewer2의 ROI 사각형을 숨길지 정할 때 사용)
        public bool IsTemplateMatchResult
        {
            get
            {
                if (resultWithoutBox != null)
                    return true;
                return false;
            }
        }

        // 어떤 형식의 Bitmap이든 24bpp(픽셀 하나 = B, G, R 3바이트) 형식으로 복사한다.
        // C++ 코드는 항상 "픽셀 하나 = 3바이트"라고 가정하고 계산하기 때문에 필요하다.
        private Bitmap CopyTo24bpp(Bitmap source)
        {
            Bitmap copy = new Bitmap(source.Width, source.Height, PixelFormat.Format24bppRgb);

            using (Graphics g = Graphics.FromImage(copy))
            {
                g.DrawImage(source, 0, 0, source.Width, source.Height);
            }
            return copy;
        }

        // Bitmap 전체를 24bpp 형식으로 메모리 잠금(LockBits)한다.
        // 반환된 BitmapData 안에 Scan0(시작 주소), Stride(한 줄 바이트 수), Width, Height가 들어 있다.
        private BitmapData Lock24(Bitmap bitmap, ImageLockMode mode)
        {
            Rectangle area = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            return bitmap.LockBits(area, mode, PixelFormat.Format24bppRgb);
        }

        // 이전 채널 이미지 제거
        public void ClearChannel()
        {
            if (ChannelBitmap != null)
            {
                ChannelBitmap.Dispose();
                ChannelBitmap = null;
            }
        }

        // R/G/B 라디오 버튼을 누르면 호출 : 선택한 채널만 남긴 흑백 영상을 만든다.
        public void ExtractChannel(Bitmap originalBitmap)
        {
            ClearChannel();
            if (originalBitmap == null || CurrentChannelMode == ChannelMode.Color)
                return;

            Bitmap copy = CopyTo24bpp(originalBitmap);
            BitmapData data = Lock24(copy, ImageLockMode.ReadWrite);

            PixelProcessor processor = new PixelProcessor();
            processor.ExtractChannel(data.Scan0, data.Width, data.Height, data.Stride, (int)CurrentChannelMode);

            copy.UnlockBits(data);
            ChannelBitmap = copy;
        }

        // 처리에 사용할 기본 영상 선택
        // Color가 아니면 채널을 분리한 흑백 영상을 처리 입력으로 쓴다.
        private Bitmap GetWorkingBitmap(Bitmap originalBitmap)
        {
            if (CurrentChannelMode != ChannelMode.Color && ChannelBitmap != null)
                return ChannelBitmap;

            return originalBitmap;
        }

        // 이번 처리의 입력 영상을 복사해서 돌려준다.
        // 직전 결과(ResultBitmap)가 있고 크기가 같으면 결과에 이어서 처리한다. (연속 처리 기능)
        // 새 이미지를 열어 크기가 달라지면 그 결과는 쓰지 않고 현재 영상에서 다시 시작한다.
        // 직전 결과가 템플릿 매칭이면 빨간 사각형이 없는 영상(resultWithoutBox)에서 이어간다.
        private Bitmap CopyProcessingSource(Bitmap originalBitmap)
        {
            Bitmap working = GetWorkingBitmap(originalBitmap);

            if (ResultBitmap != null &&
                ResultBitmap.Width == working.Width &&
                ResultBitmap.Height == working.Height)
            {
                if (resultWithoutBox != null)
                    return CopyTo24bpp(resultWithoutBox);

                return CopyTo24bpp(ResultBitmap);
            }

            return CopyTo24bpp(working);
        }

        // 이미지 열 때 / 채널 바꿀 때 이전 결과 지우기
        public void ClearResult()
        {
            if (ResultBitmap != null)
            {
                ResultBitmap.Dispose();
                ResultBitmap = null;
            }
            ClearResultWithoutBox();
        }

        // 보관해 둔 "사각형 없는 영상" 지우기
        private void ClearResultWithoutBox()
        {
            if (resultWithoutBox != null)
            {
                resultWithoutBox.Dispose();
                resultWithoutBox = null;
            }
        }

        // 연산이 끝난 비트맵을 현재 결과로 두고, 이전 결과는 이때 해제한다.
        // 새 결과에는 빨간 사각형이 없으므로 보관해 둔 영상도 함께 지운다.
        // (템플릿 매칭은 이 메서드를 부른 "뒤에" 다시 보관한다 → SaveMatchResult)
        private void ReplaceResult(Bitmap result)
        {
            if (ResultBitmap != null && ResultBitmap != result)
                ResultBitmap.Dispose();

            ResultBitmap = result;
            ClearResultWithoutBox();
        }
    }
}
