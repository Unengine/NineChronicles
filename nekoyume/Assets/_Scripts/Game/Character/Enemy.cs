using System;
using System.Collections;
using Nekoyume.TableData;
using UniRx;
using UnityEngine;

namespace Nekoyume.Game.Character
{
    public class Enemy : CharacterBase
    {
        protected override Vector3 DamageTextForce => new Vector3(0.0f, 0.8f);

        public Guid id;
        public override Guid Id => id;
        
        public int DataId = 0;

        private Player _player;

        public override float Speed => _runSpeed;
        
        protected override Vector3 HUDOffset => animator.GetHUDPosition();
        private float _runSpeed = -1.0f;

        #region Mono

        protected override void Awake()
        {
            base.Awake();
            
            animator = new EnemyAnimator(this);
            animator.OnEvent.Subscribe(OnAnimatorEvent);
            animator.TimeScale = AnimatorTimeScale;
            
            targetTag = Tag.Player;
        }

        private void OnDestroy()
        {
            animator?.Dispose();
        }

        #endregion
        
        public void Init(Model.Monster spawnCharacter, Player player)
        {
            _player = player;
            InitStats(spawnCharacter);
            id = spawnCharacter.id;
            StartRun();

            if (!ShowSpeech("ENEMY", spawnCharacter.data.Id))
            {
                ShowSpeech("ENEMY_INIT", spawnCharacter.spawnIndex);
            }
        }
        
        public override IEnumerator CoProcessDamage(Model.Skill.SkillInfo info, bool isConsiderDie, bool isConsiderElementalType)
        {
            yield return StartCoroutine(base.CoProcessDamage(info, isConsiderDie, isConsiderElementalType));
            var position = transform.TransformPoint(0f, 1f, 0f);
            var force = DamageTextForce;
            PopUpDmg(position, force, info, isConsiderElementalType);

            if (!IsDead())
                ShowSpeech("ENEMY_DAMAGE");
        }
        
        protected override bool CanRun()
        {
            return base.CanRun() && !TargetInRange(_player);
        }

        protected override IEnumerator Dying()
        {
            ShowSpeech("ENEMY_DEAD");
            StopRun();
            animator.Die();
            yield return new WaitForSeconds(.5f);
            DisableHUD();
            yield return new WaitForSeconds(.8f);
            OnDead();
        }

        protected override void OnDead()
        {
            Event.OnEnemyDead.Invoke(this);
            base.OnDead();
        }
        
        private void InitStats(Model.Monster character)
        {
            var stats = character.data.ToStats(character.level);
            HP = stats.HP;
            ATK = stats.ATK;
            DEF = stats.DEF;
            Power = 0;
            HPMax = HP;
            Range = character.attackRange;
            //FIXME 캐릭터 순서에 영향을 받아서 전투가 멈추는 버그가 있음
            // https://app.asana.com/0/958521740385861/1129316006113668
            //_runSpeed = -character.runSpeed;
            _runSpeed = -1f;
            characterSize = character.characterSize;
        }
        
        private void OnAnimatorEvent(string eventName)
        {
            switch (eventName)
            {
                case "attackStart":
                    break;
                case "attackPoint":
                    Event.OnAttackEnd.Invoke(this);
                    break;
                case "footstep":
                    break;
            }
        }

        protected override void ProcessAttack(CharacterBase target, Model.Skill.SkillInfo skill, bool isLastHit, bool isConsiderElementalType)
        {
            ShowSpeech("ENEMY_SKILL", (int)(skill.Elemental ?? 0), (int)skill.skillCategory);
            base.ProcessAttack(target, skill, isLastHit, isConsiderElementalType);
            ShowSpeech("ENEMY_ATTACK");
            UpdateHpBar(model.buffs);
        }

        protected override IEnumerator CoAnimationCast(Model.Skill.SkillInfo info)
        {
            ShowSpeech("ENEMY_SKILL", (int)(info.Elemental ?? 0), (int)info.skillCategory);
            yield return StartCoroutine(base.CoAnimationCast(info));
        }
    }
}
