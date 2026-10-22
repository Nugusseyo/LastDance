# JYG 작업 폴더

UI, 재화·평판 데이터, 상점(업그레이드), 자판기, 저장 시스템을 담당하는 폴더입니다.
네임스페이스 루트는 `_Works.JYG._Scripts` 입니다. (예외는 아래 "주의할 점" 참고)

## 폴더 구조

```
JYG/
├─ _Scripts/          스크립트 (아래 상세)
├─ Data/              SO 에셋 (데이터 컨테이너, 자판기 아이템 등)
├─ GameModule/        매니저 프리팹/에셋
├─ Graphics/ Fonts/ Sound/
├─ JYG.unity          작업용 씬
└─ Demo_COPY.unity    데모 씬 복사본
```

외부 의존성: `DevLib.EventChannelSystem`(EventChannelSO / GameEvent), `DevLib.ObjectPool`, DOTween, TextMeshPro, New Input System,
`Resources/DataBase/*`(ReviewDB, HumanDB, UpgradeDB — 엑셀에서 생성된 SO).

---

## 스크립트 개요

### Data Container — 값 + 변경 이벤트를 담는 SO
| 파일 | 역할 |
|---|---|
| `IDataContainer.cs` | `IDataContainer<T>`(Value, `OnValueChanged(new, old)`) / 비제네릭 `IDataContainer`(RawValue, `OnRawValueChanged`) |
| `Money/IntegerDataContainer.cs` | int 값 SO. min/max 클램프, `ISavableData` 구현(PlayerPrefs JSON). **돈·평판**이 이 에셋 |
| `Money/MoneyManager.cs` | 주유/폐차/지갑 이벤트를 받아 돈을 계산해 더함 (공식은 코드 주석 참고) |
| `Store/StoreValueContainer.cs` | float 값 SO. 업그레이드 블록마다 `Instantiate`해서 사용 |
| `DataController.cs` | 빈 클래스 (미사용) |

### SaveSystem / GameModule
| 파일 | 역할 |
|---|---|
| `SaveSystem/DataSaveSystem.cs` | `PlayerPrefs` + `JsonUtility` 래퍼 (`GetSaveData<T>`, `SetSaveData<T>`) |
| `SaveSystem/ISavableData.cs` | `InitializeData(key)` / `SaveData(key)` |
| `GameModule/GameManager.cs` | `initDatas`(SO + key) 목록을 Start에서 로드, OnDisable에서 저장 |
| `GameModule/ReviewManager.cs` | 평판 처리. 초당 `increasePercentage` 자동 증가, `ReviewEvent`로 ±, 콜라(2배)·사이다(감소 차단) 버프 |
| `GasStation/GasStationManager.cs` | 업그레이드 인덱스 5 → `MapEvents.GasStationEvent`로 주유기 개수 전달 |

### Store (업그레이드 상점) — `UI/StoreUI`
| 파일 | 역할 |
|---|---|
| `Store.cs` | `UpgradeDB`로 블록 생성, 구매 처리, `persistentDataPath/storeData.json` 저장/로드. `StoreItem`, `UpgradeValue` 구조체 정의 |
| `UpgradeBlock.cs` | 상품 한 칸 UI. 레벨 바, 가격 색상(구매 가능/불가/MAX), `GetUpgradeStoreItem()`으로 다음 레벨 미리 계산 |
| `UpgradeBarInitializer.cs` | 레벨 칸 생성 및 색 변경 |

업그레이드 인덱스 (코드에 하드코딩됨):

| index | 사용처 | 의미 |
|---|---|---|
| 2 | MoneyManager | 빠른 폐차 |
| 3 | MoneyManager | 주유 보너스 |
| 4 | MoneyManager | 폐차 보너스 |
| 5 | GasStationManager | 주유기 개수 |

### Events
| 파일 | 내용 |
|---|---|
| `Events/UIEvents.cs` | 정적 이벤트 인스턴스 모음 + 이벤트 클래스, `BuffType` enum |
| `Events/UpgradeEvent.cs` | `UpgradeItem(index, StoreItem)` — 업그레이드 채널 전용 |

