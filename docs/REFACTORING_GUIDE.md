# 영상처리 프로그램 리팩토링 설명서

> 목표 우선순위 : **기능 유지 → 초보자가 읽기 쉬운 코드 → 기능별 분리**

---

## 1. 기존 프로젝트 구조와 각 클래스의 역할 분석

| 프로젝트 | 파일 | 줄 수 | 역할 |
|---|---|---|---|
| ImageProcessing.Ui (WPF, C#) | MainWindow.xaml.cs | 583 | 화면, 버튼, ROI, 히스토그램 그리기, 채널 선택, 모든 버튼 처리 |
| ImageProcessing.Core (C#) | ProcessingService.cs | 330 | 24bpp 복사, LockBits, 결과/채널 상태 관리, 처리 실행, 시간 측정 |
| | BmpFileHandler.cs | 56 | BMP 열기/저장, 템플릿 잘라내기, Bitmap→BitmapSource 변환 |
| | ChannelMode.cs | 10 | Color/R/G/B 열거형 |
| Native (C++/CLI) | ImageProcessing.h (`ImageProcessor`) | 665 | 실제 픽셀 계산 전체 |

### 호출 관계 (기존)

```
MainWindow ──→ BmpFileHandler      (열기, 저장, 템플릿 등록, 화면용 변환)
MainWindow ──→ ProcessingService   (처리 요청, 결과/히스토그램 읽기)
ProcessingService ──→ ImageProcessor (C++/CLI, Scan0/Stride 전달)
```

### 발견한 문제점

1. **클래스가 너무 김** : MainWindow(583줄), ImageProcessor(665줄), ProcessingService(330줄)
2. **초보자에게 어려운 문법** : ProcessingService의 `RunFilter(Action<...>)`, `RunTemplateMatch(Func<...>)`와 람다식 `(processor, srcData, dstData) => ...`
3. **Nullable 구조체** : `System.Drawing.Rectangle? currentRoi` 와 `.Value`
4. **사용하지 않는 using** : `System.Linq`, `System.Reflection`, `using static System.Net.Mime.MediaTypeNames` (마지막 것은 `Image` 이름 충돌 위험까지 있음)
5. **버그 1개 (팽창/수축)** : 세로 패스에서 `tmp[ny * stride + x * 3 + c]` 로 읽었지만, tmp는 `(y * width + x) * 3 + c` 로 저장되어 있음.
   이미지 가로 크기가 4의 배수일 때는 `stride == width * 3` 이라 결과가 같지만,
   그렇지 않으면 **다른 위치를 읽거나 배열 밖을 읽게 됨** → `(ny * width + x) * 3 + c` 로 수정.
   (가로가 4의 배수인 이미지는 결과가 기존과 100% 동일)

---

## 2. 새로운 구조 제안 (나눈 기준)

### 기준

* **호출 관계가 같은 것끼리** 묶는다. (예: 필터 3개는 모두 "source 읽기 → result 쓰기" 흐름)
* 짧은 클래스(BmpFileHandler, ChannelMode)는 **그대로** 둔다.
* C#은 `partial class` 로 **클래스 하나를 여러 파일에 나눈다**.
  * MainWindow는 XAML과 연결되어 있어 반드시 하나의 클래스여야 한다.
  * ProcessingService는 결과 영상(ResultBitmap) 같은 **상태를 모든 기능이 공유**하므로,
    진짜로 여러 클래스로 쪼개면 상태를 주고받는 코드가 생겨 오히려 어려워진다.
  * 그래서 클래스 수는 늘리지 않고, 파일만 기능별로 나눴다. → MainWindow의 호출 코드도 바뀌지 않음.
* C++/CLI는 partial class를 지원하지 않으므로 **진짜 클래스로 나눈다**.
  각 클래스는 서로를 호출하지 않고 독립적이라 나누기 쉽다.

### 결과

**ImageProcessing.Ui** — 클래스 1개 (MainWindow, 파일 5개)

| 파일 | 담당 |
|---|---|
| MainWindow.xaml.cs | 멤버 변수, 생성자, BMP 열기/저장, 이미지 열림 검사 |
| MainWindow.Roi.cs | ROI 드래그 선택/취소, 좌표 변환, Navigator ROI 표시 |
| MainWindow.Histogram.cs | 히스토그램 계산 요청/그리기, 채널(Color/R/G/B) 선택 |
| MainWindow.Processing.cs | 이진화/평활화/팽창/수축/가우시안/라플라시안/소벨 버튼, 처리 후 화면 갱신 |
| MainWindow.TemplateMatch.cs | 템플릿 등록/Preview, DIFF/CORR/COEFF 버튼 |

**ImageProcessing.Core** — 클래스 3개 (BmpFileHandler, ChannelMode, ProcessingService)

| 파일 | 담당 |
|---|---|
| BmpFileHandler.cs | (변경 없음) |
| ChannelMode.cs | (변경 없음) |
| ProcessingService.cs | 영상 준비 및 결과 상태 관리 (속성, 24bpp 복사, LockBits, 채널 추출, 결과 교체) |
| ProcessingService.Basic.cs | 기본 영상처리 (히스토그램, 이진화, 평활화, 팽창, 수축) |
| ProcessingService.Filter.cs | 필터 (가우시안, 라플라시안, 소벨) |
| ProcessingService.TemplateMatch.cs | 템플릿 매칭 (DIFF, CORR, COEFF + 결과 저장/사각형) |

**Native** — C#에서 쓰는 클래스 4개 + 내부 보조 클래스 1개

| 파일 | 클래스 | 담당 |
|---|---|---|
| PixelProcessor.h | `PixelProcessor` | 기본 픽셀 처리 (히스토그램, 채널 추출, 이진화, 평활화) |
| MorphologyProcessor.h | `MorphologyProcessor` | 형태학 처리 (팽창, 수축) |
| FilterProcessor.h | `FilterProcessor` | 필터 (가우시안, 라플라시안, 소벨) |
| TemplateMatchProcessor.h | `TemplateMatchProcessor` | 템플릿 매칭 + 매칭 보조 (평균 계산, 결과 배열) |
| PixelHelper.h | `PixelHelper` | 공통 보조 함수 (Clamp, ClampByte, GetChannelValue) — C++ 내부 전용 |
| ImageProcessing.h | (없음) | 위 헤더들을 한 번에 include (기존 .cpp가 그대로 동작하도록) |

> Native는 5개이지만, `PixelHelper` 는 static 함수 3개(40줄)뿐인 도우미이고 C#에서는 보이지 않는다.
> 형태학(팽창/수축)을 필터에 합치면 FilterProcessor가 290줄이 되어 따로 두었다.

---

## 3. 변경된 파일 목록

| 상태 | 파일 |
|---|---|
| 수정 | `ImageProcessing.Ui/MainWindow.xaml.cs` |
| **신규** | `ImageProcessing.Ui/MainWindow.Roi.cs` |
| **신규** | `ImageProcessing.Ui/MainWindow.Histogram.cs` |
| **신규** | `ImageProcessing.Ui/MainWindow.Processing.cs` |
| **신규** | `ImageProcessing.Ui/MainWindow.TemplateMatch.cs` |
| 수정 | `ImageProcessing.Core/ProcessingService.cs` |
| **신규** | `ImageProcessing.Core/ProcessingService.Basic.cs` |
| **신규** | `ImageProcessing.Core/ProcessingService.Filter.cs` |
| **신규** | `ImageProcessing.Core/ProcessingService.TemplateMatch.cs` |
| 변경 없음 | `ImageProcessing.Core/BmpFileHandler.cs`, `ChannelMode.cs` |
| 수정 | `Native/ImageProcessing.h` (include 모음으로 변경) |
| **신규** | `Native/PixelHelper.h`, `PixelProcessor.h`, `MorphologyProcessor.h`, `FilterProcessor.h`, `TemplateMatchProcessor.h` |
| 수정 | `ImageProcessing.Ui/MainWindow.xaml` (x:Name 짧게 변경, 구역 주석 추가 — 아래 3-1 참고) |

### 3-1. MainWindow.xaml x:Name 변경표

규칙 : **종류 약자 + 대상**
(`Txt` = TextBlock, `Rb` = RadioButton, `Sld` = Slider, `Cmb` = ComboBox, `Row` = RowDefinition, `Nav` = Navigator)

| 기존 이름 | 새 이름 | 컨트롤 |
|---|---|---|
| ImageViewer1 / ImageViewer2 | `Viewer1` / `Viewer2` | Image |
| RoiRectangle | `RoiRect` | Rectangle |
| RoiCanvas | `RoiCanvas` (그대로) | Canvas |
| NavigatorViewer | `NavViewer` | Image |
| NavigatorCanvas | `NavCanvas` | Canvas |
| NavigatorRoiRectangle | `NavRoiRect` | Rectangle |
| HistRowR / G / B | `RowR` / `RowG` / `RowB` | RowDefinition |
| TxtHistR / G / B | `TxtR` / `TxtG` / `TxtB` | TextBlock |
| HistogramRBorder / G / B | `BorderR` / `BorderG` / `BorderB` | Border |
| HistogramRCanvas / G / B | `CanvasR` / `CanvasG` / `CanvasB` | Canvas |
| RbHistOriginal / RbHistResult | `RbOriginal` / `RbResult` | RadioButton |
| TxtHistRoi | `TxtRoi` | TextBlock |
| RbColor, RbR, RbG, RbB | (그대로) | RadioButton |
| TxtProcessingTime | `TxtTime` | TextBlock |
| TemplatePreview | `TemplateView` | Image |
| SliderThreshold / TxtThresholdValue | `SldThreshold` / `TxtThreshold` | Slider / TextBlock |
| SliderSigma / TxtSigmaValue | `SldSigma` / `TxtSigma` | Slider / TextBlock |
| CmbKernelSize | `CmbKernel` | ComboBox |

**이벤트 메서드 이름 변경** (XAML과 C# 모두 수정)

| 기존 | 새 이름 | 이유 |
|---|---|---|
| `BtnLaplacian_Click_` | `BtnLaplacian_Click` | 끝의 `_` 는 오타 |
| `SliderThreshold_ValueChanged` | `SldThreshold_ValueChanged` | "컨트롤이름_이벤트" 규칙에 맞춤 |
| `SliderSigma_ValueChanged` | `SldSigma_ValueChanged` | 위와 같음 |

**삭제한 것** (코드 어디에서도 쓰이지 않던 것)

* `TxtTemplateMode` TextBlock : 항상 `Collapsed` 이고 코드에서 한 번도 보이게 하지 않음.
  Color 모드가 아닐 때의 안내는 `CanRunTemplateMatching()` 의 MessageBox가 하고 있다.
* `BtnDiff`, `BtnCorr`, `BtnCoeff` 의 x:Name : Click 이벤트만 쓰고 이름으로 접근하는 코드가 없음 (버튼은 그대로 있음).
* 영상처리 탭의 `Cursor=""` : 값이 비어 있어 아무 효과가 없는 속성.

> 주의 : `Style="{StaticResource ButtonSmall}"` 등 스타일은 App.xaml에 정의되어 있으므로 그대로 두었다.
> Visual Studio에서 이름이 바뀐 뒤 빌드 오류가 나면 *빌드 → 솔루션 다시 빌드* 로
> 자동 생성 파일(MainWindow.g.cs)을 새로 만들면 된다.

### Visual Studio에 적용하는 방법

1. 각 파일을 실제 프로젝트 폴더에 복사(덮어쓰기)한다.
2. **C# 프로젝트** : 솔루션 탐색기에 새 파일이 안 보이면 프로젝트 우클릭 → *추가 → 기존 항목* 으로 새 `.cs` 파일을 추가한다.
   (SDK 스타일 프로젝트(.NET 5 이상)는 자동으로 포함된다.)
3. **C++/CLI 프로젝트** : 새 `.h` 파일들을 *추가 → 기존 항목* 으로 추가한다.
   기존 `.cpp` 에 있는 `#include "ImageProcessing.h"` 는 그대로 두면 된다.
4. 원래 `ImageProcessor` 라는 클래스 이름은 없어졌으므로, 다른 곳에서 `new ImageProcessor()` 를 쓰고 있다면 아래 표의 새 클래스로 바꾼다.
   (첨부된 파일 기준으로는 ProcessingService 외에 사용하는 곳이 없음)

---

## 4. 기존 코드가 어디로 이동했는지

### MainWindow.xaml.cs (583줄) →

| 기존 멤버 | 새 위치 | 변경 내용 |
|---|---|---|
| 필드, 생성자, `Window_Loaded`, `BtnOpen_Click`, `BtnSave_Click` | MainWindow.xaml.cs | `Rectangle? currentRoi` → `bool hasRoi` + `Rectangle currentRoi` |
| (신규) `CheckImageOpened` | MainWindow.xaml.cs | 7개 버튼에 반복되던 "먼저 BMP를 열어주세요" 검사를 한 곳으로 |
| `RoiCanvas_MouseLeftButtonDown/Move/Up`, `CanvasPointToImagePixel`, `UpdateNavigatorRoi`, `BtnRoiCancel_Click` | MainWindow.Roi.cs | `UpdateNavigatorRoi(roi)` → 매개변수 없이 필드 사용 |
| (신규) `ClearRoi` | MainWindow.Roi.cs | 열기/취소/빈 드래그에서 반복되던 ROI 초기화 3줄을 한 곳으로 |
| `UpdateHistogram`, `DrawHistograms`, `DrawHistogramBoxes`, `UpdateHistogramLayout`, `SetHistogramRow`, `HistogramSource_Checked`, `HistogramCanvas_SizeChanged`, `ChannelMode_Checked`, `ApplySelectedChannel` | MainWindow.Histogram.cs | 삼항 연산자 → if/else |
| `OnProcessingCompleted`, 슬라이더 2개, 이진화~소벨 버튼 7개 | MainWindow.Processing.cs | `CheckImageOpened()` 사용 |
| `BtnRegisterTemplate_Click`, `TryPrepareTemplateMatching`, `BtnDiff/Corr/Coeff_Click` | MainWindow.TemplateMatch.cs | `TryPrepareTemplateMatching` → `CanRunTemplateMatching` (이름만) |

### ProcessingService.cs (330줄) →

| 기존 멤버 | 새 위치 | 변경 내용 |
|---|---|---|
| 속성들, `CopyTo24bpp`, `Lock24`, `ClearChannel`, `ExtractChannel`, `WorkingBitmap`, `CopyProcessingSource`, `ClearResult`, `ReplaceResult` | ProcessingService.cs | `WorkingBitmap` → `GetWorkingBitmap`, `ReferenceEquals` → `!=` |
| `CalculateHistogram`, `Binarize`, `EqualizeHistogram`, `Dilation`, `Erosion` | ProcessingService.Basic.cs | `Stopwatch.StartNew()` → `new Stopwatch(); Start();` |
| `RunFilter(Action)` + `Gaussian`, `Laplacian`, `Sobel` | ProcessingService.Filter.cs | **Action/람다 제거**, 각 메서드에 흐름을 직접 작성 |
| `RunTemplateMatch(Func)` + `TemplateMatchDiff/Corr/Coeff` | ProcessingService.TemplateMatch.cs | **Func/람다 제거**, 공통 뒷처리는 `SaveMatchResult` 일반 메서드로 |

### ImageProcessing.h `ImageProcessor` (665줄) →

| 기존 함수 | 새 클래스 |
|---|---|
| `CalculateHistogram`, `ExtractChannel`, `Binarize`, `EqualizeHistogram`, `PickChannel`, `BuildEqualizeMap` | `PixelProcessor` |
| `Dilation`, `Erosion` | `MorphologyProcessor` (세로 패스 인덱스 버그 수정) |
| `Gaussian`, `Laplacian`, `Sobel`, `GenerateGaussianKernel1D`, `ConvolveChannel` | `FilterProcessor` |
| `TemplateMatchDiff/Corr/Coeff`, `CalculateChannelMeans` | `TemplateMatchProcessor` (+ 결과 배열 만드는 `MakeResult`) |
| `Clamp`, `ClampByte`, `GetChannelValue` | `PixelHelper` (static) |

템플릿 매칭의 `int channels[3] = {0,1,2}; int c = channels[ci];` 는 결국 `c = ci` 와 같으므로
`for (int c = 0; c < 3; c++)` 로 바꿨다. **계산 결과는 동일**하다.

---

## 5. 최종 프로그램 구조

```
전체 프로그램 구조
│
├─ [ImageProcessing.Ui]  MainWindow (partial, 파일 5개)
│   ├─ MainWindow.xaml.cs          UI 기본 / 파일 열기·저장
│   ├─ MainWindow.Roi.cs           ROI / Navigator
│   ├─ MainWindow.Histogram.cs     Histogram UI / 채널 UI
│   ├─ MainWindow.Processing.cs    영상처리·필터 버튼 / 처리 후 공통 UI 갱신
│   └─ MainWindow.TemplateMatch.cs 템플릿 등록 / 템플릿 매칭 버튼
│
│   ↓ 호출
│
├─ [ImageProcessing.Core]
│   ├─ BmpFileHandler              BMP 열기·저장, 템플릿 잘라내기, 화면용 변환
│   ├─ ChannelMode                 Color / R / G / B
│   └─ ProcessingService (partial, 파일 4개)
│       ├─ ProcessingService.cs               영상 준비 / 상태 관리 / 처리 입력 선택
│       ├─ ProcessingService.Basic.cs         기본 영상처리
│       ├─ ProcessingService.Filter.cs        필터
│       └─ ProcessingService.TemplateMatch.cs 템플릿 매칭
│
│   ↓ 실제 픽셀 처리 위임 (Scan0, Width, Height, Stride 전달)
│
└─ [Native]  (C++/CLI)
    ├─ PixelProcessor          기본 픽셀 처리
    ├─ MorphologyProcessor     형태학 처리
    ├─ FilterProcessor         필터
    ├─ TemplateMatchProcessor  템플릿 매칭 + 매칭 보조
    └─ PixelHelper             공통 보조 함수 (C++ 내부 전용)
```

---

## 6. 클래스별 설명

### 6-1. MainWindow (ImageProcessing.Ui)

**역할** : 사용자가 보는 화면. 버튼 클릭, 마우스 드래그 같은 입력을 받아서
`BmpFileHandler` 와 `ProcessingService` 에게 일을 시키고, 결과를 화면에 보여준다.
**직접 픽셀을 계산하지 않는다.**

**주요 멤버 변수**

| 변수 | 의미 |
|---|---|
| `bmpFileHandler` | 원본 Bitmap, 템플릿 Bitmap을 가지고 있는 객체 |
| `processingService` | 처리 결과, 채널 영상, 히스토그램, 처리 시간을 가지고 있는 객체 |
| `roiStartPoint` | 드래그를 시작한 화면 좌표 |
| `isRoiDragging` | 드래그 중이면 true |
| `hasRoi` | ROI가 선택되어 있으면 true |
| `currentRoi` | 선택된 ROI (이미지 픽셀 좌표) |

**주요 메서드**

| 메서드 | 파일 | 하는 일 |
|---|---|---|
| `BtnOpen_Click` | xaml.cs | 파일 선택 → 원본 열기 → Viewer1/Navigator 표시 → ROI·채널 초기화 |
| `BtnSave_Click` | xaml.cs | ResultBitmap을 BMP로 저장 |
| `CheckImageOpened` | xaml.cs | 이미지가 없으면 메시지 후 false |
| `RoiCanvas_Mouse...` | Roi.cs | 드래그로 ROI 사각형 그리기, 놓으면 이미지 좌표로 변환해 저장 |
| `CanvasPointToImagePixel` | Roi.cs | 화면 좌표 → 이미지 픽셀 좌표 |
| `UpdateNavigatorRoi` | Roi.cs | 이미지 좌표 → 네비게이터 좌표로 바꿔 빨간 사각형 표시 |
| `UpdateHistogram` | Histogram.cs | 어떤 영상(원본/채널/결과)의 어떤 영역(ROI/전체)인지 정해서 계산 요청 후 그리기 |
| `DrawHistogramBoxes` | Histogram.cs | 256개 막대를 Canvas에 그림 |
| `ApplySelectedChannel` | Histogram.cs | 채널이 바뀌면 결과 초기화, R/G/B면 채널 영상을 만들어 Viewer2에 표시 |
| `OnProcessingCompleted` | Processing.cs | 처리 후 Viewer2·처리시간·히스토그램 갱신 |
| `CanRunTemplateMatching` | TemplateMatch.cs | Color 모드? 이미지? 템플릿? 크기? 검사 |

**연결** : MainWindow → BmpFileHandler, ProcessingService (반대 방향 호출은 없음)

### 6-2. BmpFileHandler (Core, 변경 없음)

**역할** : 파일과 관련된 일.
* `OriginalBitmap`, `TemplateBitmap` 보관
* `Open` : 파일 → Bitmap → 화면용 BitmapSource
* `Save` : Bitmap → BMP 파일
* `RegisterTemplate` : 원본에서 ROI 부분을 `Clone` 해서 템플릿으로 저장
* `ToBitmapSource` : System.Drawing.Bitmap(GDI+)은 WPF Image 컨트롤에 바로 넣을 수 없어서
  MemoryStream에 BMP로 저장한 뒤 BitmapImage로 다시 읽는다.

### 6-3. ProcessingService (Core)

**역할** : MainWindow와 C++ 사이의 **중간 관리자**.
* 어떤 영상을 처리할지 고른다 (원본 / 채널 영상 / 이전 결과)
* 24bpp 복사, LockBits로 픽셀 주소를 얻는다
* C++ 클래스를 호출하고 시간을 잰다
* 결과를 `ResultBitmap` 에 보관한다

**주요 멤버 변수(속성)**

| 속성 | 의미 |
|---|---|
| `ResultBitmap` | 마지막 처리 결과 |
| `ChannelBitmap` | R/G/B 모드일 때 채널만 뽑은 흑백 영상 |
| `CurrentChannelMode` | 현재 채널 |
| `RHistogram/GHistogram/BHistogram` | 히스토그램 (길이 256 배열) |
| `LastProcessingTimeMs` | 처리 시간 |
| `BestMatchX/Y/Score`, `TemplateMatchWidth/Height` | 템플릿 매칭 결과 |

**핵심 private 메서드**

| 메서드 | 하는 일 |
|---|---|
| `CopyTo24bpp` | 어떤 형식이든 "픽셀 1개 = 3바이트(B,G,R)" 형식으로 복사 |
| `Lock24` | Bitmap 전체를 LockBits → `BitmapData`(Scan0, Stride 포함) 반환 |
| `GetWorkingBitmap` | Color면 원본, R/G/B면 채널 영상 |
| `CopyProcessingSource` | **이전 결과가 있으면 결과를, 없으면 작업 영상을** 복사해서 반환 → "결과를 이어서 다시 처리" 기능 |
| `ReplaceResult` | 새 결과를 저장하고 이전 결과는 Dispose |
| `SaveMatchResult` | 매칭 좌표 저장 + 빨간 사각형 그리기 |

**처리 방식 2가지**

1. **그 자리에서 바꾸기 (in-place)** : 이진화, 평활화
   → 픽셀 하나를 바꿀 때 다른 픽셀을 볼 필요가 없어서 Bitmap 하나로 충분
2. **source 읽기 → result 쓰기** : 팽창, 수축, 가우시안, 라플라시안, 소벨
   → 주변 픽셀을 봐야 하는데, 같은 Bitmap에 쓰면 이미 바뀐 값을 읽게 되므로 두 개가 필요

### 6-4. C++/CLI 클래스들 (Native)

* `public ref class` : .NET(C#)에서 `new` 로 만들 수 있는 클래스. C#의 class와 같다고 생각하면 된다.
* `IntPtr` : C#에서 넘어온 주소 값. `scan0.ToPointer()` 로 C++ 포인터(`unsigned char*`)로 바꿔서 사용.
* `array<int>^` : C#의 `int[]` 와 같은 배열. `^` 는 ".NET이 관리하는 객체"라는 뜻.
* `gcnew` : C#의 `new` 와 같다 (.NET 객체 생성).

| 클래스 | 주요 메서드 | 특징 |
|---|---|---|
| `PixelProcessor` | CalculateHistogram, ExtractChannel, Binarize, EqualizeHistogram | 픽셀 하나씩 처리 |
| `MorphologyProcessor` | Dilation, Erosion | 가로 1x3 → 세로 3x1 두 번 (결과는 3x3과 같음) |
| `FilterProcessor` | Gaussian, Laplacian, Sobel | 커널 곱의 합(컨벌루션) |
| `TemplateMatchProcessor` | TemplateMatchDiff/Corr/Coeff | `double[3]` = {X, Y, 점수} 반환 |
| `PixelHelper` | Clamp, ClampByte, GetChannelValue | 일반 C++ class라 C#에서는 안 보임 |

---

## 7. 버튼을 눌렀을 때 실행 순서

### 예 1) 가우시안 버튼

```
[MainWindow.Processing.cs] BtnGaussian_Click
 ├ CheckImageOpened()                 이미지 없으면 종료
 ├ kernelSize, sigma 읽기             콤보박스/슬라이더
 ├ processingService.Gaussian(원본, kernelSize, sigma)
 │   [ProcessingService.Filter.cs]
 │   ├ CopyProcessingSource(원본)     이전 결과 or 채널 or 원본을 24bpp 복사 → source
 │   ├ CopyTo24bpp(source)            → result (같은 크기의 빈 그릇 역할)
 │   ├ Lock24(source), Lock24(result) 픽셀 메모리 주소(Scan0), Stride 얻기
 │   ├ stopwatch.Start()
 │   ├ new FilterProcessor().Gaussian(srcScan0, dstScan0, W, H, Stride, k, σ)
 │   │   [FilterProcessor.h]  C++ 포인터로 직접 픽셀 계산 → result 메모리에 기록
 │   ├ stopwatch.Stop()
 │   ├ UnlockBits 2번, source.Dispose()
 │   ├ LastProcessingTimeMs 저장
 │   └ ReplaceResult(result)          이전 결과 해제, ResultBitmap = result
 └ OnProcessingCompleted()
     ├ Viewer2.Source = ToBitmapSource(ResultBitmap)
     ├ TxtTime.Text = "xx ms"
     ├ RbResult 활성화 + 선택
     └ UpdateHistogram()  → ResultBitmap(+ROI)로 히스토그램 계산 → DrawHistograms()
```

### 예 2) 템플릿 매칭 COEFF 버튼

```
(먼저) ROI 드래그 → BtnRegisterTemplate_Click → TemplateBitmap 저장 + Preview 표시

BtnCoeff_Click
 ├ CanRunTemplateMatching()            Color 모드 / 이미지 / 템플릿 / 크기 검사
 ├ Viewer2.Source = null
 ├ processingService.TemplateMatchCoeff(원본, 템플릿)
 │   ├ image = CopyProcessingSource, template = CopyTo24bpp
 │   ├ LockBits 2번 → TemplateMatchProcessor.TemplateMatchCoeff(...) → double[3]
 │   ├ UnlockBits, template.Dispose()
 │   └ SaveMatchResult → BestMatchX/Y 저장, image에 빨간 사각형, ReplaceResult
 └ OnProcessingCompleted()
```

### 예 3) R 채널 라디오 버튼

```
ChannelMode_Checked → CurrentChannelMode = R → ApplySelectedChannel
 ├ UpdateHistogramLayout()            R 줄만 보이게
 ├ 결과 초기화 (ClearResult, 처리시간 0 ms, 히스토그램 "원본" 선택)
 ├ processingService.ExtractChannel(원본) → PixelProcessor.ExtractChannel → ChannelBitmap
 ├ Viewer2 에 ChannelBitmap 표시
 └ UpdateHistogram()                  ChannelBitmap 기준 히스토그램
이후 처리 버튼을 누르면 GetWorkingBitmap 이 ChannelBitmap 을 돌려줘서 채널 영상이 처리된다.
```

### "처리 결과를 이어서 다시 처리" 원리

`CopyProcessingSource` 가 `ResultBitmap` 이 있으면 **그것을 복사해서 입력으로** 쓴다.
그래서 가우시안 → 소벨 순서로 누르면 "가우시안 결과에 소벨"이 적용된다.
새 파일을 열거나 채널을 바꾸면 `ClearResult()` 로 결과가 지워져서 처음부터 다시 시작한다.

**템플릿 매칭 결과는 예외** : 매칭 결과(`ResultBitmap`)에는 빨간 사각형이 픽셀로 그려져 있다.
이것을 그대로 다음 입력으로 쓰면 DIFF → CORR 순서로 눌렀을 때 사각형이 두 개 남고,
이전 사각형의 빨간 픽셀이 CORR 점수 계산에도 섞여 버린다.
그래서 `SaveMatchResult` 는 사각형을 그리기 **전** 영상을 `resultWithoutBox` 에 따로 보관하고,
`CopyProcessingSource` 는 이 값이 있으면 사각형 없는 영상으로 이어서 처리한다.

| 순서 | 다음 처리의 입력 | 화면(Viewer2) |
|---|---|---|
| DIFF | 원본 | 원본 + DIFF 사각형 |
| → CORR | 원본 (사각형 없음) | 원본 + CORR 사각형 1개만 |
| 가우시안 → DIFF → 소벨 | 가우시안 결과 (사각형 없음) | 가우시안 + 소벨 결과 |

다른 처리가 실행되면 `ReplaceResult` 가 `resultWithoutBox` 를 지운다. (새 결과에는 사각형이 없으므로)

---

## 8. Bitmap이 C#에서 C++/CLI까지 전달되는 과정

```
C#  Bitmap (GDI+ 객체, 픽셀은 GDI+가 관리하는 메모리 어딘가에 있음)
 │  CopyTo24bpp  → 픽셀 형식을 24bpp(B,G,R 3바이트)로 통일
 │  LockBits     → 픽셀 메모리를 "고정"하고 주소를 알려달라고 요청
 ▼
BitmapData { Scan0 = 시작 주소(IntPtr), Stride = 한 줄 바이트 수, Width, Height }
 │  processor.Gaussian(srcData.Scan0, dstData.Scan0, Width, Height, Stride, ...)
 ▼
C++/CLI  IntPtr scan0 → (unsigned char*)scan0.ToPointer()
 │  pixels[y * stride + x * 3 + c] 로 직접 읽고 쓰기   (c: 0=B, 1=G, 2=R)
 ▼
C#  UnlockBits → 바뀐 픽셀이 Bitmap에 반영됨 → ResultBitmap
```

**픽셀을 복사해서 보내는 게 아니라 "주소"만 보낸다.**
그래서 C++이 그 주소에 쓴 값이 곧바로 C#의 Bitmap 내용이 된다. (빠른 이유)

히스토그램은 반대로 C#에서 `new int[256]` 배열을 만들어 넘기면,
C++이 그 배열(`array<int>^`)에 값을 채우고, C#은 같은 배열을 그대로 읽는다.

템플릿 매칭은 C++이 `gcnew array<double>(3)` 로 만든 배열을 return 하고, C#에서는 `double[]` 로 받는다.

---

## 9. LockBits, Scan0, Stride를 쓰는 이유

### LockBits
* `bitmap.GetPixel(x, y)` 는 한 픽셀마다 함수 호출 + 검사를 해서 **매우 느리다**.
* `LockBits` 는 "지금부터 이 Bitmap의 픽셀 메모리를 옮기지 말고 주소를 알려줘"라는 요청이다.
* 다 쓰고 나면 반드시 `UnlockBits` 를 호출해야 한다. (안 하면 Bitmap을 다시 쓸 수 없다)
* `ImageLockMode.ReadOnly` / `ReadWrite` : 읽기만 할지, 쓰기도 할지

### Scan0
* 픽셀 메모리의 **첫 번째 바이트 주소** (왼쪽 위 픽셀의 B 값 위치)

### Stride
* **한 줄(가로 한 줄)이 차지하는 바이트 수**
* BMP는 한 줄의 길이를 **4의 배수**로 맞추기 때문에 `width * 3` 보다 클 수 있다.

```
width = 5 인 24bpp 이미지
 한 줄 실제 데이터 = 5 * 3 = 15 바이트
 Stride            = 16 바이트 (4의 배수로 맞추기 위해 1바이트 여백)

 [B G R][B G R][B G R][B G R][B G R][여백]   ← y = 0 (Scan0 부터 16바이트)
 [B G R][B G R][B G R][B G R][B G R][여백]   ← y = 1 (Scan0 + 16 부터)
```

그래서 (x, y) 픽셀의 위치는 반드시 `y * stride + x * 3` 로 계산해야 한다.
`y * width * 3` 으로 계산하면 가로가 4의 배수가 아닌 이미지에서 줄이 어긋난다.
(기존 팽창/수축 버그가 바로 이 stride와 width*3을 섞어 쓴 문제였다.)

---

## 10. 처리 결과가 Viewer2와 Histogram에 표시되는 과정

```
ProcessingService.ResultBitmap (System.Drawing.Bitmap)
 │
 ├─ OnProcessingCompleted
 │   ├ bmpFileHandler.ToBitmapSource(ResultBitmap)
 │   │   Bitmap → MemoryStream(BMP) → BitmapImage (WPF용) → Freeze
 │   └ Viewer2.Source = 위 결과
 │
 └─ UpdateHistogram
     ├ RbResult 선택됨 → baseBitmap = ResultBitmap
     ├ ROI 있으면 baseBitmap.Clone(roi) 로 잘라내기
     ├ processingService.CalculateHistogram(target)
     │   → 24bpp 복사 → LockBits → PixelProcessor.CalculateHistogram → R/G/B 배열 채움
     └ DrawHistograms → DrawHistogramBoxes × 3
         256칸마다 Rectangle 막대를 Canvas에 추가 (가장 큰 값 = Canvas 높이)
```

---

## 11. 발표용 한 줄 요약

* **MainWindow** : "무엇을 할지" 결정하고 화면에 보여준다.
* **ProcessingService** : "어떤 영상으로" 처리할지 준비하고 결과를 보관한다.
* **Native Processor** : "어떻게" 픽셀을 계산할지 담당한다.
* 연결 고리 : **LockBits로 얻은 Scan0(주소) + Stride(한 줄 크기)** 를 C++에 넘긴다.
