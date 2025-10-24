using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Logic.GameCustom.Abstracts;
using Logic.GameCustom.Components;
using Logic.GameCustom.Enums;
using Logic.RandomSystem.Singletones;
using Logic.UnitAISystem;
using Logic.UnitAISystem.Entities;
using Logic.UnitAISystem.Factory;

using UnityEngine;

using Behaviour = Logic.UnitAISystem.Entities.Behaviour;

namespace Logic.GameCustom.SubComponents
{
    public class AISubComponent : GameEntity
    {
        public string BehaviourKey = "default";
        public AITag[] Tags;
        public float FieldOfView = 45;
        
        private MonoBehaviour _coroutineRunner;
        private GameEntity _unitAI;
        
        private readonly List<GameEntity> _enemiesRegistered = new ();
        private readonly List<GameEntity> _enemiesPresumptive = new ();
        
        private List<BehaviourTemplate> _defaultBehaviours;
        private List<BehaviourTemplate> _temporaryBehaviourQueue;
        private Behaviour _currentBehaviour;
        
        private protected override void OnAwake()
        {           
            _defaultBehaviours = new List<BehaviourTemplate>();
            _temporaryBehaviourQueue = new List<BehaviourTemplate>();
            _coroutineRunner = this;

            if (transform.parent != null)
                transform.parent.TryGetComponent(out _unitAI);
        }
        private protected override void OnStart()
        {
            _unitAI.Subscribe("onDied", OnDied);
            
            _defaultBehaviours.AddRange(AIFactory.GetDefaultBehaviourTemplates(BehaviourKey, _unitAI));
            
            _coroutineRunner.StartCoroutine(DefaultBehavioursQueueingCoroutine());
            _coroutineRunner.StartCoroutine(ImmediateExecutionBehavioursQueueingCoroutine());
            _coroutineRunner.StartCoroutine(EnemyDetection());
        }
        private protected override void OnRegister()
        {
            RegisterTag("AI");
            RegisterComposition(CompositionKey.trigger2D);

            RegisterAnswer(AIManager.aiGetTags, () => Tags);
            
            RegisterMessage<BehaviourTemplate>(AIManager.aiIntegrateDefaultBehaviour, (x) => IntegrateDefaultBehaviour = x);
            RegisterMessage<BehaviourTemplate>(AIManager.aiInsertTemporaryBehaviour, (x) => InsertTemporaryBehaviour = x);
            RegisterMessage(AIManager.aiBreakCurrentBehaviour, BreakCurrentBehaviour);
            
            RegisterMessage(AIManager.aiPause, PauseAI);
            RegisterMessage(AIManager.aiPlay, () => _isPaused = false);
            
            RegisterAnswer(AIManager.aiGetEnemies, () => _enemiesRegistered);
            RegisterAnswer(AIManager.aiGetEnemies, () => _enemiesRegistered.ToArray());
            RegisterAnswer(Unit2DEntity.unitIsInFight, () => _enemiesRegistered.Count > 0);
        }
        private void OnDied(GameEntity entity)
        {
            _currentBehaviour?.Dispose();
            _defaultBehaviours.Clear();
            _temporaryBehaviourQueue.Clear();
            _coroutineRunner.StopAllCoroutines();
        }

