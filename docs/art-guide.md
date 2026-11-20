# 그림 가이드 (크기·경로·가져오기 설정)

게임에 들어가는 그림을 **몇 픽셀로 그려서 어디에 넣으면 되는지** 정리한 문서다.
그림은 정해진 경로에 정해진 이름으로 넣기만 하면 코드를 고치지 않아도 바로 바뀐다. 그림이 없으면 임시 색 상자가 나온다
(그림을 찾는 코드: `Assets/Scripts/Play/ArtCatalog.cs`).

- 화면 기준 해상도는 **1920 × 1080** 이다. 아래 "화면에 보이는 크기"는 이 기준의 픽셀이다.
- 파일은 **PNG**. 배경이 있는 그림(타일, 화면 일러스트) 말고는 **배경을 투명하게** 한다.
- 경로는 모두 `Assets/Resources/` 아래이고, 코드에는 확장자 없이 적힌다 (`Pixel/objects/crate` → `Pixel/objects/crate.png`).
- 폴더·파일 이름은 **영문**으로 쓰고, 새로 만들 때는 **소문자**를 권한다 (Unity는 대소문자를 가리지 않고 찾지만, 이름을 맞춰 두면 헷갈리지 않는다. 지금 있는 `Portraits/Player`, `Portraits/Cassia`도 그대로 동작한다).

---

## 한눈에 보기

