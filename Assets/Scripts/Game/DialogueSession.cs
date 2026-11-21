using System;
using System.Collections.Generic;
using Executor.Data;

namespace Executor.Game
{
    /// <summary>
    /// 인물과의 대화 한 번. 노드를 따라가며 선택지를 처리하고, 노드에 들어갈 때 effects(giveKey)를 실행한다.
    /// 대화는 턴을 쓰지 않는다. 대화 중에는 화면이 이동 입력을 막아야 한다.
    /// 이야기(story)의 dialogue 단계에서도 쓴다: 그때는 인물·판이 없어 effects는 실행하지 않는다
    /// </summary>
    public class DialogueSession
    {
        private readonly Board _board;
        private readonly DialogueData _dialogue;
        private readonly string _nickname;
        private readonly List<string> _givenKeys = new List<string>();

        /// <summary>대화하는 인물. 이야기 속 대화면 null</summary>
        public BoardObject Npc { get; }
        public DialogueNode Current { get; private set; }
        public bool IsFinished => Current == null;

        /// <summary>현재 말하는 사람 (노드에 없으면 인물 이름, {nickname} 치환됨)</summary>
        public string Speaker => Current == null ? null : TextFormat.Apply(Current.Speaker ?? Npc?.Name, _nickname);

        /// <summary>현재 대사 ({nickname} 치환됨)</summary>
        public string Text => Current == null ? null : TextFormat.Apply(Current.Text, _nickname);

        public IReadOnlyList<DialogueChoice> Choices => Current?.Choices ?? (IReadOnlyList<DialogueChoice>)Array.Empty<DialogueChoice>();

        public bool HasChoices => Choices.Count > 0;

        /// <summary>이번 대화에서 받은 열쇠 (화면에 "열쇠를 받았다" 표시용)</summary>
        public IReadOnlyList<string> GivenKeys => _givenKeys;

        /// <exception cref="ArgumentException">인물의 dialogueId가 맵에 없음</exception>
        public DialogueSession(Board board, BoardObject npc, string nickname)
        {
            _board = board;
            Npc = npc;
            _nickname = nickname;
            if (npc.DialogueId == null || !board.Data.Dialogues.TryGetValue(npc.DialogueId, out _dialogue))
            {
                throw new ArgumentException($"인물 {npc.Id}의 대화 {npc.DialogueId}가 맵에 없습니다.");
            }
            Enter(_dialogue.Start);
        }

        /// <summary>이야기(story)의 dialogue 단계: 인물 없이 대화만 진행한다 (giveKey 등 effects는 무시)</summary>
        public DialogueSession(DialogueData dialogue, string nickname)
        {
            _dialogue = dialogue ?? throw new ArgumentNullException(nameof(dialogue));
            _nickname = nickname;
            Enter(_dialogue.Start);
        }

        /// <summary>선택지가 없는 노드에서 다음으로 넘어간다 (마지막 노드면 대화 끝)</summary>
        public void Advance()
        {
            if (IsFinished) return;
            if (HasChoices) throw new InvalidOperationException("선택지가 있는 노드는 Choose를 호출해야 합니다.");
            Enter(Current.Next);
        }

        /// <summary>선택지를 고른다</summary>
        public void Choose(int index)
        {
            if (IsFinished) return;
            if (index < 0 || index >= Choices.Count) throw new ArgumentOutOfRangeException(nameof(index));
            Enter(Choices[index].Next);
        }

        private void Enter(string nodeId)
        {
            if (nodeId == null || !_dialogue.Nodes.TryGetValue(nodeId, out DialogueNode node))
            {
                Current = null;
                return;
            }
            Current = node;
            foreach (DialogueEffect effect in node.Effects ?? new List<DialogueEffect>())
            {
                if (effect.Type == "giveKey" && _board != null && Npc != null && _board.GiveKeyFrom(Npc, effect.KeyId))
                {
                    _givenKeys.Add(effect.KeyId);
                }
            }
        }
    }

    public static class TextFormat
    {
        /// <summary>{nickname}을 내 닉네임으로 바꾼다 (닉네임이 없으면 "집행자"). 예전 표기 {characterName}도 받는다</summary>
        public static string Apply(string text, string nickname)
        {
            string name = string.IsNullOrWhiteSpace(nickname) ? "집행자" : nickname;
            return text?.Replace("{nickname}", name).Replace("{characterName}", name);
        }
    }
}
