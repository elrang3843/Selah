# Selah — 프로젝트 코딩 규칙

## 시간 표시 형식

프로젝트 내 모든 시간 표시는 **`mm:ss.fff`** 형식을 사용합니다.

- `mm` — 분 (2자리, 0 패딩)
- `ss` — 초 (2자리, 0 패딩)
- `fff` — 밀리초 (3자리, 0 패딩)

**구현 패턴 (C#):**

```csharp
var ts = TimeSpan.FromSeconds(seconds);
return $"{(int)ts.TotalMinutes:D2}:{ts.Seconds:D2}.{ts.Milliseconds:D3}";
```

> **주의:** C# 커스텀 숫자 포맷 문자열에서 `f`는 밀리초 지정자가 아니라 리터럴 문자입니다.
> `TimeSpan`의 컴포넌트를 직접 포맷하거나 `TimeSpan.ToString(@"mm\:ss\.fff")`를 사용하세요.
> `{seconds:000f}` 같은 표현식은 `051f`처럼 잘못된 출력을 생성합니다.
