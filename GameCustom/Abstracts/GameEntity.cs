using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Logic.Extensions;
using Logic.GameCustom.Entities;
using Logic.GameCustom.Enums;
using Logic.SerializationSystem;

using Sirenix.OdinInspector;

using UnityEngine;

namespace Logic.GameCustom.Abstracts
{
    public abstract class GameEntity : SerializableBehaviour, IEquatable<GameEntity>
    {
        public const string ObjectTag = "GameEntity";
        
        #region KEYS
        public const string teamGetTeam = "team::getTeam";
        
        public const string transformPosition = "transform::position";
        public const string transformPositionAdd = "transform::position::add";
        public const string transformPositionMul = "transform::position::mul";
        public const string transformRotation = "transform::rotation";
        public const string transformRotationAdd = "transform::rotation::add";
        public const string transformScale = "transform::scale";
        public const string transformScaleAdd = "transform::scale::add";
        public const string transformScaleMul = "transform::scale::mul";
        
        public const string rigidbodyGetVelocity = "rigidbody::getVelocity";
        public const string rigidbodyIsKinematic = "rigidbody::isKinematic";
        public const string rigidbodyAngularVelocity = "rigidbody::angularVelocity";
        public const string rigidbodyLinearVelocity = "rigidbody::linearVelocity";
        public const string rigidbodyAddForce = "rigidbody::addForce";
        public const string rigidbodyAddTorque = "rigidbody::addTorque";
        
        public const string rigidbody2DGetVelocity = "rigidbody::getVelocity";
        public const string rigidbody2DIsKinematic = "rigidbody2d::isKinematic";
        public const string rigidbody2DAngularVelocity = "rigidbody2d::angularVelocity";
        public const string rigidbody2DLinearVelocity = "rigidbody2d::linearVelocity";
        public const string rigidbody2DAddForce = "rigidbody2d::addForce";
        public const string rigidbody2DAddTorque = "rigidbody2d::addTorque";
        
        public const string colliderEnabled = "collider::enabled";
        public const string collider2DEnabled = "collider2d::enabled";
        
        public const string rendererEnabled = "renderer::enabled";
        public const string rendererMaterials = "renderer::materials";
        public const string rendererMaterial = "renderer::material";
        #endregion

        #region CACHED COMPONENTS
        private protected Rigidbody _rigidbody;
        private protected Rigidbody2D _rigidbody2d;
        private protected Collider _collider;
        private protected Collider2D _collider2d;
        private protected Renderer _renderer;
        #endregion

        #region ENTITY STATE
        public string[] PrivateTags;
        [ShowInInspector, ReadOnly][FoldoutGroup("Game Entity")] 
        private readonly HashSet<uint> _hashedPrivateTags = new ();
        
        private int _id;
        [SerializeField, ShowInInspector, ReadOnly] private string _guid;
        [FoldoutGroup("Game Entity")] public bool IsRedirectFailedMessages;
        [FoldoutGroup("Game Entity")] public bool IsTraceable;
        [FoldoutGroup("Game Entity")] [MultiLineProperty(30)] [ShowIf("IsTraceable")]
        public string TraceString;
        [FoldoutGroup("Game Entity")] public string Guid => _guid;
        
        [Button] [FoldoutGroup("Game Entity")]
        public void UpdateGuid(bool byForce = false)
        {
            Trace($"Try update guid {_guid}");
            if (byForce || string.IsNullOrEmpty(_guid))
            {
                _guid = System.Guid.NewGuid().ToString();
                Trace($"Guid updated {_guid}");
            }
        }
        public void SetGuid(string guid)
        {
            Trace($"Try overwrite guid {_guid}");
            _guid = guid;
            Trace($"Guid has overwrited {_guid}");
        }
        private EntityState _entityState = EntityState.None;
        [ShowInInspector] [FoldoutGroup("Game Entity")]  public EntityState EntityState => _entityState;
        private bool _isAlive = true;
        private protected bool _dontRegisterDefaults;
        #endregion

        #region MESSAGES CONTAINERS
        [ShowInInspector, ReadOnly][FoldoutGroup("Game Entity")] 
        private readonly Dictionary<string, IMessage> _messages = new();
        [ShowInInspector, ReadOnly][FoldoutGroup("Game Entity")] 
        private readonly Dictionary<string, IMessage> _answers = new();
        [ShowInInspector, ReadOnly][FoldoutGroup("Game Entity")] 
        private readonly HashSet<string> _compositions = new();
        [ShowInInspector, ReadOnly][FoldoutGroup("Game Entity")] 
        private readonly Dictionary<string, Action<GameEntity>> _events = new();
        #endregion