| 종류 | 그릴 크기 (px) | 화면에 보이는 크기 | 경로 | 가져오기 설정 |
| --- | --- | --- | --- | --- |
| 맵 타일·오브젝트 | **64 × 64** (16px 도트 × 4) | 한 칸 약 100~150 | `Pixel/tiles/`, `Pixel/objects/` | 자동 |
| 플레이어 (걷는 동작) | **72 × 128** (18 × 32 × 4), 방향별 GIF | 2칸 높이 | `Art/player/*.gif` → `Pixel/player/` | 자동 |
| 대화 스탠딩 일러스트 | **1200 × 2000** (세로 2000 권장) | 세로 1000 | `Portraits/{인물}/{표정}.png` | [일러스트](#가져오기-설정-일러스트) |
| 시작 화면 배경 | **1920 × 1080** | 화면 전체 | `Illustrations/title.png` | 일러스트 |
| 시작 화면 로고 | **760 × 300 안** (가로로 긴 로고) | 760 × 300 상자 안 | `Illustrations/title_logo.png` | 일러스트 |
| 메인 화면 가운데 그림 | **1120 × 600** (또는 2240 × 1200) | 1120 × 600 | `Illustrations/main.png` | 일러스트 |
| 도감 그림 | **512 × 512** | 344 × 344 | `Guidebook/{이름}.png` | 일러스트 |

---

## 1. 맵 도트 (16px 도트를 4배로)

맵의 **한 칸 = 16 × 16 px 도트**이고, 화질이 떨어지지 않게 **4배(64 × 64 px)로 저장**한다.
화면에서는 카메라가 구간을 확대해 한 칸이 대략 100~150px로 커지고, **도트 느낌 그대로(Point 필터)** 보인다.

- **타일·오브젝트**: 크기와 상관없이 **한 칸에 꼭 맞춰** 그린다 (64 × 64든 16 × 16이든 된다. 정사각형으로 그린다).
- **플레이어**: 한 칸보다 큰 그림. **64px = 한 칸** 기준으로 크기가 정해진다 (72 × 128 = 가로 1.125칸, 세로 2칸).
  발이 서 있는 칸의 아래쪽에 닿고, 머리는 윗칸까지 올라간다.

### 타일 (배경 있음, 칸을 꽉 채움)

| 파일 | 내용 | 임시 그림 |
| --- | --- | --- |
| `Pixel/tiles/floor.png` | 바닥 | 어두운 남색, 테두리 |
| `Pixel/tiles/wall.png` | 벽. **가짜 벽도 이 그림을 쓴다** (찾기 전·후 모두 화면에서는 벽) | 회청색 |

### 오브젝트 (배경 투명, 칸 가운데에 그린다)

| 파일 | 맵 요소 | 메모 |
| --- | --- | --- |
| `Pixel/objects/exit.png` | 출구 | |
| `Pixel/objects/crate.png` | 상자 (밀 수 있음) | |
| `Pixel/objects/breakable.png` | 부서지는 블록 | 내구도가 줄수록 게임이 **어둡게 칠한다** → 밝은 색으로 그린다 |
| `Pixel/objects/key.png` | 열쇠 | ✅ 들어 있음 (64 × 64) |
| `Pixel/objects/door.png` | 잠긴 문 | |
| `Pixel/objects/npc.png` | 인물 (공통) | 인물별 도트가 없을 때 쓴다 |
| `Pixel/npcs/{인물ID}.png` | 인물별 도트 | 예: `Pixel/npcs/cassia.png`. 인물ID는 [4장](#4-인물-id) |
| `Pixel/objects/checkpoint.png` | 체크포인트 | |
| `Pixel/objects/hidden.png` | 데이터 파편 | 가까이 가야 나타난다 |
| `Pixel/objects/anomaly.png` | 이상 개체 | |
| `Pixel/objects/computer.png` | 감염된 컴퓨터 | |
| `Pixel/objects/computerRestored.png` | 복구된 컴퓨터 | 해킹에 성공하면 이 그림으로 바뀐다 |

### 플레이어 (걷는 동작, GIF)

방향마다 **GIF 하나**로 그린다. Unity는 GIF 애니메이션을 재생하지 못하므로, 스크립트로 프레임 PNG로 나눠서 넣는다.

| 원본 (게임에 안 들어감) | 게임이 읽는 파일 |
| --- | --- |
| `Executor-Game/Art/player/front.gif` (아래를 봄) | `Pixel/player/front_0.png`, `front_1.png` ... |
| `Executor-Game/Art/player/back.gif` (위를 봄) | `Pixel/player/back_0.png` ... |
| `Executor-Game/Art/player/left.gif`, `right.gif` | `Pixel/player/left_0.png`, `right_0.png` ... |

- **크기**: 18 × 32 도트를 4배 → **72 × 128 px**. 모든 방향·프레임을 같은 크기로, 발은 그림 아래쪽에 맞춘다.
- **프레임**: 몇 장이든 된다 (지금 4장). **0번(첫 프레임)이 멈춰 있을 때 자세**다. 걸을 때는 1번부터 차례로 돌아간다.
- **속도**: 한 칸 이동 0.18초, 프레임 하나 0.12초 (`BoardView`의 `MoveSeconds`, `WalkFrameSeconds`). GIF의 프레임 시간은 쓰지 않는다.
- GIF를 고친 뒤에는 Executor-Game 폴더에서 아래를 실행하면 프레임 PNG가 다시 만들어진다.

  ```bash
  python Tools/gif_to_frames.py
  ```

  (Pillow가 필요하다: `pip install pillow`)
- 프레임이 없으면 `Pixel/player.png` 한 장을 쓰고(걷는 동작 없음), 그것도 없으면 임시 도트가 나온다.

### 가져오기 설정 (도트)

`Assets/Resources/Pixel/` 아래 그림은 **자동으로 맞춰진다** (`Assets/Scripts/Editor/PixelArtImporter.cs`): Sprite, **Point 필터, 압축 없음**, 밉맵 없음.
크기는 코드가 정하므로 Pixels Per Unit은 신경 쓰지 않아도 된다. 흐려 보이면 그림을 고르고 오른쪽 클릭 → **Reimport**.

---

## 2. 대화 스탠딩 일러스트

대화창 위에 서 있는 인물 그림이다. 화면에서 **세로 1000px**(기준 해상도의 대부분)로 보이고, 가로는 그림 비율대로 정해진다.

- **권장 크기: 1200 × 2000**. 지금 들어 있는 주인공·카시아 그림이 이 크기다.
- 배경 투명. 인물의 **발(또는 허리 아래)이 그림 아래쪽 끝**에 닿게 그린다. 그림은 화면 아래에 붙어서 위로 서 있다.
- 위치: **주인공은 왼쪽, 다른 인물은 오른쪽** (화면 끝에서 120px 안쪽).
- 표정마다 파일 하나. `default.png`는 꼭 있어야 한다 (없는 표정을 부르면 default를 쓴다).

```
Portraits/
├── Player/       주인공 (지금 폴더 이름. 소문자 player로 바꿔도 된다)
│   ├── default.png
│   └── smile.png
├── Cassia/
│   ├── default.png
│   ├── smile.png
│   └── surprised.png
└── delta/        (예정)
    └── default.png
```

맵 대화에서는 `"portrait": "cassia/smile"`처럼 부른다. 비우면 이름표(`speaker`)로 인물을 찾고 표정은 `default` ([4장](#4-인물-id)).

### 가져오기 설정 (일러스트)

| 항목 | 값 |
| --- | --- |
| Texture Type | Sprite (2D and UI) |
| Filter Mode | Bilinear |
| **Max Size** | **2048 이상** (기본 2048. 세로 2000 그림이 줄어들지 않게) |
| Compression | Normal Quality (흐려 보이면 High Quality) |

---

## 3. 화면 일러스트·도감 그림

| 파일 | 그릴 크기 | 화면에서 | 메모 |
| --- | --- | --- | --- |
| `Illustrations/title.png` | 1920 × 1080 | 시작 화면 전체를 덮는다 | 비율이 다르면 넘치는 쪽을 잘라 낸다 |
| `Illustrations/title_logo.png` | 760 × 300 안 | 화면 가운데 위쪽 760 × 300 상자 안, **비율 유지** | 배경 투명. 지금 그림은 761 × 196 |
| `Illustrations/main.png` | 1120 × 600 (또는 2배 2240 × 1200) | 메인 화면 가운데 상자를 덮는다 | 비율이 다르면 잘라 낸다 |
| `Guidebook/{이름}.png` | 512 × 512 | 도감 상세 왼쪽 360 × 360 상자 (안쪽 344 × 344), **비율 유지** | 정사각형 권장 |

- 시작 화면은 아래쪽에 "화면을 클릭해 게임을 시작하세요" 문구와 버튼이 올라가므로, **아래 1/4에는 중요한 것을 그리지 않는다.**
- 도감 그림 이름은 관리자 웹 도감의 **이미지** 칸에 적는 값의 파일 이름이다. `/guide/npe.png`, `guide/npe`, `npe` 모두 `Guidebook/npe.png`를 찾는다.

가져오기 설정은 [일러스트](#가져오기-설정-일러스트)와 같다.

---

## 4. 인물 ID

대화의 이름표(`speaker`)와 그림 폴더를 잇는 표다 (`ArtCatalog.SpeakerToCharacter`). **새 인물을 넣으면 여기에 한 줄 추가**해야 이름표만으로 그림을 찾는다.

| 이름표 | 인물ID (폴더 이름) | 스탠딩 일러스트 | 맵 도트 |
| --- | --- | --- | --- |
| 집행자 · `{nickname}` (주인공) | `player` | 있음 | `Pixel/player/` (걷는 동작) |
| 카시아 | `cassia` | 있음 | `Pixel/npcs/cassia.png` (없음) |
| 연구원 | `researcher` | 없음 | 없음 |
| 감독관 | `supervisor` | 없음 | 없음 |
| 델타 | `delta` (추가 필요) | 없음 | 없음 |

---

## 5. 아직 쓰지 않는 그림

| 그림 | 상태 |
| --- | --- |
| 상점 아이템 이미지 (`items.image`) | 서버에 값은 있지만 화면에 아직 안 쓴다. 쓰게 되면 **128 × 128** 아이콘을 권장 |
| 스킨 | 장착은 저장되지만 화면은 그대로. 적용하면 **플레이어 걷기 프레임 묶음을 바꾸는** 방식이 될 예정 |
| 테마 | 장착은 저장되지만 화면은 그대로. 적용하면 **바닥·벽 타일 묶음을 바꾸는** 방식이 될 예정 |

---

## 그림을 넣은 뒤 확인

1. 파일을 위 경로에 넣고 가져오기 설정을 맞춘다 (**Apply**).
2. ▶ 재생해서 그 화면을 연다. 임시 색 상자 대신 그림이 나오면 된다.
3. 안 바뀌면: 경로·파일 이름(대소문자, 확장자 `.png`)과 Texture Type(Sprite)을 확인한다.
