#pragma once

// 예전에는 이 파일 하나에 ImageProcessor 클래스(약 660줄)가 모두 들어 있었다.
// 지금은 기능별로 나눈 헤더 파일들을 한 번에 포함(#include)하는 역할만 한다.
// 기존 .cpp 파일이 #include "ImageProcessing.h" 를 그대로 사용해도 모든 클래스가 빌드된다.

#include "PixelHelper.h"            // 공통 보조 함수 (C++ 내부 전용)
#include "PixelProcessor.h"         // 기본 픽셀 처리 : 히스토그램, 채널 추출, 이진화, 평활화
#include "MorphologyProcessor.h"    // 형태학 처리    : 팽창, 수축
#include "FilterProcessor.h"        // 필터          : 가우시안, 라플라시안, 소벨
#include "TemplateMatchProcessor.h" // 템플릿 매칭    : DIFF, CORR, COEFF