| 이벤트 | 발행처 (외부 포함) | 구독처 |
|---|---|---|
| `GaugeEvent` | JJH `PlayerMover`, GaugeDebugger | `GaugeUI` |
| `DurationEvent` | KDH `WheelDetacher`, `PartDetacher` / JJH `FuelInjector` | `DurationUI` |
| `ReviewEvent` | CJW `VisitReputationReporter` | `ReviewManager` |
| `ReviewBlockEvent` | KDH `CiderEffect` | `ReviewManager` |
| `ReviewBuffEvent` | KDH `ColaEffect` | `ReviewManager` |
| `RefuelingEvent` | CJW `VisitFuelPaymentReporter` | `MoneyManager` |
| `ScrapEvent` | KDH `GarageSender`, `SellCounter` | `MoneyManager` |
| `WalletEvent` | KDH `WalletEffect` | `MoneyManager` |
| `BuffEvent` | ReviewManager, KDH `WalletEffect` | `BuffController` (핸들러 비어 있음) |
| `MessageEvent` | Store, VendingItem | `MessageController` |
| `TooltipEvent` / `TipMoveEvent` | `VendingTooltip` | `VendingTooltipViewer` |
| `KeyHintEvent` (필드명 `UIEvents.KeyHintType`) | `KeyHintLinker` | `KeyHint` |

> 이벤트 인스턴스는 static 싱글턴을 재사용하므로, `Init()` 후 바로 `RaiseEvent` 해야 합니다.

### UI
| 파일 | 역할 |
|---|---|
| `UIDataLinker.cs` | `IDataContainer` ↔ TMP 텍스트 자동 연결 (`TextConvert`로 K/M/B 표기) |
| `PanelUI.cs` | CanvasGroup 페이드 열기/닫기, 열릴 때 UI 입력 ON / 플레이어 입력 OFF, ESC로 닫기 |
| `GaugeUI.cs` | `GaugeEvent` 값으로 fillAmount, 1이면 자동 페이드아웃 |
| `Center/Duration/DurationUI.cs` | 작업 진행 시간 원형 게이지 + 초 표시 |
| `GuestUI/WorldGauge.cs` | 손님 머리 위 게이지 (Gradient 색, 1이면 캔버스 숨김). CJW `CustomerGaugeModule`에서 사용 |
| `BillboardUI.cs` | 월드 캔버스가 카메라를 바라보게 함 |
| `Data/Money/MoneyEffector.cs`, `MoneyTextViewer.cs` | 돈 변화량 +/− 텍스트 연출 (UnityEngine.Pool) |
| `Data/Review/ReviewEffector.cs`, `ReviewItem.cs` | 리뷰 팝업 슬라이드 연출, ReviewDB에서 이름/내용 랜덤 선택 |
| `Message/MessageController.cs` | 알림 메시지 위로 떠오르며 사라지는 연출 |
| `SpeechBubble/SpeechBubble.cs` | 손님 말풍선 (PoolManagerSO, HumanDB). CJW `CustomerSpeech`/`SpeechState`에서 사용 |
| `KeyHint/KeyHint.cs`, `KeyHintViewer.cs`, `KeyHintItemUI.cs` | 현재 가능한 키 안내 표시. owner별로 `[Flags] KeyHintType`을 합쳐서 표시 |
| `Keyboard Viewer/KeyHintLinker.cs` | UnityEvent에서 호출해 키 힌트 구독/해제 |
| `Keyboard Viewer/KeyboardViewer.cs` | 활성 상태에서 지정 키 입력 시 UI 입력 모드로 전환 + 이벤트 |
| `Setting/GameSetting.cs`, `SettingSO.cs` | 볼륨(AudioMixer dB 변환)·알림 차단·마우스 감도. `SettingSO`는 PlayerPrefs에 Load/Save |
| `Buff/BuffController.cs` | `BuffEvent` 구독만 하고 처리 없음 (작업 예정) |
| `GameExit.cs` | 게임 종료, 세이브 초기화(PlayerPrefs 전체 + storeData.json 삭제) |
| `Scroller.cs` | RawImage UV 스크롤 |