        #region MESSAGES METHODS
        private protected GameEntity RegisterSubComponent<T>() where T : GameEntity, ISubComponent
        {
            GameObject go = new GameObject(typeof(T).Name);
            go.transform.SetParent(transform, false);
            
            var ret = go.AddComponent<T>();
            ret.IsSerializable = false;
            
            Trace($"Registered sub component {typeof(T).Name}");
            return ret;
        }
        private protected bool RegisterEvent(string eventName, Action<GameEntity> handler)
        {
            Trace($"RegisterEvent: {eventName}");
            return _events.TryAdd(eventName, handler);
        }
        
        private protected bool RegisterMessage<T>(string key, Func<T, T> function)
        {
            Trace($"RegisterMessage: {key} {typeof(T).Name}");
            if (_messages.TryAdd(key.Concat<T>(), new MessageFuncEntity<T>(function)))
                return true;
            _messages.Remove(key.Concat<T>());
            _messages.Add(key.Concat<T>(), new MessageFuncEntity<T>(function));
            return false;
        }
        private protected bool RegisterMessage(string key, Action function)
        {
            Trace($"RegisterMessage: {key} void");
            if (_messages.TryAdd(key, new MessageActionEntity(function)))
                return true;
            _messages.Remove(key);
            _messages.Add(key, new MessageActionEntity(function));
            return false;
        }
        
        private protected bool RegisterAnswer<T>(string key, Func<T> function)
        {
            Trace($"RegisterAnswer: {key} {typeof(T).Name}");
            return _answers.TryAdd(key.Concat<T>(), new MessageActionEntity<T>(function));
        }
        
        private protected bool RegisterComposition(string key)
        {
            Trace($"RegisterComposition: {key}");
            return _compositions.Add(key);
        }
        private protected bool RegisterComposition(CompositionKey key)
        {
            Trace($"RegisterComposition: {key.ToString()}");
            return RegisterComposition(key.ToString());
        }
        
        public bool SendMessage<T>(string key, T parameter, out T update)
        {
            if (_messages.TryGetValue(key.Concat<T>(), out var raw) && raw is MessageFuncEntity<T> behaviour)
            {
                Trace($"(S) MessageGet: {key} {typeof(T).Name}");
                update = behaviour.Execute(parameter);
                return true;
            }
            Trace($"(F) MessageGet Error: {key} {typeof(T).Name}");
            update = default;
            OnMessageFailed(key, parameter);
            return false;
        }
        public bool SendMessage<T>(string key, T parameter) => SendMessage<T>(key, parameter, out _);
        public new bool SendMessage(string key)
        {
            if (_messages.TryGetValue(key, out var raw) && raw is MessageActionEntity behaviour)
            {
                Trace($"(S) MessageGet: {key} void");
                behaviour.Execute();
                return true;
            }
            Trace($"(F) MessageGet Error: {key} void");
            OnMessageFailed(key);
            return false;
        }
        
        public bool SendMessages(List<string> keys) => SendMessages(keys.ToArray());
        public bool SendMessages(string[] keys)
        {
            bool ret = true;
            foreach (var k in keys)
                ret &= SendMessage(k);
            return ret;
        }

        private void OnMessageFailed<T>(string key, T parameter)
        {
            if (IsRedirectFailedMessages)
                foreach (var entity in gameObject.GetComponentsInChildren<GameEntity>())
                    if (!entity.Equals(this))
                        entity.SendMessage(key, parameter);
            OnMessageProcessingFailed(key, parameter);
        }
        private void OnMessageFailed(string key)
        {
            if (IsRedirectFailedMessages)
                foreach (var entity in gameObject.GetComponentsInChildren<GameEntity>())
                    if (!entity.Equals(this))
                        entity.SendMessage(key);
            OnMessageProcessingFailed(key);
        }
        
        public bool GetAnswer<T>(string key, out T answer)
        {
            if (_answers.TryGetValue(key.Concat<T>(), out var raw) && raw is MessageActionEntity<T> abehaviour)
            {
                Trace($"(S) AnswerSent: {key} {typeof(T).Name}");
                answer = abehaviour.Execute();
                return true;
            }
            Trace($"(F) AnswerSent Error: {key} {typeof(T).Name}");
            answer = default;
            return false;
        }        
        public T GetAnswer<T>(string key)
        {
            if (_answers.TryGetValue(key.Concat<T>(), out var raw) && raw is MessageActionEntity<T> abehaviour)
            {
                Trace($"(S) AnswerSent: {key} {typeof(T).Name}");
                return abehaviour.Execute();
            }
            Trace($"(F) AnswerSent Error: {key} {typeof(T).Name}");
            return default;
        }
        
