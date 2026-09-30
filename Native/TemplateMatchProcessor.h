#pragma once
#include "PixelHelper.h"

using namespace System;

namespace Native
{
    // 템플릿 매칭 (DIFF, CORR, COEFF)
    // 템플릿을 원본 위에서 한 픽셀씩 옮기면서 점수를 계산하고, 가장 좋은 위치를 찾는다.
    // 결과는 double 배열 3칸 : [0] = 최적 X, [1] = 최적 Y, [2] = 점수
    public ref class TemplateMatchProcessor
    {
    public:

        // DIFF : 픽셀 차이의 절댓값을 누적. 작을수록 Best Match.
        array<double>^ TemplateMatchDiff(
            IntPtr imageScan0, int imageWidth, int imageHeight, int imageStride,
            IntPtr templateScan0, int templateWidth, int templateHeight, int templateStride)
        {
            unsigned char* image = (unsigned char*)imageScan0.ToPointer();
            unsigned char* tmpl = (unsigned char*)templateScan0.ToPointer();

            int bestX = 0;
            int bestY = 0;
            double bestScore = 1e18; // Diff는 작을수록 좋으므로 아주 큰 값으로 시작

            // 템플릿 이동 범위
            int maxX = imageWidth - templateWidth;
            int maxY = imageHeight - templateHeight;

            // (x, y) : 템플릿이 놓인 위치
            for (int y = 0; y <= maxY; y++)
            {
                for (int x = 0; x <= maxX; x++)
                {
                    double sum = 0;

                    // (tx, ty) : 템플릿 내부 좌표
                    for (int ty = 0; ty < templateHeight; ty++)
                    {
                        for (int tx = 0; tx < templateWidth; tx++)
                        {
                            for (int c = 0; c < 3; c++) // B, G, R 채널 비교
                            {
                                unsigned char imagePixel = PixelHelper::GetChannelValue(image, imageStride, x + tx, y + ty, c);
                                unsigned char templatePixel = PixelHelper::GetChannelValue(tmpl, templateStride, tx, ty, c);
                                int diff = imagePixel - templatePixel;
                                if (diff < 0)
                                    diff = -diff;
                                sum += diff;
                            }
                        }
                    }

                    // 더 작은 점수가 나오면 갱신
                    if (sum < bestScore)
                    {
                        bestScore = sum;
                        bestX = x;
                        bestY = y;
                    }
                }
            }
            return MakeResult(bestX, bestY, bestScore);
        }

        // CORR : 픽셀 곱을 누적. 클수록 Best Match.
        array<double>^ TemplateMatchCorr(
            IntPtr imageScan0, int imageWidth, int imageHeight, int imageStride,
            IntPtr templateScan0, int templateWidth, int templateHeight, int templateStride)
        {
            unsigned char* image = (unsigned char*)imageScan0.ToPointer();
            unsigned char* tmpl = (unsigned char*)templateScan0.ToPointer();

            int bestX = 0;
            int bestY = 0;
            double bestScore = -1e18; // CORR는 클수록 좋으므로 아주 작은 값으로 시작

            int maxX = imageWidth - templateWidth;
            int maxY = imageHeight - templateHeight;

            for (int y = 0; y <= maxY; y++)
            {
                for (int x = 0; x <= maxX; x++)
                {
                    double sum = 0;

                    for (int ty = 0; ty < templateHeight; ty++)
                    {
                        for (int tx = 0; tx < templateWidth; tx++)
                        {
                            for (int c = 0; c < 3; c++)
                            {
                                unsigned char imagePixel = PixelHelper::GetChannelValue(image, imageStride, x + tx, y + ty, c);
                                unsigned char templatePixel = PixelHelper::GetChannelValue(tmpl, templateStride, tx, ty, c);
                                sum += (double)imagePixel * (double)templatePixel;
                            }
                        }
                    }

                    // 더 큰 점수가 나오면 갱신
                    if (sum > bestScore)
                    {
                        bestScore = sum;
                        bestX = x;
                        bestY = y;
                    }
                }
            }
            return MakeResult(bestX, bestY, bestScore);
        }

        // COEFF : (imagePixel - imageMean) * (templatePixel - templateMean)를 누적. 클수록 Best Match.
        array<double>^ TemplateMatchCoeff(
            IntPtr imageScan0, int imageWidth, int imageHeight, int imageStride,
            IntPtr templateScan0, int templateWidth, int templateHeight, int templateStride)
        {
            unsigned char* image = (unsigned char*)imageScan0.ToPointer();
            unsigned char* tmpl = (unsigned char*)templateScan0.ToPointer();

            // 1단계: Template 평균 계산 (한 번만 계산하면 된다)
            double templateMean[3];
            CalculateChannelMeans(tmpl, templateStride, 0, 0, templateWidth, templateHeight, templateMean);

            int bestX = 0;
            int bestY = 0;
            double bestScore = -1e18;

            int maxX = imageWidth - templateWidth;
            int maxY = imageHeight - templateHeight;

            for (int y = 0; y <= maxY; y++)
            {
                for (int x = 0; x <= maxX; x++)
                {
                    // 2단계: 현재 위치의 이미지 평균 계산
                    double imageMean[3];
                    CalculateChannelMeans(image, imageStride, x, y, templateWidth, templateHeight, imageMean);

                    // 3단계: 매칭 값 계산
                    double sum = 0;
                    for (int ty = 0; ty < templateHeight; ty++)
                    {
                        for (int tx = 0; tx < templateWidth; tx++)
                        {
                            for (int c = 0; c < 3; c++)
                            {
                                unsigned char imagePixel = PixelHelper::GetChannelValue(image, imageStride, x + tx, y + ty, c);
                                unsigned char templatePixel = PixelHelper::GetChannelValue(tmpl, templateStride, tx, ty, c);
                                sum += (imagePixel - imageMean[c]) * (templatePixel - templateMean[c]);
                            }
                        }
                    }

                    if (sum > bestScore)
                    {
                        bestScore = sum;
                        bestX = x;
                        bestY = y;
                    }
                }
            }
            return MakeResult(bestX, bestY, bestScore);
        }

    private:
        // 템플릿 매칭 보조 1 : (startX, startY)부터 width x height 영역의 채널별 평균 계산
        // means[0] = B 평균, means[1] = G 평균, means[2] = R 평균
        void CalculateChannelMeans(unsigned char* buffer, int stride, int startX, int startY,
            int width, int height, double means[3])
        {
            for (int c = 0; c < 3; c++)
            {
                double sum = 0;

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                        sum += PixelHelper::GetChannelValue(buffer, stride, startX + x, startY + y, c);
                }
                means[c] = sum / (width * height);
            }
        }

        // 템플릿 매칭 보조 2 : C#으로 돌려줄 결과 배열 만들기
        array<double>^ MakeResult(int bestX, int bestY, double bestScore)
        {
            array<double>^ result = gcnew array<double>(3);
            result[0] = bestX;
            result[1] = bestY;
            result[2] = bestScore;
            return result;
        }
    };
}
