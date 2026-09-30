#pragma once
#include "PixelHelper.h"

using namespace System;

namespace Native
{
    // 기본 픽셀 처리
    // 픽셀 하나의 값만 보고 계산하는 처리들을 모아 둔 클래스
    // (히스토그램 계산, 채널 추출, 이진화, 히스토그램 평활화)
    public ref class PixelProcessor
    {
    public:

        // 히스토그램
        void CalculateHistogram(IntPtr scan0, int width, int height, int stride,
            array<int>^ rHist, array<int>^ gHist, array<int>^ bHist) // 포인터 정보들 받음
        {
            unsigned char* pixels = (unsigned char*)scan0.ToPointer(); // 픽셀의 시작 주소

            // 히스토그램 배열 0~255 초기화
            for (int i = 0; i < 256; i++)
            {
                rHist[i] = 0;
                gHist[i] = 0;
                bHist[i] = 0;
            }

            for (int y = 0; y < height; y++)
            {
                unsigned char* row = pixels + y * stride; // 행 시작 위치

                for (int x = 0; x < width; x++)
                {
                    int i = x * 3; // 열 위치
                    unsigned char b = row[i];
                    unsigned char g = row[i + 1];
                    unsigned char r = row[i + 2]; // 메모리에는 B, G, R 순서로 저장됨

                    // 밝기값 개수 세기
                    rHist[r]++;
                    gHist[g]++;
                    bHist[b]++;
                }
            }
        }

        // RGB 중 채널 하나 추출 (선택한 채널 값을 B, G, R 세 곳에 모두 넣어 흑백 영상으로 만든다)
        void ExtractChannel(IntPtr scan0, int width, int height, int stride, int channelMode)
        {
            unsigned char* pixels = (unsigned char*)scan0.ToPointer(); // 시작 주소

            for (int y = 0; y < height; y++)
            {
                unsigned char* row = pixels + y * stride; // 행

                for (int x = 0; x < width; x++)
                {
                    int i = x * 3; // 열
                    unsigned char b = row[i];
                    unsigned char g = row[i + 1];
                    unsigned char r = row[i + 2];

                    // 채널값 하나 선택
                    unsigned char value = PickChannel(r, g, b, channelMode);

                    // 값 통일
                    row[i] = value;
                    row[i + 1] = value;
                    row[i + 2] = value;
                }
            }
        }

        // 이진화 : threshold 이상이면 255, 미만이면 0
        void Binarize(IntPtr scan0, int width, int height, int stride, int threshold)
        {
            unsigned char* pixels = (unsigned char*)scan0.ToPointer();

            for (int y = 0; y < height; y++)
            {
                unsigned char* row = pixels + y * stride;

                for (int x = 0; x < width; x++)
                {
                    int i = x * 3;
                    unsigned char b = row[i];
                    unsigned char g = row[i + 1];
                    unsigned char r = row[i + 2];

                    // RGB 각각 이진화
                    row[i] = (b >= threshold) ? 255 : 0;
                    row[i + 1] = (g >= threshold) ? 255 : 0;
                    row[i + 2] = (r >= threshold) ? 255 : 0;
                }
            }
        }

        // 히스토그램 평활화
        void EqualizeHistogram(IntPtr scan0, int width, int height, int stride)
        {
            unsigned char* pixels = (unsigned char*)scan0.ToPointer();

            int totalPixels = width * height; // 전체 픽셀 수
            int rHist[256] = { 0 };
            int gHist[256] = { 0 };
            int bHist[256] = { 0 };

            // 1단계: 처리 전 밝기값 개수 측정
            for (int y = 0; y < height; y++)
            {
                unsigned char* row = pixels + y * stride;

                for (int x = 0; x < width; x++)
                {
                    int i = x * 3;
                    bHist[row[i]]++;
                    gHist[row[i + 1]]++;
                    rHist[row[i + 2]]++;
                }
            }

            // 2단계: Histogram → CDF → 0~255 매핑표 생성
            unsigned char rMap[256];
            unsigned char gMap[256];
            unsigned char bMap[256];
            BuildEqualizeMap(rHist, totalPixels, rMap);
            BuildEqualizeMap(gHist, totalPixels, gMap);
            BuildEqualizeMap(bHist, totalPixels, bMap);

            // 3단계: 매핑표로 밝기값 바꾸기
            for (int y = 0; y < height; y++)
            {
                unsigned char* row = pixels + y * stride;

                for (int x = 0; x < width; x++)
                {
                    int i = x * 3;
                    row[i] = bMap[row[i]];
                    row[i + 1] = gMap[row[i + 1]];
                    row[i + 2] = rMap[row[i + 2]];
                }
            }
        }

    private:
        // R/G/B 중 선택 (1 = R, 2 = G, 3 = B)
        unsigned char PickChannel(unsigned char r, unsigned char g, unsigned char b, int channelMode)
        {
            if (channelMode == 1)
                return r;
            if (channelMode == 2)
                return g;
            return b;
        }

        // Histogram → CDF → 0~255 매핑표 생성
        void BuildEqualizeMap(int hist[256], int totalPixels, unsigned char map[256])
        {
            int cdf[256];
            int sum = 0;

            // 누적합(CDF) 계산
            for (int i = 0; i < 256; i++)
            {
                sum += hist[i]; // 밝기값별 개수
                cdf[i] = sum;   // 누적합
            }

            // 0이 아닌 첫 번째 CDF 값 찾기
            int cdfMin = 0;
            for (int i = 0; i < 256; i++)
            {
                if (cdf[i] != 0)
                {
                    cdfMin = cdf[i];
                    break;
                }
            }

            int denom = totalPixels - cdfMin;
            if (denom <= 0)
                denom = 1; // 0으로 나누기 방지

            // 매핑표 계산
            for (int i = 0; i < 256; i++)
            {
                // 0~255 범위에서 정수 반올림
                int value = (int)(((double)(cdf[i] - cdfMin) / denom) * 255.0 + 0.5);
                map[i] = PixelHelper::ClampByte(value); // 범위 제한
            }
        }
    };
}