        #region AI WORK
        private bool _isPaused = false;
        private bool _isBehaviourDisposing = false;
        private bool _isImmediateExecution = false;
        private IEnumerator DefaultBehavioursQueueingCoroutine()
        {
            while (true)
            {
                while (_isBehaviourDisposing || _isImmediateExecution || _isPaused)
                    yield return null;
                
                while (_currentBehaviour == null || _currentBehaviour.IsDone)
                {
                    if (!DequeueBehaviour())
                        DequeueDefaultBehaviour();
                    yield return null;
                }

                yield return _currentBehaviour?.Wait();
            }
        }
        private IEnumerator ImmediateExecutionBehavioursQueueingCoroutine()
        {
            while (true)
            {
                while (_temporaryBehaviourQueue.Count == 0 || _isPaused)
                    yield return null;
                if (_temporaryBehaviourQueue[0]._immediateExecution
                    && _temporaryBehaviourQueue[0]._priority >= _currentBehaviour?.Priority)
                {
                    _isImmediateExecution = true;
                    if (!_isBehaviourDisposing)
                        yield return _coroutineRunner.StartCoroutine(DisposeCurrentBehaviour());
                    DequeueBehaviour();
                    _isImmediateExecution = false;
                }
                yield return null;
            }
        }
        private IEnumerator DisposeCurrentBehaviour()
        {
            _isBehaviourDisposing = true;
                    
            if (_currentBehaviour != null)
            {
                yield return _currentBehaviour.Interrupt();
                yield return _currentBehaviour.Wait();
                _currentBehaviour.Dispose();
                _currentBehaviour = null;
            }
            
            _isBehaviourDisposing = false;
        }

        private bool DequeueBehaviour()
        {
            if (_temporaryBehaviourQueue is null or  { Count: 0 })
                return false;
            _currentBehaviour = new UnitAISystem.Entities.Behaviour(_temporaryBehaviourQueue[0]);
            _currentBehaviour.Init(_unitAI, _coroutineRunner);
            _temporaryBehaviourQueue.RemoveAt(0);
            return true;
        }
        private bool DequeueDefaultBehaviour()
        {            
            if (_defaultBehaviours is null or  { Count: 0 })
                return false;
            
            var availableDefaultBehaviours = _defaultBehaviours
                .Where(x => x.IsAvailable(_unitAI))
                .OrderBy(x => CustomRandom.Instance.NextFloat(1f))
                .ToList();
            
            if (availableDefaultBehaviours is null or  { Count: 0 })
                return false;
            _currentBehaviour = new UnitAISystem.Entities.Behaviour(availableDefaultBehaviours[0]);
            _currentBehaviour.Init(_unitAI, _coroutineRunner);
            return true;
        }
        
        public BehaviourTemplate InsertTemporaryBehaviour
        {
            set
            {
                if (!IsAlive)
                    return;
                int insertIndex = value._breakQueue
                    ? _temporaryBehaviourQueue.FindIndex(x => x._priority <= value._priority)
                    : _temporaryBehaviourQueue.FindLastIndex(x => x._priority <= value._priority);

                if (insertIndex == -1)
                    _temporaryBehaviourQueue.Add(value);
                else
                    _temporaryBehaviourQueue.Insert(
                        value._breakQueue ? insertIndex : insertIndex + 1,
                        value);
            }
        }
        public BehaviourTemplate IntegrateDefaultBehaviour
        {
            set
            {
                if (!IsAlive)
                    return;
                _defaultBehaviours.Add(value);
            }
        }

        public void BreakCurrentBehaviour()
        {
            if (!IsAlive)
                return;
            if (!_isBehaviourDisposing)
                _coroutineRunner.StartCoroutine(DisposeCurrentBehaviour());
        }
        public void ImmediateBreakCurrentBehaviour()
        {
            if (_currentBehaviour != null)
            {
                _currentBehaviour.Interrupt();
                _currentBehaviour.Dispose();
                _currentBehaviour = null;
            }
        }

        public void PauseAI()
        {
            _isPaused = true;
            BreakCurrentBehaviour();
        }
        #endregion

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
                        if (_enemiesRegistered.Count == 1)
                            InsertTemporaryBehaviour = AIFactory.GetFightInitiateBehaviour(BehaviourKey, _unitAI);
                        break;
                    }

                    yield return wait;
                }
            }
        }
        #endregion
        
        private protected override bool AliveCondition() => _unitAI != null && _unitAI.IsAlive;
        private protected override void OnEntityDestroy()
        {
            ImmediateBreakCurrentBehaviour();
            _coroutineRunner.StopAllCoroutines();
        }
    }
}