### Vending Machine (자판기)
| 파일 | 역할 |
|---|---|
| `VendingMachine.cs` | `VendingItemData` 목록으로 아이템 UI 생성 (최대 9개) |
| `VendingItem.cs` | 구매 처리 → KDH `VendingEvents.VendingSelectDropEvent` 발행 |
| `VendingItemData.cs` | 아이템 SO (가격, 스프라이트, `VendingItemSO`, 툴팁) |
| `VendingTooltip.cs`, `VendingTooltipData.cs`, `VendingTooltipViewer.cs` | 마우스 오버 툴팁 |

### 기타
| 파일 | 역할 |
|---|---|
| `Input/UIInputSO.cs` | `Controls.UI` 액션맵 래퍼 (ESC 이벤트, Enable/Disable). 네임스페이스 없음 |
| `InteractableObj/InteractableObject.cs` | OverlapBox로 플레이어 진입/이탈 시 UnityEvent |
| `OutBox.cs` | 맵 밖으로 나간 플레이어를 스폰 지점으로 이동 |
| `Util/SerializableInterface.cs` | 인스펙터에서 인터페이스를 직렬화하기 위한 래퍼 (`ISerializableInterface` 필요) |
| `Util/TextConvert.cs` | 숫자 → `1.5K`, `2M` 등 축약 표기 |
| `Util/Description.cs` | 설명 텍스트용 추상 SO |
| `Debugging/*` | 테스트용 (Space/Enter로 돈 ±1000, 게이지, Y키 말풍선) |
| `DataBase/Test/*` | 엑셀 임포트 테스트용 |

---

## 저장 데이터 위치
| 데이터 | 저장소 | 키/파일 |
|---|---|---|
| 돈, 평판 | PlayerPrefs (JSON) | `GameManager.initDatas`의 key |
| 상점 레벨 | `Application.persistentDataPath/storeData.json` | Store.cs, GameExit.cs (경로 상수 중복) |
| 설정 | PlayerPrefs | `Setting_*` 키 (SettingSO) |

---

## 주의할 점 / 확인이 필요한 부분
코드를 읽으며 발견한 부분이고, 아직 수정하지 않았습니다.

- **`StoreValueContainer.Value` setter**가 `OnValueChanged(_value, value)` 순서로 호출 → 인터페이스 정의(new, old)와 **반대**입니다.
- **`UpgradeBlock.UpdateUI`**: MAX일 때 `priceTmp.text = "MAX"`로 설정한 직후 가격 텍스트로 덮어씀 → MAX 표시가 안 보입니다.
- **`UpgradeBlock.UpgradeInit`**: `_moneyManager.OnValueChanged +=`를 null 체크 **이전**에 실행합니다.
- **`SettingSO.Save()`를 호출하는 곳이 프로젝트에 없습니다.** (`Load()`는 JJH `PlayerCameraModule`에서 호출) 설정이 PlayerPrefs에 저장되지 않을 수 있습니다.
- **`PanelUI`**: 열 때마다 `OnEscapePressed`를 구독하고 ESC로 닫을 때만 해제 → 버튼으로 닫으면 구독이 쌓일 수 있습니다.
- **`DurationUI`**: `AddListener`만 있고 `OnDestroy`에서 `RemoveListener`가 없습니다.
- **`GameManager.OnDisable`**: 모든 key를 `IntegerDataForJson`으로 가정하고 로그를 찍습니다 (int가 아닌 데이터가 추가되면 문제).
- **`KeyHintType.None = 1 << 0`**: None이 0이 아니라서 Flags 조합 시 None 비트가 섞일 수 있습니다 (Viewer에서 `None`/`0` 둘 다 체크 중).
- `VendingItem`, `VendingMachine`의 네임스페이스가 `_Works.JYG.Data.Vending_Machine`으로 다른 파일과 다릅니다.
- `MoneyManager`의 빠른 폐차 수수료 공식에 "물어봐야 할 듯" 주석이 남아 있습니다.
- `ReviewEffector`는 `MoneyEffector`와 구조가 같아 공통 부모로 합치자는 TODO가 있습니다.
