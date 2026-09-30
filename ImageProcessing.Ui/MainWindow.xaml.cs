using ImageProcessing.Core;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ImageProcessing.Ui
{
    // MainWindow (1/5) : 기본 UI 및 파일 열기/저장
    //
    // MainWindow 클래스는 아래 다섯 파일에 나누어 작성되어 있다. (partial class)
    //   MainWindow.xaml.cs          : 멤버 변수, 생성자, BMP 열기/저장
    //   MainWindow.Roi.cs           : ROI 선택/취소, Navigator ROI 표시
    //   MainWindow.Histogram.cs     : Histogram 표시, 채널(Color/R/G/B) 선택
    //   MainWindow.Processing.cs    : 기본 영상처리/필터 버튼, 처리 후 화면 갱신
    //   MainWindow.TemplateMatch.cs : 템플릿 등록, 템플릿 매칭 버튼
    public partial class MainWindow : Window
    {
        private BmpFileHandler bmpFileHandler = new BmpFileHandler();          // Bitmap 파일 관리
        private ProcessingService processingService = new ProcessingService(); // 영상처리 실행 및 결과 관리

        // ROI 관련 (MainWindow.Roi.cs에서 주로 사용)
        private Point roiStartPoint;                  // 마우스를 누른 위치 (화면 좌표)
        private bool isRoiDragging = false;           // 지금 드래그 중인지
        private bool hasRoi = false;                  // ROI가 선택되어 있는지
        private System.Drawing.Rectangle currentRoi;  // 선택된 ROI (실제 이미지 픽셀 좌표)

        public MainWindow()
        {
            InitializeComponent();
        }

        // 창이 처음 열릴 때 히스토그램 그리기
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            DrawHistograms();
        }

        // BMP 열기
        private void BtnOpen_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "BMP Files (*.bmp)|*.bmp";

            if (dialog.ShowDialog() != true)
                return;

            // 기존 화면 초기화
            ImageViewer1.Source = null;
            NavigatorViewer.Source = null;
            ImageViewer2.Source = null;
            TemplatePreview.Source = null;

            // 기존 Result/Channel 제거
            processingService.ClearResult();
            processingService.ClearChannel();

            // 원본 열어서 Viewer1과 Navigator에 표시
            BitmapSource source = bmpFileHandler.Open(dialog.FileName);
            ImageViewer1.Source = source;
            NavigatorViewer.Source = source;

            ClearRoi();             // ROI 초기화 (MainWindow.Roi.cs)
            ApplySelectedChannel(); // 선택 채널 적용 + 히스토그램 갱신 (MainWindow.Histogram.cs)
        }

        // BMP 저장
        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (processingService.ResultBitmap == null)
            {
                MessageBox.Show("저장할 처리 결과가 없습니다.");
                return;
            }

            SaveFileDialog dialog = new SaveFileDialog();
            dialog.Filter = "BMP Files (*.bmp)|*.bmp";

            if (dialog.ShowDialog() == true)
                bmpFileHandler.Save(processingService.ResultBitmap, dialog.FileName);
        }

        // 이미지가 열려 있는지 확인. 없으면 안내 메시지를 띄우고 false 반환.
        private bool CheckImageOpened()
        {
            if (bmpFileHandler.OriginalBitmap == null)
            {
                MessageBox.Show("먼저 BMP를 열어주세요.");
                return false;
            }
            return true;
        }
    }
}