        public bool HasComposition(string compositionKey) => _compositions.Contains(compositionKey);
        public bool HasComposition(CompositionKey compositionKey) => HasComposition(compositionKey.ToString());

        public void Subscribe(string eventName, Action<GameEntity> handler)
        {
            Trace($"Someone subscribed to event: {eventName}");
            if (_events.TryGetValue(eventName, out var ev))
                _events[eventName] = ev + handler;
            else
                _events.Add(eventName, handler);
        }
        public void Unsubscribe(string eventName, Action<GameEntity> handler)
        {
            Trace($"Someone unsubscribed from event: {eventName}");
            if (_events.TryGetValue(eventName, out var ev))
                _events[eventName] = ev - handler;
        }
        private protected bool SendEvent(string eventName)
        {
            if (_events.TryGetValue(eventName, out var ev))
            {
                Trace($"(S) Event sent: {eventName}");
                ev?.Invoke(this);
                return true;
            }
            Trace($"(F) Event sending error: {eventName}");
            return false;
        }

        private protected bool RegisterTag(string tag)
        {
            if (_hashedPrivateTags.Add(tag.MurmurHash()))
            {
                Trace($"Registered Tag: {tag}");
                return true;
            }
            Trace($"Failed to register Tag: {tag}");
            return false;
        }
        public  bool ComparePrivateTag(string tag) => _hashedPrivateTags.Contains(tag.MurmurHash());
        public bool HasAllTags(params string[] tags) => tags.All(tag => ComparePrivateTag(tag));
        public bool HasOneTag(params string[] tags) => tags.Any(tag => ComparePrivateTag(tag));
        #endregion

        #region INITIALIZATION
        private void Awake()
        {
            Trace("Awake Start");
            OnAwake();
            if (!GameEntitiesManager.IsLoadingStage && !GameEntitiesManager.IsRegistered(this))
                Initialize();
            _entityState = EntityState.Awaken;
            Trace("Awake End");
        }
        
        public void Initialize()
        {
            Trace("Initialize Start");
                        
            _hashedPrivateTags.Clear();
            foreach (var tag in PrivateTags)
                _hashedPrivateTags.Add(tag.MurmurHash());

            if (string.IsNullOrEmpty(Guid))
                UpdateGuid();
            gameObject.name = _guid;
            gameObject.tag = ObjectTag;
            _id = gameObject.name.MurmurHashInt();
            
            if (!_dontRegisterDefaults)
            {
                RegisterDefaultSerialization();
                RegisterDefaultMessages();
            }
            
            OnRegister();
            GameEntitiesManager.RegisterEntity(this);
            _entityState = EntityState.Initialized;
            OnEntityInitialized?.Invoke();
            Trace("Initialize End");
        }
        public void StartEntity()
        {
            Trace("StartEntity Start");
            if (!IsAlive)
            {
                Destroy(gameObject);
                return;
            }
            OnStart();
            _entityState = EntityState.Started;
            OnEntityStarted?.Invoke();
            Trace("StartEntity End");
        }
        #endregion

        #region VIRTUALS
        /// <summary>
        /// Register messages, answers, serializations and events here
        /// </summary>
        private protected virtual void OnRegister() { }

        private protected virtual void OnMessageProcessingFailed<T>(string key, T parameter) { }
        private protected virtual void OnMessageProcessingFailed(string key) { }
        
