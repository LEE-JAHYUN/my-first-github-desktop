using ImageProcessing.Core;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ImageProcessing.Ui
{
    // MainWindow (3/5) : Histogram 및 Channel UI
    // 히스토그램 계산 요청과 그리기, Color/R/G/B 채널 선택 처리
    public partial class MainWindow
    {
        // 히스토그램 다시 계산 + 다시 그리기
        private void UpdateHistogram()
        {
            if (bmpFileHandler.OriginalBitmap == null)
                return;

            // 1. 어떤 영상의 히스토그램을 계산할지 선택
            //    "처리결과"가 선택되어 있고 결과가 있으면 → 결과 영상
            //    R/G/B 모드이면 → 채널 영상
            //    그 외 → 원본 영상
            System.Drawing.Bitmap baseBitmap = bmpFileHandler.OriginalBitmap;

            if (RbHistResult.IsChecked == true && processingService.ResultBitmap != null)
                baseBitmap = processingService.ResultBitmap;
            else if (processingService.CurrentChannelMode != ChannelMode.Color && processingService.ChannelBitmap != null)
                baseBitmap = processingService.ChannelBitmap;

            // 2. ROI가 있으면 ROI 부분만 잘라낸 Bitmap으로 계산
            System.Drawing.Bitmap target = baseBitmap; // 히스토그램을 계산할 Bitmap
            bool isCloned = false;                     // 잘라서 새로 만들었는지 (나중에 해제용)

            if (hasRoi == true)
            {
                System.Drawing.Rectangle roi = currentRoi;

                // ROI가 이미지 밖으로 나갔으면 이미지 안쪽 부분만 남긴다
                if (roi.Right > baseBitmap.Width || roi.Bottom > baseBitmap.Height)
                {
                    System.Drawing.Rectangle imageArea = new System.Drawing.Rectangle(0, 0, baseBitmap.Width, baseBitmap.Height);
                    roi = System.Drawing.Rectangle.Intersect(roi, imageArea);
                }

                // ROI가 정상 크기이면 잘라서 새로운 Bitmap 생성
                if (roi.Width > 0 && roi.Height > 0)
                {
                    target = baseBitmap.Clone(roi, baseBitmap.PixelFormat);
                    isCloned = true;
                }
            }

            // 3. 히스토그램 배열 계산 (ProcessingService → C++ PixelProcessor)
            processingService.CalculateHistogram(target);

            if (isCloned == true)
                target.Dispose(); // 잘라낸 Bitmap은 계산 후 해제

            // 4. 상태 표시 후 그리기
            if (hasRoi == true)
                TxtHistRoi.Text = "ROI 영역";
            else
                TxtHistRoi.Text = "전체 영역";

            DrawHistograms();
        }

        // R, G, B 히스토그램을 각각 그린다. (보이는 줄만 그림)
        private void DrawHistograms()
        {
            if (HistogramRCanvas == null || processingService.RHistogram == null)
                return;

            if (HistogramRBorder.Visibility == Visibility.Visible)
                DrawHistogramBoxes(HistogramRCanvas, processingService.RHistogram, Colors.Red);
            if (HistogramGBorder.Visibility == Visibility.Visible)
                DrawHistogramBoxes(HistogramGCanvas, processingService.GHistogram, Colors.LimeGreen);
            if (HistogramBBorder.Visibility == Visibility.Visible)
                DrawHistogramBoxes(HistogramBCanvas, processingService.BHistogram, Colors.DeepSkyBlue);
        }

        // 밝기 0~255, 칸 하나마다 Rectangle 하나를 아래에서 위로 채움
        private void DrawHistogramBoxes(Canvas canvas, int[] histogram, Color color)
        {
            canvas.Children.Clear();
            double w = canvas.ActualWidth;
            double h = canvas.ActualHeight;

            if (w <= 0 || h <= 0 || histogram == null || histogram.Length < 256)
                return;

            // 가장 큰 값 찾기 (막대 높이의 기준)
            int maxValue = 1;
            for (int i = 0; i < 256; i++)
            {
                if (histogram[i] > maxValue)
                    maxValue = histogram[i];
            }

            double boxWidth = w / 256.0;

            SolidColorBrush brush = new SolidColorBrush(color);
            brush.Freeze();

            for (int i = 0; i < 256; i++)
            {
                double filled = h * histogram[i] / maxValue;

                // 값이 있는데 너무 작아 안 보이면 최소 1픽셀로 표시
                if (histogram[i] > 0 && filled < 1)
                    filled = 1;

                Rectangle box = new Rectangle();
                box.Width = Math.Max(0.5, boxWidth - 0.5);
                box.Height = filled;
                box.Fill = brush;
                Canvas.SetLeft(box, i * boxWidth);
                Canvas.SetTop(box, h - filled);
                canvas.Children.Add(box);
            }
        }

        // Color는 R/G/B를 모두 보여주고, R/G/B 모드는 해당 채널만 보여준다.
        private void UpdateHistogramLayout()
        {
            if (HistRowR == null)
                return;

            ChannelMode mode = processingService.CurrentChannelMode;
            SetHistogramRow(HistRowR, TxtHistR, HistogramRBorder, mode == ChannelMode.Color || mode == ChannelMode.R);
            SetHistogramRow(HistRowG, TxtHistG, HistogramGBorder, mode == ChannelMode.Color || mode == ChannelMode.G);
            SetHistogramRow(HistRowB, TxtHistB, HistogramBBorder, mode == ChannelMode.Color || mode == ChannelMode.B);
        }

        // 히스토그램 한 줄 보이기/숨기기
        private void SetHistogramRow(RowDefinition row, TextBlock label, Border border, bool show)
        {
            if (show == true)
            {
                row.Height = new GridLength(1, GridUnitType.Star);
                label.Visibility = Visibility.Visible;
                border.Visibility = Visibility.Visible;
            }
            else
            {
                row.Height = new GridLength(0);
                label.Visibility = Visibility.Collapsed;
                border.Visibility = Visibility.Collapsed;
            }
        }

        // 원본/처리결과 라디오 버튼 : 히스토그램 숫자 자체를 다시 계산
        private void HistogramSource_Checked(object sender, RoutedEventArgs e)
        {
            UpdateHistogram();
        }

        // 창 크기 변경 : 이미 계산된 히스토그램을 화면에 맞게 다시 그림
        private void HistogramCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            DrawHistograms();
        }

        // 채널 모드 라디오 버튼 선택
        private void ChannelMode_Checked(object sender, RoutedEventArgs e)
        {
            if (RbColor == null)
                return;

            if (RbColor.IsChecked == true)
                processingService.CurrentChannelMode = ChannelMode.Color;
            else if (RbR.IsChecked == true)
                processingService.CurrentChannelMode = ChannelMode.R;
            else if (RbG.IsChecked == true)
                processingService.CurrentChannelMode = ChannelMode.G;
            else if (RbB.IsChecked == true)
                processingService.CurrentChannelMode = ChannelMode.B;

            ApplySelectedChannel();
        }

        // 실제 채널 변경 담당. 모드를 바꾸면 먼저 화면을 바꾸고, 영상처리는 다음 버튼에서 적용한다.
        private void ApplySelectedChannel()
        {
            UpdateHistogramLayout();
            if (bmpFileHandler.OriginalBitmap == null)
                return;

            // 이전 처리 결과 초기화
            ImageViewer2.Source = null;
            processingService.ClearResult();
            TxtProcessingTime.Text = "0 ms";
            RbHistResult.IsEnabled = false;
            RbHistOriginal.IsChecked = true;

            if (processingService.CurrentChannelMode == ChannelMode.Color)
            {
                processingService.ClearChannel();
                ImageViewer2.Source = null;
            }
            else
            {
                processingService.ExtractChannel(bmpFileHandler.OriginalBitmap);
                ImageViewer2.Source = bmpFileHandler.ToBitmapSource(processingService.ChannelBitmap);
            }

            UpdateHistogram();
        }
    }
}
