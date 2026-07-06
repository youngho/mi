# ADR: 자산 배포 방식 — Addressables 채택

## 상태

**채택** (2026-07-06)

## 맥락

README에서 Asset Bundle과 Addressables 용어가 혼용되었습니다. 미션 동적 로드 파이프라인을 하나로 통일해야 합니다.

## 결정

**Unity Addressables**를 미션 번들 배포의 단일 방식으로 채택합니다.

## 근거

| 항목 | Asset Bundle (수동) | Addressables |
|------|---------------------|--------------|
| 의존성 관리 | 직접 구현 | 내장 |
| 원격 CDN | 직접 구현 | `RemoteLoadPath` 설정 |
| 캐시·버전 | 직접 구현 | `Catalog` + hash |
| Unity 통합 | 낮음 | 높음 |

## 구현 규칙

1. 미션 메타데이터 `bundleUrl`은 Addressables **원격 카탈로그 URL** 또는 **번들 직접 URL** 모두 허용
2. Core는 `MissionBundleLoader`로 로드·언로드·버전 검사 수행
3. 외부 제작자 SDK 가이드에 Addressables 빌드 절차 명시
4. README 및 신규 문서에서는 "에셋 번들" 대신 **"Addressables 미션 패키지"** 용어 사용

## 미션 버전 호환

- 메타데이터 `version`은 SemVer
- Core `MissionSDKVersion`과 major 버전이 다르면 로드 거부
