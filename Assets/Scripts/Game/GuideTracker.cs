using System.Collections.Generic;
using System.Linq;
using Executor.Data;

namespace Executor.Game
{
    /// <summary>
    /// 안내 문구(map_data.guides)를 언제 보여 줄지 정한다. 각 안내는 한 판에 한 번만.
    /// <list type="bullet">
    ///   <item>start: 스테이지 시작</item>
    ///   <item>near: 대상 오브젝트와 처음으로 3x3 안에 들어왔을 때</item>
    ///   <item>pickup: 대상 열쇠를 얻었을 때</item>
    /// </list>
    /// </summary>
    public class GuideTracker
    {
        private readonly Board _board;
        private readonly string _characterName;
        private readonly HashSet<GuideData> _shown = new HashSet<GuideData>();

        public GuideTracker(Board board, string characterName = null)
        {
            _board = board;
            _characterName = characterName;
        }

        /// <summary>시작할 때 보여 줄 안내 (start, 그리고 시작 위치에서 이미 가까운 near)</summary>
        public IReadOnlyList<string> OnStart()
        {
            var texts = Take(g => g.On == "start");
            texts.AddRange(TakeNear());
            return texts;
        }

        /// <summary>입력 하나를 처리한 뒤 새로 보여 줄 안내</summary>
        public IReadOnlyList<string> AfterMove(MoveResult result)
        {
            var texts = new List<string>();
            if (result.Outcome == MoveOutcome.PickedUpKey && result.Target != null)
            {
                texts.AddRange(Take(g => g.On == "pickup" && g.Target == result.Target.Id));
            }
            if (result.UsedTurn) texts.AddRange(TakeNear());
            return texts;
        }

        private List<string> TakeNear() => Take(g =>
        {
            if (g.On != "near") return false;
            BoardObject target = _board.Objects.FirstOrDefault(o => o.Id == g.Target);
            return target != null && !target.IsRemoved && target.Pos.IsWithin3x3(_board.PlayerPos);
        });

        private List<string> Take(System.Func<GuideData, bool> match)
        {
            var texts = new List<string>();
            foreach (GuideData guide in _board.Data.Guides ?? new List<GuideData>())
            {
                if (_shown.Contains(guide) || !match(guide)) continue;
                _shown.Add(guide);
                texts.Add(TextFormat.Apply(guide.Text, _characterName));
            }
            return texts;
        }
    }
}
