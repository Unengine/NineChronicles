using System;
using System.Collections.Generic;
using System.Linq;
using Assets.SimpleLocalization;
using Bencodex.Types;
using Nekoyume.Game.Item;
using Nekoyume.TableData;

namespace Nekoyume.Game.Quest
{
    [Serializable]
    public class ItemEnhancementQuest : Quest
    {
        public readonly int Grade;
        private int _current;
        private readonly int _count;

        public ItemEnhancementQuest(ItemEnhancementQuestSheet.Row data) : base(data)
        {
            _count = data.Count;
            Grade = data.Grade;
        }

        public ItemEnhancementQuest(Dictionary serialized) : base(serialized)
        {
            Grade = (int) ((Integer) serialized[(Bencodex.Types.Text) "grade"]).Value;
            _count = (int) ((Integer) serialized[(Bencodex.Types.Text) "count"]).Value;
            _current = (int) ((Integer) serialized[(Bencodex.Types.Text) "current"]).Value;
        }

        public override QuestType QuestType => QuestType.Craft;

        public override void Check()
        {
            Complete = _count == _current;
        }

        public override string ToInfo()
        {
            return string.Format(GoalFormat, GetName(), _current, _count);
        }

        public override string GetName()
        {
            var format = LocalizationManager.Localize("QUEST_ITEM_ENHANCEMENT_FORMAT");
            return string.Format(format, Grade, Goal);
        }

        public void Update(Equipment equipment)
        {
            if (equipment.level == Goal)
                _current++;
            Check();
        }

        protected override string TypeId => "itemEnhancementQuest";

        public override IValue Serialize() =>
            new Bencodex.Types.Dictionary(new Dictionary<IKey, IValue>
            {
                [(Text) "current"] = (Integer) _current,
                [(Text) "grade"] = (Integer) Grade,
                [(Text) "count"] = (Integer) _count,
            }.Union((Bencodex.Types.Dictionary) base.Serialize()));

    }
}