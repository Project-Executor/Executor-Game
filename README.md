# Executor(집행자) Game

Unity 6 PC 2D 퍼즐 게임 **Executor(집행자)** 의 클라이언트.
정해진 턴 안에 출구까지 가는 격자 퍼즐이고, 서버([Executor-Server](https://github.com/Project-Executor/Executor-Server))에 로그인하면 기록·별·코인·도감·친구가 저장된다.

| 문서 | 내용 |
| --- | --- |
| [docs/art-guide.md](docs/art-guide.md) | **그림 크기·경로** (도트, 일러스트, 도감 그림). 그림을 넣는 사람은 이것만 보면 된다 |
| [Executor-Server docs/map-data.md](https://github.com/Project-Executor/Executor-Server/blob/main/docs/map-data.md) | 맵 JSON 구조와 게임 규칙 (이동, 턴, 체크포인트, 별, 요소별 동작) |
| [Executor-Server docs/api-spec.md](https://github.com/Project-Executor/Executor-Server/blob/main/docs/api-spec.md) | 서버 API |

## 실행

1. Unity Hub에서 이 폴더를 연다 (에디터 버전은 `ProjectSettings/ProjectVersion.txt`).
2. `Assets/Scenes/Wireframe.unity`를 열고 ▶ 재생. 씬이 없으면 메뉴 **Executor → 와이어프레임 플레이 씬 만들기**.
3. 크게 보려면 Game 탭의 **Play Focused ▾ → Play Maximized** (재생을 멈춘 상태에서), 해상도는 **Full HD (1920x1080)**.

시작 화면에서 고를 수 있다.

| 방식 | 맵 | 기록 |
| --- | --- | --- |
| **로그인** | 서버의 공식 스테이지 (레이어 → Operation → 스테이지) | 서버에 저장 (별, 코인, 도감, 순위) |
| **오프라인 테스트** | PC의 `Assets/Resources/Maps/` | 이 PC에만 저장. 도감·친구·신고는 쓸 수 없음 |

서버 주소는 `Assets/Scripts/Network/ApiConfig.cs`의 `BaseUrl` (기본은 배포 서버, 로컬 서버는 `LocalBaseUrl`).

## 조작

| 키 | 동작 |
| --- | --- |
| W A S D / 방향키 | 이동 (상태가 바뀌는 이동만 1턴) |
| E | 인물과 대화, 감염된 컴퓨터 해킹 (주변 3x3 안의 가장 가까운 대상) |
| Q | 도착한 체크포인트로 워프 |
| M | 맵 전체 보기 ↔ 구간 보기 |
| R | 다시 하기 |
| Esc | 일시 정지 메뉴 (버그 제보 포함) |

## 폴더

```
Assets/
├── Scripts/
│   ├── Game/        규칙 (Unity와 상관없는 순수 C#) — Board(이동·턴·체크포인트·별), DialogueSession, GuideTracker, TypingChallenge
│   ├── Data/        서버·맵 JSON 형식 (MapData, StageDtos, ItemDtos, SocialDtos ...)
│   ├── Network/     서버 호출 (ApiClient: 토큰 첨부·재발급, 기능별 *Api)
│   ├── Play/        화면
│   │   ├── WireframeGame.cs   게임 흐름 (메뉴 ↔ 스테이지 ↔ 결과, 입력 처리)
│   │   ├── MenuUI.cs          시작·메인·스테이지 선택·시나리오·상점·우편·도감 등 메뉴 화면
│   │   ├── GameUI.cs          플레이 화면 (HUD, 대화창, 해킹 창, 결과, 순위)
│   │   ├── BoardView.cs       판 그리기와 카메라
│   │   ├── Minimap.cs         미니맵 그림
│   │   ├── SocialPanel.cs     친구·메시지 팝업
│   │   ├── ReportPanel.cs     신고·버그 제보 팝업
│   │   ├── ArtCatalog.cs      그림 찾기 (없으면 임시 색 상자) — docs/art-guide.md
│   │   ├── ServerSync.cs      서버 스테이지 목록·맵 받기, 못 보낸 결과 다시 보내기
│   │   └── UiKit.cs           UI 부품 (버튼, 입력칸, 팝업, 배치)
│   ├── Dev/         서버 API 확인용 스크립트 (테스트 계정을 만들고 지움)
│   └── Editor/      에디터 메뉴
├── Resources/       코드가 이름으로 불러오는 파일 (그림, 글꼴, 오프라인 맵)
│   ├── Maps/        chapters.json + {챕터}/stage-N.json (오프라인 테스트용)
│   ├── Pixel/       도트 (16px × 4배: 타일·오브젝트 64x64, 플레이어 걷기 프레임 72x128)
│   ├── Portraits/   대화 스탠딩 일러스트 ({인물ID}/{표정}.png)
│   ├── Illustrations/  시작 화면·메인 화면 그림
│   ├── Guidebook/   도감 그림
│   └── Fonts/       Pretendard
└── Scenes/Wireframe.unity
Art/                 원본 그림 (게임에 안 들어감). 예: player/*.gif → Tools/gif_to_frames.py로 프레임 PNG
Tools/RulesCheck/    규칙 점검 (Unity 없이 실행)
Tools/gif_to_frames.py  GIF를 프레임 PNG로 나누기
```

## 규칙 점검

`Game/`의 규칙을 Unity 없이 확인한다 (튜토리얼 맵 풀이, 이동·체크포인트·워프·별·컴퓨터·타자 해킹 등 125개).

```bash
bash Tools/RulesCheck/run.sh                    # 기본: ../Executor-Server/docs/maps/tutorial
```

Unity 에디터가 설치되어 있어야 한다 (에디터에 들어 있는 C# 컴파일러를 쓴다). 결과물은 `Tools/RulesCheck/bin/` (git에 올리지 않음).

## 작업 흐름

- 기능 하나를 마치면 `develop`에 바로 커밋한다. 배포(릴리스) 때만 `develop` → `main` PR을 연다.
- 커밋 제목은 `feat:`, `fix:`, `chore:`, `docs:` 접두어와 짧은 명사형.
- 새 스크립트를 만들면 Unity가 만든 `.meta` 파일도 함께 커밋한다.
