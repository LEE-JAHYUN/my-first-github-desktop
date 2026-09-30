#pragma once
#include <cmath>
#include <vector>
#include "PixelHelper.h"

using namespace System;

namespace Native
{
    // 필터 (가우시안, 라플라시안, 소벨)
    // 주변 픽셀에 커널(가중치 표)을 곱해서 더하는 "컨벌루션" 방식의 처리
    public ref class FilterProcessor
    {
    public:

        // 가우시안 (흐리게 하기)
        // 2D 가우시안 커널을 가로 1D + 세로 1D 두 번으로 나누어 계산한다.
        void Gaussian(IntPtr srcScan0, IntPtr dstScan0, int width, int height, int stride,
            int kernelSize, double sigma)
        {
            unsigned char* src = (unsigned char*)srcScan0.ToPointer();
            unsigned char* dst = (unsigned char*)dstScan0.ToPointer();

            double kernel[5];
            GenerateGaussianKernel1D(kernelSize, sigma, kernel);
            int radius = kernelSize / 2;

            std::vector<double> tmp(width * height * 3); // 가로 패스 결과 (반올림X)

            // 1) 가로 패스: 1 x kernelSize
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    for (int c = 0; c < 3; c++)
                    {
                        double sum = 0;
                        for (int kx = -radius; kx <= radius; kx++)
                        {
                            int nx = PixelHelper::Clamp(x + kx, 0, width - 1); // 경계처리
                            sum += PixelHelper::GetChannelValue(src, stride, nx, y, c) * kernel[kx + radius]; // 컨벌루션
                        }
                        tmp[(y * width + x) * 3 + c] = sum;
                    }
                }
            }

            // 2) 세로 패스: kernelSize x 1
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    for (int c = 0; c < 3; c++)
                    {
                        double sum = 0;
                        for (int ky = -radius; ky <= radius; ky++)
                        {
                            int ny = PixelHelper::Clamp(y + ky, 0, height - 1);
                            sum += tmp[(ny * width + x) * 3 + c] * kernel[ky + radius];
                        }
                        dst[y * stride + x * 3 + c] = PixelHelper::ClampByte((int)(sum + 0.5)); // 여기서 한 번만 반올림
                    }
                }
            }
        }

        // 라플라시안 (윤곽선 검출)
        void Laplacian(IntPtr srcScan0, IntPtr dstScan0, int width, int height, int stride)
        {
            unsigned char* src = (unsigned char*)srcScan0.ToPointer();
            unsigned char* dst = (unsigned char*)dstScan0.ToPointer();

            int kernel[3][3] = {
                { 0, -1, 0 },
                { -1, 4, -1 },
                { 0, -1, 0 }
            };

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    for (int c = 0; c < 3; c++)
                    {
                        int sum = ConvolveChannel(src, x, y, width, height, stride, c, kernel);
                        dst[y * stride + x * 3 + c] = PixelHelper::ClampByte(sum);
                    }
                }
            }
        }

        // 소벨 (가로 방향 + 세로 방향 경계 검출)
        void Sobel(IntPtr srcScan0, IntPtr dstScan0, int width, int height, int stride)
        {
            unsigned char* src = (unsigned char*)srcScan0.ToPointer();
            unsigned char* dst = (unsigned char*)dstScan0.ToPointer();

            int gxKernel[3][3] = {
                { -1, 0, 1 },
                { -2, 0, 2 },
                { -1, 0, 1 }
            };
            int gyKernel[3][3] = {
                { -1, -2, -1 },
                { 0, 0, 0 },
                { 1, 2, 1 }
            };

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    for (int c = 0; c < 3; c++)
                    {
                        int gx = ConvolveChannel(src, x, y, width, height, stride, c, gxKernel);
                        int gy = ConvolveChannel(src, x, y, width, height, stride, c, gyKernel);

                        // 절댓값
                        if (gx < 0)
                            gx = -gx;
                        if (gy < 0)
                            gy = -gy;

                        dst[y * stride + x * 3 + c] = PixelHelper::ClampByte(gx + gy);
                    }
                }
            }
        }

    private:
        // 1D 가우시안 커널 생성
        // kernelSize는 3 또는 5. kernel[5] 중 앞의 kernelSize칸만 사용.
        void GenerateGaussianKernel1D(int kernelSize, double sigma, double kernel[5])
        {
            int radius = kernelSize / 2; // 3->1, 5->2
            double sum = 0;

            for (int i = -radius; i <= radius; i++)
            {
                // 2D의 exp(-(x²+y²)/2σ²)를 x, y로 분리한 한쪽
                double value = exp(-(double)(i * i) / (2.0 * sigma * sigma));
                kernel[i + radius] = value;
                sum += value;
            }

            // 정규화: 1D 합이 1이 되도록
            for (int i = 0; i < kernelSize; i++)
                kernel[i] /= sum;
        }

        // 커널 중심(x,y) 기준 3x3 이웃 × kernel 곱을 모두 더한 값 반환
        int ConvolveChannel(unsigned char* src, int x, int y, int width, int height, int stride,
            int channel, const int kernel[3][3])
        {
            int sum = 0;

            for (int ky = -1; ky <= 1; ky++)
            {
                for (int kx = -1; kx <= 1; kx++)
                {
                    int nx = PixelHelper::Clamp(x + kx, 0, width - 1);
                    int ny = PixelHelper::Clamp(y + ky, 0, height - 1);
                    unsigned char value = PixelHelper::GetChannelValue(src, stride, nx, ny, channel);
                    sum += value * kernel[ky + 1][kx + 1]; // ky=-1, kx=-1 -> [0][0]
                }
            }
            return sum;
        }
    };
}