        private void RegisterDefaultSerialization()
        {
            RegisterSerializable<Vector3>(transformPosition, () => transform.position, x => transform.position = x);
            RegisterSerializable<Quaternion>(transformRotation, () => transform.rotation, x => transform.rotation = x);
            RegisterSerializable<Vector3>(transformScale, () => transform.localScale, x => transform.localScale = x);
        }
        private void RegisterDefaultMessages()
        {
            RegisterMessage<Vector3>(transformPosition, (x) => transform.position = x);
            RegisterMessage<Vector3>(transformPositionAdd, (x) => transform.position += x);
            RegisterMessage<float>(transformPositionMul, (x) => (transform.position *= x).magnitude);
            
            RegisterMessage<Quaternion>(transformRotation, (x) => transform.rotation = x);
            RegisterMessage<Quaternion>(transformRotationAdd, (x) => transform.rotation *= x);
            
            RegisterMessage<Vector3>(transformScale, (x) => transform.localScale = x);
            RegisterMessage<Vector3>(transformScaleAdd, (x) => transform.localScale += x);
            RegisterMessage<float>(transformScaleMul, (x) => (transform.localScale *= x).magnitude);

            if (TryGetComponent(out _rigidbody))
            {              
                RegisterMessage<bool>(rigidbodyIsKinematic, (x) => _rigidbody.isKinematic = x);
                RegisterMessage<Vector3>(rigidbodyAngularVelocity, (x) => _rigidbody.angularVelocity = x);
                RegisterMessage<Vector3>(rigidbodyLinearVelocity, (x) => _rigidbody.linearVelocity = x);
                RegisterMessage<Vector3>(rigidbodyAddForce, (x) =>
                {
                    _rigidbody.AddForce(x);
                    return _rigidbody.linearVelocity;
                });
                RegisterMessage<Vector3>(rigidbodyAddTorque, (x) =>
                {
                    _rigidbody.AddTorque(x);
                    return _rigidbody.angularVelocity;
                });
                RegisterAnswer(rigidbodyGetVelocity, () => _rigidbody.linearVelocity);
            }
            if (TryGetComponent(out _rigidbody2d))
            {              
                RegisterMessage<bool>(rigidbody2DIsKinematic, (x) => _rigidbody2d.isKinematic = x);
                RegisterMessage<float>(rigidbody2DAngularVelocity, (x) => _rigidbody2d.angularVelocity = x);
                RegisterMessage<Vector2>(rigidbody2DLinearVelocity, (x) => _rigidbody2d.linearVelocity = x);
                RegisterMessage<Vector2>(rigidbody2DAddForce, (x) =>
                {
                    _rigidbody2d.AddForce(x);
                    return _rigidbody2d.linearVelocity;
                });
                RegisterMessage<float>(rigidbody2DAddTorque, (x) =>
                {
                    _rigidbody2d.AddTorque(x);
                    return _rigidbody2d.angularVelocity;
                });
                RegisterAnswer(rigidbody2DGetVelocity, () => _rigidbody2d.linearVelocity);
            }
            if (TryGetComponent(out _collider))
            {            
                RegisterMessage<bool>(colliderEnabled, (x) => _collider.enabled = x);
            }
            if (TryGetComponent(out _collider2d))
            {            
                RegisterMessage<bool>(collider2DEnabled, (x) => _collider2d.enabled = x);
            }
            if (TryGetComponent(out _renderer))
            {            
                RegisterMessage<bool>(rendererEnabled, (x) => _renderer.enabled = x);
                RegisterMessage<Material[]>(rendererMaterials, (x) => _renderer.materials = x);
                RegisterMessage<Material>(rendererMaterial, (x) => _renderer.materials[0] = x);
            }
        }
        
        private protected virtual void OnAwake() { }
        private protected virtual void OnStart() { }
        public virtual void OnUpdate() { }
        public virtual void OnFixedUpdate() { }
        public virtual void OnLateUpdate() { }

        #region Collission 3D
        private static void ThrowCollision(Collision other, Action<GameEntity> gameEntityAction, Action<Collision> otherAction)
        {
            if (other.gameObject.CompareTag(ObjectTag))
            {
                var gameEntity = GameEntitiesManager.GetEntity(other.gameObject.name);
                
                if (gameEntity != null)
                    gameEntityAction(gameEntity);
                else
                    otherAction(other);
            }
            else otherAction(other);
        }
        private static void ThrowCollider(Collider other, Action<GameEntity> gameEntityAction, Action<Collider> otherAction)
        {
            if (other.gameObject.CompareTag(ObjectTag))
            {
                var gameEntity = GameEntitiesManager.GetEntity(other.gameObject.name);
                
                if (gameEntity != null)
                    gameEntityAction(gameEntity);
                else
                    otherAction(other);
            }
            else otherAction(other);
        }
        
        private void OnCollisionEnter(Collision other)
        {
            if (!HasComposition(CompositionKey.collision))
                return;
            Trace($"OnCollisionEnter: {other.gameObject.name}");
            SendEvent("OnCollisionEnter");
            ThrowCollision(other, OnCollisionEnter_GameEntity, OnCollisionEnter_Other);
        }
        private protected virtual void OnCollisionEnter_GameEntity(GameEntity other) { }
        private protected virtual void OnCollisionEnter_Other(Collision other) { }
        
