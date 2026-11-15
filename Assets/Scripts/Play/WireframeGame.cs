using System.Collections.Generic;
using System.Linq;
using Executor.Data;
using Executor.Game;
using Executor.Network;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Executor.Play
{
    /// <summary>
    /// 와이어프레임 앱 흐름: 시작 화면 → 메인 화면 → 스테이지 선택 → 게임.
    /// 빈 씬의 GameObject 하나에 붙이면 카메라·UI를 만든다 (메뉴 Executor > 와이어프레임 플레이 씬 만들기).
    /// <list type="bullet">
    ///   <item>로그인: 서버 계정 (자동 로그인 포함). 오프라인 테스트로도 들어갈 수 있다</item>
    ///   <item>맵: Resources/Maps/tutorial/stage-N.json (Executor-Server docs/maps/tutorial 복사본)</item>
    ///   <item>턴: 남은 턴(parMoves - 쓴 턴)만 보여 주고, -1이 되면 실패</item>
    ///   <item>튜토리얼 진행 기록은 계정별로 PC에 저장 (서버에 튜토리얼 등록 후 서버 기록으로 교체)</item>
    /// </list>
    /// </summary>
    public class WireframeGame : MonoBehaviour
    {
        private const float EndDelay = 0.35f;
        private const string DefaultQuest = "목표: 출구(문 아이콘)에 도착하기";

        private enum Mode { Title, Main, StageSelect, Story, Playing, Dialogue, Result }

        private class StageFile
        {
            public int No;
            public string Title;
            public int Par;
            public MapData Map;
        }

        private readonly List<StageFile> _stages = new List<StageFile>();
        private MenuUI _menu;
        private GameUI _game;
        private BoardView _view;
        private Mode _mode;

        private StageFile _stage;
        private Board _board;
        private GuideTracker _guides;
        private readonly List<string> _questGuides = new List<string>();
        private DialogueSession _dialogue;
        private bool _choicesShown;
        private float _endAt = -1;
        private int _blockInputFrame = -1;

        private List<StoryStep> _story;
        private int _storyIndex;
        private System.Action _afterStory;
        private bool _savingName;

        private void Start()
        {
            if (Camera.main == null)
            {
                var cam = new GameObject("Main Camera", typeof(Camera)) { tag = "MainCamera" };
                cam.GetComponent<Camera>().orthographic = true;
            }
            _view = new GameObject("Board").AddComponent<BoardView>();
            _menu = new GameObject("MenuUI").AddComponent<MenuUI>();
            _menu.Build();
            _game = new GameObject("GameUI").AddComponent<GameUI>();
            _game.Build();
            WireMenu();
            WireGame();
            ApiClient.SessionExpired += () => { if (_mode != Mode.Title) ShowTitle(); };
            LoadStages();
            ShowTitle();
            RestoreLogin();
        }

        private async void RestoreLogin()
        {
            if (await PlayerSession.TryRestoreAsync() && _mode == Mode.Title) ShowTitle();
        }

        private void LoadStages()
        {
            foreach (TextAsset file in Resources.LoadAll<TextAsset>("Maps/tutorial"))
            {
                var json = JObject.Parse(file.text);
                _stages.Add(new StageFile
                {
                    No = json.Value<int>("stageNo"),
                    Title = json.Value<string>("title"),
                    Par = json.Value<int>("parMoves"),
                    Map = MapData.From((JObject)json["mapData"]),
                });
            }
            _stages.Sort((a, b) => a.No.CompareTo(b.No));
        }

        private void Block() => _blockInputFrame = Time.frameCount;

        // ================= 메뉴 =================
        private void WireMenu()
        {
            // 로그인(또는 오프라인)한 뒤에만 화면 클릭으로 메인에 들어간다
            _menu.OnTitleStart = () =>
            {
                if (!_menu.HasPopup && PlayerSession.CanPlay) ShowMain();
            };
            _menu.OnTitleLogin = OpenLogin;
            _menu.OnTitleLogout = Logout;
            _menu.OnTitleOffline = () => { PlayerSession.StartOffline(); ShowTitle(); };
            _menu.OnSettings = () => _menu.ShowSettings(PlayerSession.DisplayName, PlayerSession.IsOnline, Logout);

            _menu.OnProfile = ShowProfile;
            _menu.OnMail = () => _menu.ShowMessage("우편", "우편함은 준비 중입니다.\n(서버 우편 API 연동 예정)");
            _menu.OnShop = () => _menu.ShowMessage("상점", "상점은 준비 중입니다.\n(서버 상점 API 연동 예정)");
            _menu.OnStages = ShowStageSelect;
            _menu.OnMainBack = ShowTitle;

            _menu.OnStageBack = ShowMain;
            _menu.OnStageSelected = no =>
            {
                StageFile s = _stages.FirstOrDefault(x => x.No == no);
                if (s != null && TutorialProgress.IsUnlocked(no)) StartStage(s, true);
            };
            _menu.OnProgress = ShowProgress;
            _menu.OnScenario = () =>
            {
                List<StoryStep> intro = _stages.FirstOrDefault()?.Map.Story?.Intro;
                if (intro == null || intro.Count == 0) { _menu.ShowMessage("시나리오", "아직 시나리오가 없습니다."); return; }
                PlayStory(intro.Where(s => s.Type != "inputCharacterName").ToList(), ShowStageSelect);
            };
        }

        private void ShowTitle()
        {
            _mode = Mode.Title;
            Block();
            HideGame();
            string account = PlayerSession.IsOnline ? $"{PlayerSession.Profile.Nickname} 님으로 로그인" : "오프라인 테스트 중";
            _menu.ShowTitle(PlayerSession.CanPlay, account, PlayerSession.IsOnline ? "로그아웃" : "오프라인 종료");
        }

        private void OpenLogin()
        {
            // 로그인하면 시작 화면으로 돌아가고, 화면을 클릭해 메인으로 들어간다
            _menu.ShowLogin(PlayerSession.LoginAsync, PlayerSession.SignupAsync, ShowTitle);
        }

        private async void Logout()
        {
            await PlayerSession.LogoutAsync();
            ShowTitle();
        }

        private async void ShowMain()
        {
            _mode = Mode.Main;
            Block();
            HideGame();
            ShowMainProfile();
            if (PlayerSession.IsOnline)
            {
                try
                {
                    await PlayerSession.RefreshProfileAsync();
                    if (_mode == Mode.Main) ShowMainProfile();
                }
                catch (ApiException e)
                {
                    Debug.LogWarning("[WireframeGame] 프로필 갱신 실패: " + e);
                }
            }
        }

        private void ShowMainProfile()
        {
            string coin = PlayerSession.IsOnline ? $"코인 {PlayerSession.Profile.Coin}" : "오프라인 (서버 기록 없음)";
            _menu.ShowMain(PlayerSession.DisplayName, coin);
        }

        private void ShowProfile()
        {
            if (!PlayerSession.IsOnline)
            {
                _menu.ShowList("프로필", new[] { "오프라인 테스트 중입니다.", $"집행자 이름: {NameOrDash(PlayerSession.CharacterName)}" });
                return;
            }
            MyProfileResponse p = PlayerSession.Profile;
            _menu.ShowList("프로필", new[]
            {
                $"닉네임: {p.Nickname}",
                $"집행자 이름: {NameOrDash(p.CharacterName)}",
                $"코인: {p.Coin}",
                $"이메일: {p.Email}",
                $"자기소개: {NameOrDash(p.Comment)}",
            });
        }

        private static string NameOrDash(string s) => string.IsNullOrEmpty(s) ? "-" : s;

        private void ShowStageSelect()
        {
            _mode = Mode.StageSelect;
            Block();
            HideGame();
            _menu.ShowStageSelect("튜토리얼 · 홀로그램 시뮬레이션", _stages.Select(s => new MenuUI.StageNode
            {
                No = s.No,
                Title = s.Title.Replace("시뮬레이션 ", ""),
                Unlocked = TutorialProgress.IsUnlocked(s.No),
                Cleared = TutorialProgress.IsCleared(s.No),
                Detail = TutorialProgress.IsCleared(s.No) ? $"최고 {TutorialProgress.BestMoves(s.No)}턴 사용" : null,
            }).ToList());
        }

        private void ShowProgress()
        {
            int cleared = _stages.Count(s => TutorialProgress.IsCleared(s.No));
            var lines = new List<string> { $"클리어 {cleared} / {_stages.Count}", "" };
            foreach (StageFile s in _stages)
            {
                string state = TutorialProgress.IsCleared(s.No)
                    ? $"클리어 · 최소 {TutorialProgress.BestMoves(s.No)}턴 사용 (제한 {s.Par})"
                    : TutorialProgress.IsUnlocked(s.No) ? "도전 가능" : "잠김";
                lines.Add($"{s.No}. {s.Title} — {state}");
            }
            _menu.ShowList("진행 상황", lines);
        }

        private void HideGame()
        {
            _board = null;
            _view.gameObject.SetActive(false);
            _game.HideAll();
        }

        // ================= 게임 =================
        private void WireGame()
        {
            _game.OnSettings = ShowPauseMenu;
            _game.OnChat = () => _game.ShowMessage("채팅", "채팅은 준비 중입니다.\n(친구 메시지 API 연동 예정)");
            _game.OnCollect = () => _game.ShowMessage("수집", "이 스테이지에는 수집 요소가 없습니다.\n(숨겨진 요소는 본 스테이지에서 추가)");
        }

        private void ShowPauseMenu()
        {
            if (_board == null || _game.HasPopup) return;
            _game.ShowMenu("일시 정지", new (string, System.Action)[]
            {
                ("계속하기", null),
                ("다시 하기 (R)", () => StartStage(_stage, false)),
                ("스테이지 선택", ShowStageSelect),
                ("메인으로", ShowMain),
            });
        }

        private void StartStage(StageFile stage, bool withIntro)
        {
            Block();
            _stage = stage;
            _board = new Board(stage.Map, stage.Par);
            _guides = new GuideTracker(_board, PlayerSession.CharacterName);
            _questGuides.Clear();
            _dialogue = null;
            _endAt = -1;
            _menu.HideAll();
            _game.HideAll();
            _view.gameObject.SetActive(true);
            _view.Build(_board);
            _game.ShowHud($"{stage.No}. {stage.Title}", DefaultQuest, 0, 0);
            UpdateHud();

            if (withIntro && stage.Map.Story?.Intro?.Count > 0) PlayStory(stage.Map.Story.Intro, BeginPlay);
            else BeginPlay();
        }

        private void BeginPlay()
        {
            _mode = Mode.Playing;
            Block();
            _guides = new GuideTracker(_board, PlayerSession.CharacterName);
            ShowGuides(_guides.OnStart());
        }

        private void ShowGuides(IReadOnlyList<string> texts)
        {
            foreach (string text in texts)
            {
                _game.Toast(text);
                _questGuides.Add(text);
            }
            if (texts.Count == 0) return;
            // 퀘스트 패널: 목표 + 최근 안내 2개
            _game.SetQuest(DefaultQuest + "\n\n" + string.Join("\n\n", _questGuides.Skip(System.Math.Max(0, _questGuides.Count - 2))));
        }

        private void UpdateHud() => _game.SetTurns(_board.RemainingTurns, _board.Keys.Values.Sum());

        // ---------------- 나레이션 ----------------
        private void PlayStory(List<StoryStep> steps, System.Action after)
        {
            _story = steps;
            _storyIndex = -1;
            _afterStory = after;
            _mode = Mode.Story;
            Block();
            NextStoryStep();
        }

        private void NextStoryStep()
        {
            _storyIndex++;
            while (_storyIndex < _story.Count && _story[_storyIndex].Type == "inputCharacterName"
                   && !string.IsNullOrEmpty(PlayerSession.CharacterName))
            {
                _storyIndex++;
            }
            if (_storyIndex >= _story.Count)
            {
                _game.HideNarration();
                _afterStory?.Invoke();
                return;
            }
            StoryStep step = _story[_storyIndex];
            _game.ShowNarration(TextFormat.Apply(step.Text, PlayerSession.CharacterName),
                step.Type == "inputCharacterName", PlayerSession.CharacterName);
        }

        private bool AskingName => _mode == Mode.Story && _storyIndex < _story.Count
                                   && _story[_storyIndex].Type == "inputCharacterName";

        private async void SubmitName()
        {
            string input = _game.NameInput;
            if (_savingName || input.Length < 1 || input.Length > 20) return;
            _savingName = true;
            _game.SetNarrationHint("저장 중...");
            try
            {
                await PlayerSession.SetCharacterNameAsync(input);
                NextStoryStep();
            }
            catch (ApiException e)
            {
                _game.SetNarrationHint("저장하지 못했습니다: " + e.Message);
            }
            finally
            {
                _savingName = false;
            }
        }

        // ---------------- 입력 ----------------
        private void Update()
        {
            Keyboard k = Keyboard.current;
            if (k == null || Time.frameCount == _blockInputFrame) return;
            bool enter = k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame;
            bool next = k.eKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame || enter
                        || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);

            switch (_mode)
            {
                case Mode.Title:
                    if (!_menu.HasPopup && (enter || k.spaceKey.wasPressedThisFrame)) _menu.OnTitleStart();
                    break;

                case Mode.Main:
                    if (!_menu.HasPopup && k.escapeKey.wasPressedThisFrame) ShowTitle();
                    break;

                case Mode.StageSelect:
                    if (_menu.HasPopup) break;
                    if (k.escapeKey.wasPressedThisFrame) { ShowMain(); return; }
                    for (int i = 0; i < _stages.Count && i < 9; i++)
                    {
                        if (k[Key.Digit1 + i].wasPressedThisFrame || k[Key.Numpad1 + i].wasPressedThisFrame)
                        {
                            _menu.OnStageSelected(_stages[i].No);
                            return;
                        }
                    }
                    break;

                case Mode.Story:
                    if (AskingName) { if (enter) SubmitName(); }
                    else if (next) NextStoryStep();
                    break;

                case Mode.Playing:
                    if (_game.HasPopup)
                    {
                        if (k.escapeKey.wasPressedThisFrame) _game.ClosePopups();
                        return;
                    }
                    if (_endAt >= 0)
                    {
                        if (Time.time >= _endAt) FinishStage();
                        return;
                    }
                    if (k.escapeKey.wasPressedThisFrame) { ShowPauseMenu(); return; }
                    if (k.rKey.wasPressedThisFrame) { StartStage(_stage, false); return; }
                    if (k.eKey.wasPressedThisFrame) { TryTalk(); return; }
                    if (Pressed(k, out Direction dir)) Move(dir);
                    break;

                case Mode.Dialogue:
                    if (_dialogue.HasChoices && !_game.IsTyping)
                    {
                        if (!_choicesShown)
                        {
                            _choicesShown = true;
                            _game.ShowChoices(_dialogue.Choices.Select(c => c.Text).ToList(), Choose);
                        }
                        for (int i = 0; i < _dialogue.Choices.Count && i < 9; i++)
                        {
                            if (k[Key.Digit1 + i].wasPressedThisFrame || k[Key.Numpad1 + i].wasPressedThisFrame)
                            {
                                Choose(i);
                                return;
                            }
                        }
                    }
                    else if (next)
                    {
                        if (_game.IsTyping) _game.CompleteLine();
                        else { _dialogue.Advance(); ShowDialogueNode(); }
                    }
                    break;

                case Mode.Result:
                    if (k.rKey.wasPressedThisFrame) StartStage(_stage, false);
                    else if (k.escapeKey.wasPressedThisFrame) ShowStageSelect();
                    else if (enter && _board != null && _board.IsCleared) NextStage();
                    break;
            }
        }

        private static bool Pressed(Keyboard k, out Direction dir)
        {
            dir = default;
            if (k.wKey.wasPressedThisFrame || k.upArrowKey.wasPressedThisFrame) { dir = Direction.Up; return true; }
            if (k.aKey.wasPressedThisFrame || k.leftArrowKey.wasPressedThisFrame) { dir = Direction.Left; return true; }
            if (k.sKey.wasPressedThisFrame || k.downArrowKey.wasPressedThisFrame) { dir = Direction.Down; return true; }
            if (k.dKey.wasPressedThisFrame || k.rightArrowKey.wasPressedThisFrame) { dir = Direction.Right; return true; }
            return false;
        }

        // ---------------- 플레이 ----------------
        private void Move(Direction dir)
        {
            MoveResult result = _board.Move(dir);
            if (result.UsedTurn) _view.Sync(); else _view.Bump(dir);
            ShowGuides(_guides.AfterMove(result));
            if (result.Outcome == MoveOutcome.PickedUpKey) _game.Toast("열쇠를 얻었습니다.");
            UpdateHud();
            if (_board.IsOver) _endAt = Time.time + EndDelay;
        }

        private void TryTalk()
        {
            BoardObject npc = _board.TalkableNpc;
            if (npc == null) return;
            _dialogue = new DialogueSession(_board, npc, PlayerSession.CharacterName);
            _mode = Mode.Dialogue;
            ShowDialogueNode();
        }

        private void Choose(int index)
        {
            if (_mode != Mode.Dialogue || _dialogue == null || _dialogue.IsFinished || _game.IsTyping) return;
            _dialogue.Choose(index);
            ShowDialogueNode();
        }

        private void ShowDialogueNode()
        {
            Block();
            if (_dialogue.IsFinished)
            {
                _game.HideDialogue();
                if (_dialogue.GivenKeys.Count > 0) _game.Toast("열쇠를 받았습니다.");
                _dialogue = null;
                _mode = Mode.Playing;
                UpdateHud();
                return;
            }
            _choicesShown = false;
            Sprite portrait = ArtCatalog.Portrait(_dialogue.Current.Portrait, _dialogue.Speaker, out string characterId);
            _game.ShowLine(_dialogue.Speaker, _dialogue.Text, portrait, characterId == "player", _dialogue.Speaker);
            UpdateHud();
        }

        // ---------------- 끝 ----------------
        private void FinishStage()
        {
            _endAt = -1;
            if (_board.IsFailed)
            {
                _mode = Mode.Result;
                Block();
                _game.ShowResult($"실패\n\n{_stage.Title}\n남은 턴이 없습니다.", false, new (string, System.Action)[]
                {
                    ("다시 (R)", () => StartStage(_stage, false)),
                    ("스테이지 선택 (Esc)", ShowStageSelect),
                });
                return;
            }

            // TODO: 서버에 튜토리얼이 등록되면 StageApi.SubmitResultAsync(stageId, _board.ToResultRequest())
            int best = TutorialProgress.BestMoves(_stage.No);
            bool newBest = TutorialProgress.RecordClear(_stage.No, _board.Turns);

            void ShowResult()
            {
                _mode = Mode.Result;
                Block();
                bool hasNext = _stages.Any(s => s.No > _stage.No);
                var buttons = new List<(string, System.Action)>();
                if (hasNext) buttons.Add(("다음 (Enter)", NextStage));
                buttons.Add(("다시 (R)", () => StartStage(_stage, false)));
                buttons.Add(("스테이지 선택 (Esc)", ShowStageSelect));
                _game.ShowResult($"클리어!\n\n{_stage.Title}\n남은 턴 {_board.RemainingTurns} · {_board.Turns}턴 사용" +
                                 (newBest ? "\n최고 기록" : $"\n최고 {best}턴 사용"), true, buttons);
            }

            if (_stage.Map.Story?.Outro?.Count > 0) PlayStory(_stage.Map.Story.Outro, ShowResult);
            else ShowResult();
        }

        private void NextStage()
        {
            StageFile next = _stages.FirstOrDefault(s => s.No > _stage.No);
            if (next != null && TutorialProgress.IsUnlocked(next.No)) StartStage(next, true);
            else ShowStageSelect();
        }
    }
}
