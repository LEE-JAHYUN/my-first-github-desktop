using ImageProcessing.Core;
using System.Windows;

namespace ImageProcessing.Ui
{
    // MainWindow (5/5) : Template 등록 및 Template Matching 버튼 처리
    public partial class MainWindow
    {
        // 선택한 ROI를 템플릿 이미지로 등록하고 Preview에 표시
        private void BtnRegisterTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (hasRoi == false)
            {
                MessageBox.Show("먼저 ROI를 선택해주세요.");
                return;
            }

            bmpFileHandler.RegisterTemplate(bmpFileHandler.OriginalBitmap, currentRoi);             // 템플릿 이미지 저장
            TemplatePreview.Source = bmpFileHandler.ToBitmapSource(bmpFileHandler.TemplateBitmap); // Preview 표시
        }

        // 템플릿 매칭을 실행할 수 있는지 검사. 문제가 있으면 안내 메시지를 띄우고 false 반환.
        private bool CanRunTemplateMatching()
        {
            if (processingService.CurrentChannelMode != ChannelMode.Color)
            {
                MessageBox.Show("템플릿 매칭은 Color 모드에서만 사용할 수 있습니다.");
                return false;
            }

            if (bmpFileHandler.OriginalBitmap == null)
            {
                MessageBox.Show("먼저 BMP를 열어주세요.");
                return false;
            }

            if (bmpFileHandler.TemplateBitmap == null)
            {
                MessageBox.Show("먼저 Template을 등록해주세요.");
                return false;
            }

            if (bmpFileHandler.TemplateBitmap.Width > bmpFileHandler.OriginalBitmap.Width ||
                bmpFileHandler.TemplateBitmap.Height > bmpFileHandler.OriginalBitmap.Height)
            {
                MessageBox.Show("Template 크기가 원본보다 큽니다.");
                return false;
            }

            return true;
        }

        // DIFF
        private void BtnDiff_Click(object sender, RoutedEventArgs e)
        {
            if (CanRunTemplateMatching() == false)
                return;

            ImageViewer2.Source = null;
            processingService.TemplateMatchDiff(bmpFileHandler.OriginalBitmap, bmpFileHandler.TemplateBitmap);
            OnProcessingCompleted();
        }

        // CORR
        private void BtnCorr_Click(object sender, RoutedEventArgs e)
        {
            if (CanRunTemplateMatching() == false)
                return;

            ImageViewer2.Source = null;
            processingService.TemplateMatchCorr(bmpFileHandler.OriginalBitmap, bmpFileHandler.TemplateBitmap);
            OnProcessingCompleted();
        }

        // COEFF
        private void BtnCoeff_Click(object sender, RoutedEventArgs e)
        {
            if (CanRunTemplateMatching() == false)
                return;

            ImageViewer2.Source = null;
            processingService.TemplateMatchCoeff(bmpFileHandler.OriginalBitmap, bmpFileHandler.TemplateBitmap);
            OnProcessingCompleted();
        }
    }
}
