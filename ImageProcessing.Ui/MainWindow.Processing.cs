using System.Windows;

namespace ImageProcessing.Ui
{
    // MainWindow (4/5) : 영상처리 / Filter 버튼 처리
    // 모든 버튼은 같은 순서로 동작한다.
    //   1) 이미지가 열려 있는지 확인 (CheckImageOpened)
    //   2) 화면에서 값 읽기 (슬라이더, 콤보박스)
    //   3) processingService의 처리 메서드 호출
    //   4) 처리 결과를 화면에 표시 (OnProcessingCompleted)
    public partial class MainWindow
    {
        // 처리 완료 후 공통 화면 갱신
        private void OnProcessingCompleted()
        {
            Viewer2.Source = bmpFileHandler.ToBitmapSource(processingService.ResultBitmap); // 결과를 뷰어2에 표시
            TxtTime.Text = processingService.LastProcessingTimeMs + " ms"; // 처리 시간 표시
            RbResult.IsEnabled = true; // 처리결과 히스토그램 활성화
            RbResult.IsChecked = true; // 처리결과 히스토그램 자동 선택
            UpdateRoiMarks();          // 뷰어2에도 ROI 사각형 표시 (MainWindow.Roi.cs)
            UpdateHistogram();         // 히스토그램 갱신
        }

        // 이진화 슬라이더 값 표시
        private void SldThreshold_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtThreshold == null)
                return;

            TxtThreshold.Text = "현재 값: " + (int)SldThreshold.Value;
        }

        // 이진화
        private void BtnBinarize_Click(object sender, RoutedEventArgs e)
        {
            if (CheckImageOpened() == false)
                return;

            int threshold = (int)SldThreshold.Value; // 슬라이더 선택값
            processingService.Binarize(bmpFileHandler.OriginalBitmap, threshold);
            OnProcessingCompleted();
        }

        // 평활화
        private void BtnEqualize_Click(object sender, RoutedEventArgs e)
        {
            if (CheckImageOpened() == false)
                return;

            processingService.EqualizeHistogram(bmpFileHandler.OriginalBitmap);
            OnProcessingCompleted();
        }

        // 팽창
        private void BtnDilation_Click(object sender, RoutedEventArgs e)
        {
            if (CheckImageOpened() == false)
                return;

            processingService.Dilation(bmpFileHandler.OriginalBitmap);
            OnProcessingCompleted();
        }

        // 수축
        private void BtnErosion_Click(object sender, RoutedEventArgs e)
        {
            if (CheckImageOpened() == false)
                return;

            processingService.Erosion(bmpFileHandler.OriginalBitmap);
            OnProcessingCompleted();
        }

        // 가우시안 시그마 슬라이더 값 표시
        private void SldSigma_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtSigma == null)
                return;

            TxtSigma.Text = SldSigma.Value.ToString("F1"); // 소수점 아래 한 자리까지 표시
        }

        // 가우시안
        private void BtnGaussian_Click(object sender, RoutedEventArgs e)
        {
            if (CheckImageOpened() == false)
                return;

            // 콤보박스 Index 0 → 3x3, Index 1 → 5x5
            int kernelSize = 3;
            if (CmbKernel.SelectedIndex == 1)
                kernelSize = 5;

            double sigma = SldSigma.Value; // 슬라이더 선택값

            processingService.Gaussian(bmpFileHandler.OriginalBitmap, kernelSize, sigma);
            OnProcessingCompleted();
        }

        // 라플라시안
        private void BtnLaplacian_Click(object sender, RoutedEventArgs e)
        {
            if (CheckImageOpened() == false)
                return;

            processingService.Laplacian(bmpFileHandler.OriginalBitmap);
            OnProcessingCompleted();
        }

        // 소벨
        private void BtnSobel_Click(object sender, RoutedEventArgs e)
        {
            if (CheckImageOpened() == false)
                return;

            processingService.Sobel(bmpFileHandler.OriginalBitmap);
            OnProcessingCompleted();
        }
    }
}
