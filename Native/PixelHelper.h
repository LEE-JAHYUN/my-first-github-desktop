#pragma once

namespace Native
{
    // 여러 영상처리 클래스가 함께 사용하는 작은 보조 함수 모음
    // ref class가 아닌 일반 C++ class이므로 C#에서는 보이지 않는다. (C++ 내부 전용)
    // 모든 함수가 static이라서 객체를 만들지 않고 PixelHelper::Clamp(...) 처럼 바로 호출한다.
    class PixelHelper
    {
    public:
        // 가장자리 처리(좌표용) : 이미지 바깥 접근 방지
        // 최솟값보다 작으면 최솟값, 최댓값보다 크면 최댓값으로 바꾼다.
        static int Clamp(int value, int minValue, int maxValue) // (현재값, 최솟값, 최댓값)
        {
            if (value < minValue)
                return minValue;
            if (value > maxValue)
                return maxValue;
            return value;
        }

        // 0~255 범위 지정(픽셀값 제한)
        // 픽셀 하나에 저장할 수 있는 값은 0~255 이므로 범위를 넘으면 잘라낸다.
        static unsigned char ClampByte(int value)
        {
            if (value < 0)
                return 0;
            if (value > 255)
                return 255;
            return (unsigned char)value;
        }

        // (x, y) 위치 픽셀의 채널 값 하나를 읽는다.
        // channel : 0 = B, 1 = G, 2 = R  (24bpp BMP는 B, G, R 순서로 저장됨)
        // y * stride     : y번째 줄의 시작 위치
        // x * 3          : 그 줄 안에서 x번째 픽셀의 위치 (픽셀 하나 = 3바이트)
        static unsigned char GetChannelValue(unsigned char* buffer, int stride, int x, int y, int channel)
        {
            return buffer[y * stride + x * 3 + channel];
        }
    };
}
