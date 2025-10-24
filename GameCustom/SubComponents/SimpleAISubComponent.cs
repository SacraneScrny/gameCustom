using System.Collections;
using System.Collections.Generic;

using Logic.Extensions;
using Logic.GameCustom.Abstracts;
using Logic.GameCustom.Components;
using Logic.GameCustom.Enums;
using Logic.RandomSystem.Singletones;
using Logic.UnitAISystem;
using Logic.UnitAISystem.Factory;

using UnityEngine;

namespace Logic.GameCustom.SubComponents
{
    public class SimpleAISubComponent : GameEntity
    {
        public float MinimumDistanceToTarget = 1;
        public float FieldOfView = 45;
        public float AttackAvailableAngle = 15;
        public GameEntity Health;
        public GameEntity Stamina;

        private float sqrMinDistanceToTarget;
        private bool _isPaused;
        private GameEntity _unitAI;
        private readonly List<GameEntity> _enemiesRegistered = new ();
        private readonly List<GameEntity> _enemiesPresumptive = new ();
        
        private protected override void OnAwake()
        {            
            sqrMinDistanceToTarget = MinimumDistanceToTarget * MinimumDistanceToTarget;
            if (transform.parent != null)
                transform.parent.TryGetComponent(out _unitAI);
        }
        private protected override void OnStart()
        {
            StartCoroutine(EnemyDetection());
        }
        private protected override void OnRegister()
        {            
            RegisterTag("AI");
            RegisterComposition(CompositionKey.trigger2D);
            RegisterComposition(CompositionKey.fixedUpdate);
            
            RegisterMessage(AIManager.aiPause, PauseAI);
            RegisterMessage(AIManager.aiPlay, () => _isPaused = false);
            
            RegisterAnswer(AIManager.aiGetEnemies, () => _enemiesRegistered);
            RegisterAnswer(AIManager.aiGetEnemies, () => _enemiesRegistered.ToArray());
            RegisterAnswer(Unit2DEntity.unitIsInFight, () => _enemiesRegistered.Count > 0);
        }

        public void PauseAI()
        {
            _isPaused = true;
        }

        public override void OnFixedUpdate()
        {                
            if (_isPaused) return;
            if (_enemiesRegistered.Count == 0) return;
            
            var enemy = _enemiesRegistered[0];
            
            Vector2 dir = (enemy.transform.position - transform.position).ToVector2();
            _unitAI.SendMessage<float>(Unit2DEntity.unitRotate, dir.ToEulerAngle());
            
            float d = dir.sqrMagnitude;
            bool shouldAttack = 
                d <= sqrMinDistanceToTarget 
                && Vector2.Angle(dir, transform.right) <= AttackAvailableAngle;
            
            if (shouldAttack)
                _unitAI.SendMessage<Vector2>(Unit2DEntity.unitAttack, dir);
            if (d > sqrMinDistanceToTarget)
                _unitAI.SendMessage(Unit2DEntity.unitMove, dir);
        }

        #region COLLISSION
        private protected override void OnTriggerEnter2D_GameEntity(GameEntity other)
        {
            var team = other.GetAnswer<Team>(teamGetTeam);
            if (team == default || team == _unitAI.GetAnswer<Team>(teamGetTeam))
                return;
            if (_enemiesRegistered.Contains(other) || _enemiesPresumptive.Contains(other))
                return;
            _enemiesPresumptive.Add(other);
        }
        private protected override void OnTriggerExit2D_GameEntity(GameEntity other)
        {
            if (!_enemiesRegistered.Remove(other))
                _enemiesPresumptive.Remove(other);
        }
        #endregion

        #region ENEMY DETECTION
        private IEnumerator EnemyDetection()
        {
            var wait = new WaitForSeconds(CustomRandom.Instance.NextFloat(0.2f, 0.6f));
            while (true)
            {
                while (_enemiesPresumptive.Count == 0)
                    yield return null;
                
                yield return null;
                for (var i = 0; i < _enemiesPresumptive.Count; i++)
                {
                    var a = _enemiesPresumptive[i];
                    if (Vector2.Angle(transform.right, a.transform.position - transform.position) < FieldOfView)
                    {
                        _enemiesRegistered.Add(a);
                        _enemiesPresumptive.Remove(a);
                        break;
                    }

                    yield return wait;
                }
            }
        }
        #endregion
    }
}