        private void OnCollisionExit(Collision other)
        {
            if (!HasComposition(CompositionKey.collision))
                return;
            Trace($"OnCollisionExit: {other.gameObject.name}");
            SendEvent("OnCollisionExit");
            ThrowCollision(other, OnCollisionExit_GameEntity, OnCollisionExit_Other);
        }
        private protected virtual void OnCollisionExit_GameEntity(GameEntity other) { }
        private protected virtual void OnCollisionExit_Other(Collision other) { }
        
        private void OnCollisionStay(Collision other)
        {
            if (!HasComposition(CompositionKey.collision))
                return;
            SendEvent("OnCollisionStay");
            ThrowCollision(other, OnCollisionStay_GameEntity, OnCollisionStay_Other);
        }
        private protected virtual void OnCollisionStay_GameEntity(GameEntity other) { }
        private protected virtual void OnCollisionStay_Other(Collision other) { }
        
        private void OnTriggerEnter(Collider other)
        {
            if (!HasComposition(CompositionKey.trigger))
                return;
            Trace($"OnTriggerEnter: {other.gameObject.name}");
            SendEvent("OnTriggerEnter");
            ThrowCollider(other, OnTriggerEnter_GameEntity, OnTriggerEnter_Other);
        }
        private protected virtual void OnTriggerEnter_GameEntity(GameEntity other) { }
        private protected virtual void OnTriggerEnter_Other(Collider other) { }
        
        private void OnTriggerExit(Collider other)
        {
            if (!HasComposition(CompositionKey.trigger))
                return;
            Trace($"OnTriggerExit: {other.gameObject.name}");
            SendEvent("OnTriggerExit");
            ThrowCollider(other, OnTriggerExit_GameEntity, OnTriggerExit_Other);
        }
        private protected virtual void OnTriggerExit_GameEntity(GameEntity other) { }
        private protected virtual void OnTriggerExit_Other(Collider other) { }
        
        private void OnTriggerStay(Collider other)
        {
            if (!HasComposition(CompositionKey.trigger))
                return;
            SendEvent("OnTriggerStay");
            ThrowCollider(other, OnTriggerStay_GameEntity, OnTriggerStay_Other);
        }
        private protected virtual void OnTriggerStay_GameEntity(GameEntity other) { }
        private protected virtual void OnTriggerStay_Other(Collider other) { }
        #endregion
        
        #region Collission 2D
        private static void ThrowCollision2D(Collision2D other, Action<GameEntity> gameEntityAction, Action<Collision2D> otherAction)
        {
            if (other.gameObject.CompareTag(ObjectTag))
            {
                var gameEntity = GameEntitiesManager.GetEntity(other.gameObject.name);
                
                if (gameEntity != null)
                    gameEntityAction(gameEntity);
                else
                    otherAction(other);
            }
            else otherAction(other);
        }
        private static void ThrowCollider2D(Collider2D other, Action<GameEntity> gameEntityAction, Action<Collider2D> otherAction)
        {
            if (other.gameObject.CompareTag(ObjectTag))
            {
                var gameEntity = GameEntitiesManager.GetEntity(other.gameObject.name);
                
                if (gameEntity != null)
                    gameEntityAction(gameEntity);
                else
                    otherAction(other);
            }
            else otherAction(other);
        }
        
        private void OnCollisionEnter2D(Collision2D other)
        {
            if (!HasComposition(CompositionKey.collision2D))
                return;
            Trace($"OnCollisionEnter2D: {other.gameObject.name}");
            SendEvent("OnCollisionEnter2D");
            ThrowCollision2D(other, OnCollisionEnter2D_GameEntity, OnCollisionEnter2D_Other);
        }
        private protected virtual void OnCollisionEnter2D_GameEntity(GameEntity other) { }
        private protected virtual void OnCollisionEnter2D_Other(Collision2D other) { }
        
        private void OnCollisionExit2D(Collision2D other)
        {
            if (!HasComposition(CompositionKey.collision2D))
                return;
            Trace($"OnCollisionExit2D: {other.gameObject.name}");
            SendEvent("OnCollisionExit2D");
            ThrowCollision2D(other, OnCollisionExit2D_GameEntity, OnCollisionExit2D_Other);
        }
        private protected virtual void OnCollisionExit2D_GameEntity(GameEntity other) { }
        private protected virtual void OnCollisionExit2D_Other(Collision2D other) { }
        
