using System.Collections.Generic;
using System.Linq;
using Libplanet;
using Libplanet.Action;
using Nekoyume.Battle;
using Nekoyume.Data;
using Nekoyume.Data.Table;
using Nekoyume.Game.Item;
using Nekoyume.Game.Skill;
using Nekoyume.Model;
using Nekoyume.State;
using NUnit.Framework;

namespace Tests
{
    public class CharacterModelTest
    {
        private readonly IRandom _random;
        private readonly Player _player;

        public CharacterModelTest()
        {
             _random = new TestRandom();
            var address = new Address();
            var agentAddress = new Address();
            var avatarState = new AvatarState(address, agentAddress);
            var simulator = new Simulator(_random, avatarState, new List<Food>(), 1);
            _player = simulator.Player;
            _player.InitAI();
        }

        [Test]
        public void MonsterSelectSkill()
        {
            var data = Tables.instance.Character.Values.First(i => i.id > 200000);
            var monster = new Monster(data, 1, _player);
            monster.InitAI();

            Assert.IsNotEmpty(monster.Skills);

            //Check selected skill is first
            var skill = monster.Skills.Select(_random);
            Assert.AreEqual(1, skill.effect.id);
        }

        [Test]
        public void PlayerSelectSkill()
        {
            Assert.AreEqual(1, _player.Skills.Count());
            foreach (var effect in Tables.instance.SkillEffect.Values)
            {
                var dmg = (int) (1.3m);
                var skill = SkillFactory.Get(0.1m, effect, Elemental.ElementalType.Normal, dmg);
                _player.Skills.Add(skill);
            }
            Assert.AreEqual(5, _player.Skills.Count());

            //Check selected skill is first
            var selected = _player.Skills.Select(_random);
            Assert.AreEqual(1, selected.effect.id);
        }

        private class TestRandom : IRandom
        {
            public int Next()
            {
                return 0;
            }

            public int Next(int maxValue)
            {
                return 0;
            }

            public int Next(int minValue, int maxValue)
            {
                return 0;
            }

            public void NextBytes(byte[] buffer)
            {
            }

            public double NextDouble()
            {
                return default;
            }
        }
    }
}