using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;

namespace ImageProcessing.Ui
{
    // MainWindow (2/5) : ROI 및 Navigator
    // 마우스 드래그로 ROI 선택, ROI 취소, Viewer1 / Viewer2 / Navigator에 ROI 사각형 표시
    public partial class MainWindow
    {
        // ROI 마우스 클릭 시작
        private void RoiCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (bmpFileHandler.OriginalBitmap == null)
                return;

            roiStartPoint = e.GetPosition(RoiCanvas); // 마우스 누른 위치 저장
            isRoiDragging = true;                     // 드래그 시작

            // ROI Rectangle 초기화
            Canvas.SetLeft(RoiRect, roiStartPoint.X);
            Canvas.SetTop(RoiRect, roiStartPoint.Y);
            RoiRect.Width = 0;
            RoiRect.Height = 0;
            RoiRect.Visibility = Visibility.Visible;
            RoiCanvas.CaptureMouse(); // 마우스가 Canvas 밖으로 나가도 이벤트를 계속 받음
        }

        // ROI 클릭 중 드래그
        private void RoiCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (isRoiDragging == false)
                return;

            Point current = e.GetPosition(RoiCanvas); // 현재 마우스 위치

            // 시작점과 현재점 중 더 작은 값을 사각형의 왼쪽 위로 사용
            double x = Math.Min(current.X, roiStartPoint.X);
            double y = Math.Min(current.Y, roiStartPoint.Y);

            // 드래그 방향과 상관없이 항상 양수가 되도록 절댓값 사용
            double width = Math.Abs(current.X - roiStartPoint.X);
            double height = Math.Abs(current.Y - roiStartPoint.Y);

