using System.Collections.Generic;
using System.Linq;
using Executor.Data;
using Executor.Game;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Executor.Play
{
    /// <summary>
    /// 와이어프레임 플레이 (서버 없이 오프라인).
    /// 빈 씬의 GameObject 하나에 붙이면 카메라·UI를 만들고 튜토리얼 6개를 플레이할 수 있다.
    /// 메뉴 Executor > 와이어프레임 플레이 씬 만들기 로 씬을 자동 생성할 수 있다.
    /// <list type="bullet">
    ///   <item>맵: Resources/Maps/tutorial/stage-N.json (Executor-Server docs/maps/tutorial 복사본)</item>
    ///   <item>그래픽: ArtCatalog 경로에 이미지를 넣으면 임시 상자 대신 그 이미지를 쓴다</item>
    ///   <item>집행자 이름·최고 기록은 PlayerPrefs에만 저장 (서버 연동은 이후 작업)</item>
    /// </list>
    /// </summary>
    public class WireframeGame : MonoBehaviour
    {
        private const string NameKey = "executor.characterName";
        private const float ClearDelay = 0.35f;

        private enum Mode { Select, Story, Playing, Dialogue, Result }

        private class StageFile
        {
            public int No;
            public string Title;
            public int Par;
            public MapData Map;
        }

        private readonly List<StageFile> _stages = new List<StageFile>();
        private GameUI _ui;
        private BoardView _view;
        private Mode _mode;

        private StageFile _stage;
        private Board _board;
        private GuideTracker _guides;
        private DialogueSession _dialogue;
        private bool _choicesShown;
        private float _clearAt = -1;
        // 화면이 바뀐 프레임에는 입력을 받지 않는다 (버튼 클릭이 '다음'으로도 잡히는 것 방지)
        private int _blockInputFrame = -1;

        private List<StoryStep> _story;
        private int _storyIndex;
        private System.Action _afterStory;

        private static string CharacterName
        {
            get => PlayerPrefs.GetString(NameKey, "");
            set { PlayerPrefs.SetString(NameKey, value); PlayerPrefs.Save(); }
        }

        private void Start()
        {
            if (Camera.main == null)
            {
                var cam = new GameObject("Main Camera", typeof(Camera)) { tag = "MainCamera" };
                cam.GetComponent<Camera>().orthographic = true;
            }
            _view = new GameObject("Board").AddComponent<BoardView>();
            _ui = new GameObject("UI").AddComponent<GameUI>();
            _ui.Build();
            LoadStages();
            ShowSelect();
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

        // ---------------- 스테이지 선택 ----------------
        private void ShowSelect()
        {
            _blockInputFrame = Time.frameCount;
            _mode = Mode.Select;
            _board = null;
            _view.gameObject.SetActive(false);
            string name = string.IsNullOrEmpty(CharacterName) ? "아직 없음 (1스테이지에서 입력)" : CharacterName;
            _ui.ShowSelect($"집행자 이름: {name}",
                _stages.Select(s =>
                {
                    int best = PlayerPrefs.GetInt(BestKey(s.No), 0);
                    string mark = best > 0 ? $"   ✓ 최고 {best}턴" : "";
                    return ($"{s.No}. {s.Title}{mark}", (System.Action)(() => StartStage(s, true)));
                }));
        }

        private static string BestKey(int no) => $"executor.tutorial.best.{no}";

        // ---------------- 스테이지 시작 ----------------
        private void StartStage(StageFile stage, bool withIntro)
        {
            _blockInputFrame = Time.frameCount;
            _stage = stage;
            _board = new Board(stage.Map);
            _guides = new GuideTracker(_board, CharacterName);
            _dialogue = null;
            _clearAt = -1;
            _ui.HideAll();
            _view.gameObject.SetActive(true);
            _view.Build(_board);
            _ui.ShowHud($"{stage.No}. {stage.Title}");
            UpdateStats();

            if (withIntro && stage.Map.Story?.Intro?.Count > 0)
            {
                PlayStory(stage.Map.Story.Intro, BeginPlay);
            }
            else
            {
                BeginPlay();
            }
        }

        private void BeginPlay()
        {
            _mode = Mode.Playing;
            foreach (string text in _guides.OnStart()) _ui.Toast(text);
        }

        private void UpdateStats()
        {
            int keys = _board.Keys.Values.Sum();
            _ui.SetStats($"턴 {_board.Turns}   기준 {_stage.Par}   열쇠 {keys}");
        }

        // ---------------- 나레이션 ----------------
        private void PlayStory(List<StoryStep> steps, System.Action after)
        {
            _story = steps;
            _storyIndex = -1;
            _afterStory = after;
            _mode = Mode.Story;
            _blockInputFrame = Time.frameCount;
            NextStoryStep();
        }

        private void NextStoryStep()
        {
            _storyIndex++;
            // 이름이 이미 있으면 이름 입력은 건너뛴다
            while (_storyIndex < _story.Count && _story[_storyIndex].Type == "inputCharacterName"
                   && !string.IsNullOrEmpty(CharacterName))
            {
                _storyIndex++;
            }
            if (_storyIndex >= _story.Count)
            {
                _ui.HideNarration();
                _afterStory?.Invoke();
                return;
            }
            StoryStep step = _story[_storyIndex];
            bool askName = step.Type == "inputCharacterName";
            _ui.ShowNarration(TextFormat.Apply(step.Text, CharacterName), askName, CharacterName);
        }

        private bool AskingName => _mode == Mode.Story && _storyIndex < _story.Count
                                   && _story[_storyIndex].Type == "inputCharacterName";

        // ---------------- 입력 ----------------
        private void Update()
        {
            Keyboard k = Keyboard.current;
            if (k == null || Time.frameCount == _blockInputFrame) return;
            bool next = k.eKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame
                        || k.numpadEnterKey.wasPressedThisFrame
                        || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);

            switch (_mode)
            {
                case Mode.Select:
                    for (int i = 0; i < _stages.Count && i < 9; i++)
                    {
                        if (k[Key.Digit1 + i].wasPressedThisFrame || k[Key.Numpad1 + i].wasPressedThisFrame)
                        {
                            StartStage(_stages[i], true);
                            return;
                        }
                    }
                    if (k.nKey.wasPressedThisFrame) { PlayerPrefs.DeleteKey(NameKey); ShowSelect(); }
                    break;

                case Mode.Story:
                    if (AskingName)
                    {
                        if (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame)
                        {
                            string input = _ui.NameInput;
                            if (input.Length >= 1 && input.Length <= 20)
                            {
                                CharacterName = input;
                                _guides = new GuideTracker(_board, CharacterName);
                                NextStoryStep();
                            }
                        }
                    }
                    else if (next)
                    {
                        NextStoryStep();
                    }
                    break;

                case Mode.Playing:
                    if (k.escapeKey.wasPressedThisFrame) { ShowSelect(); return; }
                    if (k.rKey.wasPressedThisFrame) { StartStage(_stage, false); return; }
                    if (_clearAt >= 0)
                    {
                        if (Time.time >= _clearAt) FinishStage();
                        return;
                    }
                    if (k.eKey.wasPressedThisFrame) { TryTalk(); return; }
                    if (Pressed(k, out Direction dir)) Move(dir);
                    break;

                case Mode.Dialogue:
                    if (_dialogue.HasChoices && !_ui.IsTyping)
                    {
                        if (!_choicesShown)
                        {
                            _choicesShown = true;
                            _ui.ShowChoices(_dialogue.Choices.Select(c => c.Text).ToList(), Choose);
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
                        if (_ui.IsTyping) _ui.CompleteLine();
                        else { _dialogue.Advance(); ShowDialogueNode(); }
                    }
                    break;

                case Mode.Result:
                    if (k.rKey.wasPressedThisFrame) StartStage(_stage, false);
                    else if (k.escapeKey.wasPressedThisFrame) ShowSelect();
                    else if (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame) NextStage();
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
            foreach (string text in _guides.AfterMove(result)) _ui.Toast(text);
            if (result.Outcome == MoveOutcome.PickedUpKey) _ui.Toast("열쇠를 얻었습니다.");
            UpdateStats();
            if (_board.IsCleared) _clearAt = Time.time + ClearDelay;
        }

        private void TryTalk()
        {
            BoardObject npc = _board.TalkableNpc;
            if (npc == null) return;
            _dialogue = new DialogueSession(_board, npc, CharacterName);
            _mode = Mode.Dialogue;
            ShowDialogueNode();
        }

        private void Choose(int index)
        {
            if (_mode != Mode.Dialogue || _dialogue == null || _dialogue.IsFinished) return;
            if (_ui.IsTyping) return;
            _dialogue.Choose(index);
            ShowDialogueNode();
        }

        private void ShowDialogueNode()
        {
            if (_dialogue.IsFinished)
            {
                _ui.HideDialogue();
                foreach (string keyId in _dialogue.GivenKeys) _ui.Toast("열쇠를 받았습니다.");
                _dialogue = null;
                _mode = Mode.Playing;
                UpdateStats();
                return;
            }
            _choicesShown = false;
            _blockInputFrame = Time.frameCount;
            Sprite portrait = ArtCatalog.Portrait(_dialogue.Current.Portrait, _dialogue.Speaker, out string characterId);
            _ui.ShowLine(_dialogue.Speaker, _dialogue.Text, portrait, characterId == "player", _dialogue.Speaker);
            UpdateStats();
        }

        // ---------------- 클리어 ----------------
        private void FinishStage()
        {
            _clearAt = -1;
            int best = PlayerPrefs.GetInt(BestKey(_stage.No), 0);
            bool newBest = best == 0 || _board.Turns < best;
            if (newBest) { PlayerPrefs.SetInt(BestKey(_stage.No), _board.Turns); PlayerPrefs.Save(); }

            void ShowResult()
            {
                _mode = Mode.Result;
                bool hasNext = _stages.Any(s => s.No > _stage.No);
                var buttons = new List<(string, System.Action)>();
                if (hasNext) buttons.Add(("다음 (Enter)", NextStage));
                buttons.Add(("다시 (R)", () => StartStage(_stage, false)));
                buttons.Add(("목록 (Esc)", ShowSelect));
                _ui.ShowResult($"클리어!\n\n{_stage.Title}\n턴 {_board.Turns} / 기준 {_stage.Par}" +
                               (newBest ? "\n최고 기록" : $"\n최고 {best}턴"), buttons);
            }

            if (_stage.Map.Story?.Outro?.Count > 0) PlayStory(_stage.Map.Story.Outro, ShowResult);
            else ShowResult();
        }

        private void NextStage()
        {
            StageFile next = _stages.FirstOrDefault(s => s.No > _stage.No);
            if (next != null) StartStage(next, true); else ShowSelect();
        }
    }
}