        private void OnCollisionStay2D(Collision2D other)
        {
            if (!HasComposition(CompositionKey.collision2D))
                return;
            SendEvent("OnCollisionStay2D");
            ThrowCollision2D(other, OnCollisionStay2D_GameEntity, OnCollisionStay2D_Other);
        }
        private protected virtual void OnCollisionStay2D_GameEntity(GameEntity other) { }
        private protected virtual void OnCollisionStay2D_Other(Collision2D other) { }
        
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!HasComposition(CompositionKey.trigger2D))
                return;
            Trace($"OnTriggerEnter2D: {other.gameObject.name}");
            SendEvent("OnTriggerEnter2D");
            ThrowCollider2D(other, OnTriggerEnter2D_GameEntity, OnTriggerEnter2D_Other);
        }
        private protected virtual void OnTriggerEnter2D_GameEntity(GameEntity other) { }
        private protected virtual void OnTriggerEnter2D_Other(Collider2D other) { }
        
        private void OnTriggerExit2D(Collider2D other)
        {
            if (!HasComposition(CompositionKey.trigger2D))
                return;
            Trace($"OnTriggerExit2D: {other.gameObject.name}");
            SendEvent("OnTriggerExit2D");
            ThrowCollider2D(other, OnTriggerExit2D_GameEntity, OnTriggerExit2D_Other);
        }
        private protected virtual void OnTriggerExit2D_GameEntity(GameEntity other) { }
        private protected virtual void OnTriggerExit2D_Other(Collider2D other) { }
        
        private void OnTriggerStay2D(Collider2D other)
        {
            if (!HasComposition(CompositionKey.trigger2D))
                return;
            SendEvent("OnTriggerStay2D");
            ThrowCollider2D(other, OnTriggerStay2D_GameEntity, OnTriggerStay2D_Other);
        }
        private protected virtual void OnTriggerStay2D_GameEntity(GameEntity other) { }
        private protected virtual void OnTriggerStay2D_Other(Collider2D other) { }
        #endregion
        
        private void OnDestroy()
        {
            Trace($"OnDestroy");
            SendEvent("onDestroy");
            OnEntityDestroy();
            GameEntitiesManager.UnregisterEntity(this);
            _isAlive = false;
            _events.Clear();
            _entityState = EntityState.Destroyed;
        }
        private protected virtual void OnEntityDestroy() { }

        public bool IsAlive => _isAlive && AliveCondition();
        private protected virtual bool AliveCondition() => true;
        
        public void Reset()
        {
            Trace($"Trying to reset");
            _entityState = EntityState.Awaken;
            _isAlive = true;

            if (!Application.isPlaying)
                return;
            if (!GameEntitiesManager.UnregisterEntity(this))
                return;
            
            _messages.Clear();
            _compositions.Clear();
            _events.Clear();
            
            OnReset();
            SendEvent("OnReset");
            Initialize();
            
            Trace($"Reset complete");
        }
        private protected virtual void OnReset() { }
        #endregion

        #region OTHER
        public bool Equals(GameEntity other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            Trace($"Check {gameObject.name} equality to {other.gameObject.name}");
            return _id == other._id;
        }
        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((GameEntity)obj);
        }
        public override int GetHashCode() => _id;

        [Button][FoldoutGroup("Game Entity")] 
        public GameEntity Duplicate()
        {
            Trace($"Trying to duplicate");
            var clone = Instantiate(gameObject).GetComponent<GameEntity>();
            clone.UpdateGuid(true);
            clone._messages.Clear();
            clone._compositions.Clear();
            clone._events.Clear();
            clone.Initialize();
            return clone;
        }
        
        private protected void Trace(string message)
        {
            if (!IsTraceable)
                return;
            TraceString += $"\n{System.DateTime.Now:hh:mm:ss.fff} - {message}";
        }
        
        private void OnValidate()
        {
            if (GetType().Name.Contains("SubComponent"))
            {
                _cantBeSerialized = true;
                IsSerializable = false;
            }
            if (string.IsNullOrEmpty(_guid)) UpdateGuid();
        }
        #endregion

        #region EVENTS
        public event System.Action OnEntityInitialized;
        public event System.Action OnEntityStarted;
        #endregion
    }

    public enum EntityState
    {
        None,
        Awaken,
        Initialized,
        Started,
        Destroyed,
    }
    public enum Team : int
    {
        Neutral = 0,
        Blue,
        Green,
        Red,
    }
}