#pragma once
#include <vector>
#include "PixelHelper.h"

using namespace System;

namespace Native
{
    // 형태학 처리 (팽창, 수축)
    // 3x3 영역을 한 번에 보지 않고 "가로 1x3" → "세로 3x1" 두 번으로 나누어 계산한다.
    // (결과는 3x3 한 번과 같고, 계산량이 줄어든다)
    public ref class MorphologyProcessor
    {
    public:

        // 팽창 : 주변 3x3 중 가장 큰 값으로 바꾼다. (밝은 부분이 넓어짐)
        void Dilation(IntPtr srcScan0, IntPtr dstScan0, int width, int height, int stride)
        {
            unsigned char* src = (unsigned char*)srcScan0.ToPointer(); // 원본 이미지
            unsigned char* dst = (unsigned char*)dstScan0.ToPointer(); // 결과 이미지

            // 가로 패스 결과 저장
            // src/dst와 똑같이 한 줄 = stride 칸으로 만든다. (줄 끝 여백 포함)
            // 그래서 src, dst, tmp 모두 같은 공식 y * stride + x * 3 + c 로 위치를 계산한다.
            std::vector<unsigned char> tmp(stride * height);

            // 1) 가로 패스: 1x3
            for (int y = 0; y < height; y++) // 행
            {
                for (int x = 0; x < width; x++) // 열
                {
                    for (int c = 0; c < 3; c++) // 채널
                    {
                        unsigned char maxValue = 0;

                        for (int dx = -1; dx <= 1; dx++)
                        {
                            // 범위를 벗어나면 0 또는 마지막 좌표(width - 1)로 제한
                            int nx = PixelHelper::Clamp(x + dx, 0, width - 1);
                            unsigned char value = PixelHelper::GetChannelValue(src, stride, nx, y, c);
                            if (value > maxValue)
                                maxValue = value;
                        }
                        tmp[y * stride + x * 3 + c] = maxValue;
                    }
                }
            }

            // 2) 세로 패스: 3x1 (가로 패스 결과 tmp를 읽는다)
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    for (int c = 0; c < 3; c++)
                    {
                        unsigned char maxValue = 0;

                        for (int dy = -1; dy <= 1; dy++)
                        {
                            int ny = PixelHelper::Clamp(y + dy, 0, height - 1);
                            unsigned char value = tmp[ny * stride + x * 3 + c];
                            if (value > maxValue)
                                maxValue = value;
                        }
                        dst[y * stride + x * 3 + c] = maxValue;
                    }
                }
            }
        }

        // 수축 : 주변 3x3 중 가장 작은 값으로 바꾼다. (어두운 부분이 넓어짐)
        void Erosion(IntPtr srcScan0, IntPtr dstScan0, int width, int height, int stride)
        {
            unsigned char* src = (unsigned char*)srcScan0.ToPointer();
            unsigned char* dst = (unsigned char*)dstScan0.ToPointer();

            std::vector<unsigned char> tmp(stride * height); // 가로 패스 결과 저장 (src와 같은 모양)

            // 1) 가로 패스: 1x3
            for (int y = 0; y < height; y++) // 행
            {
                for (int x = 0; x < width; x++) // 열
                {
                    for (int c = 0; c < 3; c++) // 채널
                    {
                        unsigned char minValue = 255;

                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = PixelHelper::Clamp(x + dx, 0, width - 1);
                            unsigned char value = PixelHelper::GetChannelValue(src, stride, nx, y, c);
                            if (value < minValue)
                                minValue = value;
                        }
                        tmp[y * stride + x * 3 + c] = minValue;
                    }
                }
            }

            // 2) 세로 패스: 3x1
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    for (int c = 0; c < 3; c++)
                    {
                        unsigned char minValue = 255;

                        for (int dy = -1; dy <= 1; dy++)
                        {
                            int ny = PixelHelper::Clamp(y + dy, 0, height - 1);
                            unsigned char value = tmp[ny * stride + x * 3 + c];
                            if (value < minValue)
                                minValue = value;
                        }
                        dst[y * stride + x * 3 + c] = minValue;
                    }
                }
            }
        }
    };
}