            Canvas.SetLeft(RoiRect, x);
            Canvas.SetTop(RoiRect, y);
            RoiRect.Width = width;
            RoiRect.Height = height;
        }

        // ROI 클릭 후 뗌
        private void RoiCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (isRoiDragging == false)
                return;

            isRoiDragging = false;
            RoiCanvas.ReleaseMouseCapture();

            // 화면 좌표 → 실제 이미지 픽셀 좌표로 변환
            Point startPixel = CanvasPointToImagePixel(roiStartPoint);
            Point endPixel = CanvasPointToImagePixel(e.GetPosition(RoiCanvas));

            int x = (int)Math.Min(startPixel.X, endPixel.X);
            int y = (int)Math.Min(startPixel.Y, endPixel.Y);
            int width = (int)Math.Abs(endPixel.X - startPixel.X);
            int height = (int)Math.Abs(endPixel.Y - startPixel.Y);

            if (width <= 0 || height <= 0)
            {
                // 크기가 0이면 ROI 지정 안 됨
                ClearRoi();
            }
            else
            {
                // ROI 지정 성공
                currentRoi = new System.Drawing.Rectangle(x, y, width, height);
                hasRoi = true;
                UpdateRoiMarks(); // Viewer1, Viewer2, 네비게이터에 ROI 표시
            }

            UpdateHistogram(); // ROI(또는 전체) 히스토그램 출력
        }

        // ROI 취소 버튼
        private void BtnRoiCancel_Click(object sender, RoutedEventArgs e)
        {
            ClearRoi();
            UpdateHistogram(); // 전체 영역 히스토그램으로 업데이트
        }

        // ROI 선택 해제 : 값 초기화 + 화면의 ROI 사각형 숨김
        private void ClearRoi()
        {
            hasRoi = false;
            UpdateRoiMarks(); // 모든 ROI 사각형 숨김
        }

        // 화면(Canvas) 좌표 → 실제 이미지 픽셀 좌표
        // 이미지는 Canvas 크기에 맞게 확대/축소되고 가운데 정렬되므로, 그 비율과 여백을 되돌린다.
        private Point CanvasPointToImagePixel(Point canvasPoint)
        {
            // 이미지 크기
            int imageWidth = bmpFileHandler.OriginalBitmap.Width;
            int imageHeight = bmpFileHandler.OriginalBitmap.Height;

            // 확대/축소 비율 (가로 비율, 세로 비율 중 작은 쪽)
            double scale = Math.Min(RoiCanvas.ActualWidth / imageWidth, RoiCanvas.ActualHeight / imageHeight);

            // 화면에 실제로 그려진 이미지 크기
            double displayedWidth = imageWidth * scale;
            double displayedHeight = imageHeight * scale;

            // 이미지 좌우/위아래 여백
            double offsetX = (RoiCanvas.ActualWidth - displayedWidth) / 2;
            double offsetY = (RoiCanvas.ActualHeight - displayedHeight) / 2;

            // 실제 픽셀 좌표
            double pixelX = (canvasPoint.X - offsetX) / scale;
            double pixelY = (canvasPoint.Y - offsetY) / scale;

            // 이미지 범위(0 ~ 크기-1) 안으로 제한
            pixelX = Math.Max(0, Math.Min(pixelX, imageWidth - 1));
            pixelY = Math.Max(0, Math.Min(pixelY, imageHeight - 1));

            return new Point(pixelX, pixelY);
        }

        // 세 화면(Viewer1, Viewer2, Navigator)의 ROI 사각형을 현재 ROI에 맞게 다시 그린다.
        // ROI는 이미지 픽셀 좌표로 저장되어 있으므로, 화면마다 크기에 맞게 바꿔서 그린다.
        private void UpdateRoiMarks()
        {
            // Viewer1 : 드래그 중에는 마우스 위치대로 그리고 있으므로 건드리지 않는다
            if (isRoiDragging == false)
                ShowRoiOnCanvas(RoiCanvas, RoiRect);

            // Navigator
            ShowRoiOnCanvas(NavCanvas, NavRoiRect);

            // Viewer2 : 표시 중인 영상이 있을 때만 그린다
            if (Viewer2.Source != null)
                ShowRoiOnCanvas(ResultCanvas, ResultRoiRect);
            else
                ResultRoiRect.Visibility = Visibility.Collapsed;
        }

        // Canvas 하나에 ROI 사각형 표시 (ROI가 없으면 숨김)
        // Canvas는 영상(Image)과 같은 자리에 겹쳐 있고, 영상은 Stretch="Uniform"으로 가운데 정렬되어 있다.
        private void ShowRoiOnCanvas(Canvas canvas, Rectangle rect)
        {
            if (hasRoi == false || bmpFileHandler.OriginalBitmap == null ||
                canvas.ActualWidth <= 0 || canvas.ActualHeight <= 0)
            {
                rect.Visibility = Visibility.Collapsed;
                return;
            }

            // 이미지 크기 (처리 결과도 원본과 크기가 같다)
            int imageWidth = bmpFileHandler.OriginalBitmap.Width;
            int imageHeight = bmpFileHandler.OriginalBitmap.Height;

            // 확대/축소 비율과 여백 (CanvasPointToImagePixel과 같은 계산)
            double scale = Math.Min(canvas.ActualWidth / imageWidth, canvas.ActualHeight / imageHeight);
            double offsetX = (canvas.ActualWidth - imageWidth * scale) / 2;
            double offsetY = (canvas.ActualHeight - imageHeight * scale) / 2;

            // 이미지 픽셀 좌표 → 화면 좌표로 바꿔서 표시
            Canvas.SetLeft(rect, offsetX + currentRoi.X * scale);
            Canvas.SetTop(rect, offsetY + currentRoi.Y * scale);
            rect.Width = Math.Max(1, currentRoi.Width * scale);
            rect.Height = Math.Max(1, currentRoi.Height * scale);
            rect.Visibility = Visibility.Visible;
        }

        // 창 크기가 바뀌어 뷰어 크기가 달라지면 ROI 사각형 위치도 다시 계산
        private void RoiMarkCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateRoiMarks();
        }
    }
}
