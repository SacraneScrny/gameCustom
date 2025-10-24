using System;
using System.Collections.Generic;

using Logic.Arsenal.Weapons.Entities;
using Logic.GameCustom.Abstracts;
using Logic.GameCustom.Components;
using Logic.GameCustom.Entities;
using Logic.GameCustom.Enums;

using Sirenix.OdinInspector;

using UnityEngine;

namespace Logic.GameCustom.SubComponents
{
    [RequireComponent(typeof(Collider2D))]
    public class WeaponSubComponent : GameEntity
    {
        public GameEntity Unit;
        public GameEntity Animator;
        public GameEntity Stamina;
        public ClipElement[] Clips;
        
        public List<StaminaCostsEntity> StaminaCosts = new ();
        [Serializable] public struct StaminaCostsEntity { public float[] Costs; }
        
        public WeaponData Data;
        
        private List<GameEntity> _alreadyHit = new List<GameEntity>();
        
        private protected override void OnRegister()
        {
            RegisterTag("Weapon");
            RegisterComposition(CompositionKey.trigger2D);
        }
        
        private protected override void OnStart()
        {
            _collider2d.enabled = false;
            if (Animator != null)
            {
                Animator.Subscribe("OnAttackStart", OnAttackStart);      
                Animator.Subscribe("OnAttackEnd", OnAttackEnd);      
                Animator.SendMessage(AnimatorSubComponent.animationRegisterTemporaryClips, Clips);
            }
        }
        
        private void OnAttackEnd(GameEntity obj)
        {
            SendEvent("OnAttackEnd");
            _alreadyHit.Clear();
            _collider2d.enabled = false;
        }
        private void OnAttackStart(GameEntity obj)
        {
            SendEvent("OnAttackStart");
            _collider2d.enabled = true;

            if (Stamina == null)
                return;
            int aType = Unit.GetAnswer<int>(Unit2DEntity.unitGetAttackType);
            int aNum = Unit.GetAnswer<int>(Unit2DEntity.unitGetAttackNum);
            if (StaminaCosts.Count > aType && StaminaCosts[aType].Costs.Length > aNum)
                Stamina.SendMessage<float>("action", StaminaCosts[aType].Costs[aNum]);
        }
        
        private protected override void OnTriggerEnter2D_GameEntity(GameEntity other)
        {
            var otherTeam = other.GetAnswer<Team>(Unit2DEntity.teamGetTeam);
            if (Unit != null)
            {
                if (Unit.GetAnswer<Team>(Unit2DEntity.teamGetTeam) == otherTeam
                    && otherTeam != Team.Neutral)
                    return;
            }
            if (_alreadyHit.Contains(other))
                return;
            
            other.SendMessage<DamageInfo>("damage", new DamageInfo(Unit, other, Data));
            _alreadyHit.Add(other);
            SendEvent("OnHit");
        }
    }
}