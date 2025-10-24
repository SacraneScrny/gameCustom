using System.Collections.Generic;

using Logic.ExpandedVariableSystem.Entities;
using Logic.Extensions;
using Logic.GameCustom.Abstracts;
using Logic.GameCustom.Entities;
using Logic.GameCustom.Enums;
using Logic.GameCustom.SubComponents;
using Logic.UnitAISystem;

using UnityEngine;

using Team = Logic.GameCustom.Abstracts.Team;

namespace Logic.GameCustom.Components
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class Unit2DEntity : GameEntity
    {
        public UnitProperties Properties;
        public GameEntity AI;
        public GameEntity Animation;
        public GameEntity Stamina;
        public Team Team;
        
        public const string unitMove = "unit::move";
        public const string unitRotate = "unit::rotate";
        public const string unitDodge = "unit::dodge";
        public const string unitAttack = "unit::attack";
        
        public const string unitIsInFight = "unit::isInFight";
        public const string unitIsActive = "unit::isActive";
        public const string unitIsAttacking = "unit::isAttacking";
        public const string unitGetAttackNum = "unit::getAttackNum";
        public const string unitGetAttackType = "unit::getAttackType";
        public const string unitIsDodging = "unit::isDodging";
        
        public float Inertia = 0.01f;
        
        public ExpandedBool _isActive;
        private bool _isAttacking = false;
        private bool _wasMoved = false;
        private bool _isDodging = false;
        private bool _isDodgeCalled = false;
        
        private protected override void OnAwake()
        {
            _isActive = new ExpandedBool(true);
        }
        private protected sealed override void OnRegister()
        {
            RegisterTag("Unit");
            RegisterAnswer<Team>(teamGetTeam, () => Team);
            
            RegisterComposition(CompositionKey.fixedUpdate);
            RegisterComposition(CompositionKey.lateUpdate);
            
            RegisterAnswer<bool>(unitIsActive, () => _isActive.Value());
            RegisterAnswer<ExpandedBool>(unitIsActive, () => _isActive);
            RegisterAnswer<bool>(unitIsAttacking, () => _isAttacking);
            RegisterAnswer<bool>(unitIsDodging, () => _isDodging);
            
            RegisterAnswer<int>(unitGetAttackNum, () => _attackNum);
            RegisterAnswer<int>(unitGetAttackType, () => _attackType);
            
            RegisterMessage<Vector2>(unitMove, Move);
            RegisterMessage<Vector2>(unitDodge, Dodge);
            RegisterMessage<Vector2>(unitAttack, Attack);
            RegisterMessage<float>(unitRotate, Rotate);

            if (AI != null)
            {
                RegisterAnswer(unitIsInFight, () => AI.GetAnswer<bool>(unitIsInFight));
                RegisterAnswer(AIManager.aiGetEnemies, () => AI.GetAnswer<List<GameEntity>>(AIManager.aiGetEnemies));
            }

            if (Animation != null)
            {
                Animation.Subscribe("OnDodgeStart", (x) => _isDodging = true);
                Animation.Subscribe("OnDodgeEnd", (x) =>
                {
                    _isDodging = false;
                    _isDodgeCalled = false;
                });
                Animation.Subscribe("OnClipDone", OnClipDone);
                Animation.Subscribe("OnAttackPass", OnAttackPass);
            }
            
            OnRegisterUnit();
        }
        private protected virtual void OnRegisterUnit() {}
        
        private Vector2 Move(Vector2 dir)
        {
            if (!_isActive.Value()) return _rigidbody2d.linearVelocity;
            if (_isDodging || _isAttacking) return _rigidbody2d.linearVelocity;
            _wasMoved = true;
            return _rigidbody2d.linearVelocity = Vector2.Lerp(
                _rigidbody2d.linearVelocity,
                dir.normalized * Time.fixedDeltaTime * Properties.MoveSpeed,
                (1f - Inertia) * Time.fixedDeltaTime * 25f);
        }
        private float Rotate(float arg)
        {
            if (!_isActive.Value()) return _rigidbody2d.rotation;
            if (_isDodging || _isDodgeCalled || _isAttacking) return _rigidbody2d.rotation;
            _rigidbody2d.transform.rotation = Quaternion.Lerp(
                _rigidbody2d.transform.rotation,
                Quaternion.Euler(0, 0, arg),
                (1f - Inertia) * Time.fixedDeltaTime * Properties.RotationSpeed
                );
            return _rigidbody2d.rotation;
        }
        private Vector2 Dodge(Vector2 dir)
        {
            if (Stamina != null && !Stamina.GetAnswer<bool>("hasStamina")) return _rigidbody2d.linearVelocity;
            if (!_isActive.Value()) return _rigidbody2d.linearVelocity;
            if (_isDodging || _isAttacking) return _rigidbody2d.linearVelocity;
            if (Animation == null) return _rigidbody2d.linearVelocity;
            if (Animation.GetAnswer<string>("animation::getCurrentClip") == "dodge") return _rigidbody2d.linearVelocity;
            _isDodgeCalled = true;
            Animation.SendMessage("animation::play", "dodge");
            _wasMoved = true;
            _rigidbody2d.transform.rotation = Quaternion.Euler(0, 0, dir.ToEulerAngle());
            Stamina?.SendMessage<float>("action", 20f);
            return _rigidbody2d.linearVelocity = dir.normalized * Properties.DodgeForce;
        }

        private int _attackNum = 0;
        private int _attackType = 0;
        private bool _isAttackMayBePassed = false;
        private bool _isAttackPassCalled = false;
        private Vector2 Attack(Vector2 dir)
        {
            if (Stamina != null && !Stamina.GetAnswer<bool>("hasStamina")) return _rigidbody2d.linearVelocity;
            if (!_isActive.Value()) return _rigidbody2d.linearVelocity;
            if (_isDodging || _isDodgeCalled) return _rigidbody2d.linearVelocity;
            if (Animation == null) return _rigidbody2d.linearVelocity;
            
            if (_isAttacking && !_isAttackMayBePassed)
            {
                _isAttackPassCalled = true;
                return _rigidbody2d.linearVelocity;
            }
            if (_isAttacking && !_isAttackPassCalled) 
                return _rigidbody2d.linearVelocity;
            
            Animation.SendMessage("animation::play", $"attack_{_attackType}_{_attackNum}", out string answer);
            if (!string.IsNullOrEmpty(answer))
            {
                _isAttackMayBePassed = false;
                _isAttacking = true;
            }
            else switch (_attackNum)
            {
                case > 0:
                {
                    _attackNum = 0;
                    Attack(transform.right);
                    break;
                }
                case 0:
                    AttackReset();
                    break;
            }
            return _rigidbody2d.linearVelocity;
        }
        private void OnAttackPass(GameEntity obj)
        {
            _isAttackMayBePassed = true;
            if (_isAttackPassCalled)
            {
                _attackNum++;
                Attack(transform.right);
                _isAttackPassCalled = false;
            }
        }
        
        private void OnClipDone(GameEntity obj)
        {
            _isDodgeCalled = false;
            AttackReset();
        }
        private void AttackReset()
        {
            _isAttackMayBePassed = false;
            _isAttackPassCalled = false;
            _isAttacking = false;
            _attackNum = 0;
        }
        
        public sealed override void OnUpdate()
        {
            OnUpdateUnit();
        }
        private protected virtual void OnUpdateUnit() {}
        
        public override void OnFixedUpdate()
        {
            if (_rigidbody2d.linearVelocity.sqrMagnitude > 0.1f) Animation?.SendMessage("animation::play", "walk");
            InertiaUpdate();
            OnFixedUpdateUnit();
        }
        private protected virtual void OnFixedUpdateUnit() {}
        
        public override void OnLateUpdate()
        {
            _wasMoved = false;
            OnLateUpdateUnit();
        }
        private protected virtual void OnLateUpdateUnit() {}
        
        private void InertiaUpdate()
        {
            if (!_wasMoved)
                _rigidbody2d.linearVelocity -= _rigidbody2d.linearVelocity 
                    * Time.fixedDeltaTime 
                    * (1f - Inertia) / Time.fixedDeltaTime
                    * (_isDodging || _isDodgeCalled ? 0.1f : 1f);
            _rigidbody2d.angularVelocity -= _rigidbody2d.angularVelocity * Time.fixedDeltaTime * (1f - Inertia) / Time.fixedDeltaTime;
        }
    }
}