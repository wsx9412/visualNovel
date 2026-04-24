# Reincarnation Log 기본 시스템

## 포함된 구성
- `ReincarnationGameManager`: 이벤트 루프, 성공/실패 처리, 사망/부활, 엔딩/환생 포인트 계산.
- `EventResolver`: 스탯/성향/난이도 기반 성공 확률 계산 및 결과 반영.
- `LegacyShop`: 환생 포인트 업그레이드 처리.
- `SaveService`: 런 상태 + 메타 상태 저장/로드.
- `events.json`: 샘플 이벤트 3종.

## Unity 연결 방법
1. 빈 GameObject를 만들고 `ReincarnationGameManager`를 붙입니다.
2. `eventJson` 슬롯을 비워두면 `Resources/events.json`을 자동 로드합니다.
3. UI 버튼에서 `ChooseOption(index)`를 호출해 선택지 처리.
4. 로그 UI는 `OnLog` 이벤트를 구독해 출력.
5. 선택지 목록 UI는 `OnEventReady(event, options)` 이벤트를 구독해 렌더링.

## AdMob 연동 포인트
- 현재는 `DebugAdReviveService`가 더미 광고 성공 콜백을 반환합니다.
- 실제 SDK 적용 시 `IAdReviveService` 구현체를 만들어
  전면 광고 닫힘 콜백에서 `onClosed(true/false)`를 호출하면 됩니다.
