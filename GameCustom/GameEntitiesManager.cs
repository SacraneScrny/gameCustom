using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Logic.GameCustom.Abstracts;
using Logic.GameCustom.Enums;
using Logic.Managers;
using Logic.SerializationSystem;
using Logic.SerializationSystem.Converters;
using Logic.SerializationSystem.Static;

using Newtonsoft.Json;

using Sirenix.OdinInspector;

using UnityEngine;

namespace Logic.GameCustom
{
    public class GameEntitiesManager : AManager<GameEntitiesManager>
    {
        public bool DontSave;
        [ShowInInspector, ReadOnly]
        private readonly Dictionary<string, GameEntity> _entities = new ();
        private readonly Dictionary<string, GameEntity> _entitiesTemp = new ();
        private SerializationContainer _serializationContainer;
        private bool _isLoadingStage;
        private bool _isInited;
        
        private GameEntity _playerEntity;
        public static GameEntity GetPlayerEntity() => Instance._playerEntity;
        public static bool HasPlayerEntity() => Instance._playerEntity != null;
        
        public static bool IsLoadingStage => Instance._isLoadingStage;
        public static bool IsInited => Instance._isInited;
        
        public static bool IsRegistered(GameEntity entity) => Instance._entities.ContainsKey(entity.Guid);
        public static bool RegisterEntity(GameEntity entity)
        {
            if (Instance._entitiesTemp.TryAdd(entity.Guid, entity))
            {
                if (entity.HasComposition(CompositionKey.update)) Instance.OnUpdate += entity.OnUpdate;
                if (entity.HasComposition(CompositionKey.fixedUpdate)) Instance.OnFixedUpdate += entity.OnFixedUpdate;
                if (entity.HasComposition(CompositionKey.lateUpdate)) Instance.OnLateUpdate += entity.OnLateUpdate;
                Instance.OnRegisterEntity?.Invoke(entity);
                
                if (Instance._playerEntity == null && entity.HasAllTags("Player", "Unit"))
                    Instance._playerEntity = entity;
                return true;
            }
            return false;
        }
        public static bool UnregisterEntity(GameEntity entity)
        {
            if (_instance == null) return false;
            if (Instance._entities.Remove(entity.Guid))
            {
                if (entity.HasComposition(CompositionKey.update)) Instance.OnUpdate -= entity.OnUpdate;
                if (entity.HasComposition(CompositionKey.fixedUpdate)) Instance.OnFixedUpdate -= entity.OnFixedUpdate;
                if (entity.HasComposition(CompositionKey.lateUpdate)) Instance.OnLateUpdate -= entity.OnLateUpdate;
                Instance.OnUnregisterEntity?.Invoke(entity);
                return true;
            }
            return false;
        }

        public static GameEntity GetEntityByTag(string privateTag) 
            => Instance._entities.FirstOrDefault(x => x.Value.ComparePrivateTag(privateTag)).Value;
        public static GameEntity GetEntityWithAllTags(params string[] privateTags) 
            => Instance._entities.FirstOrDefault(x => x.Value.HasAllTags(privateTags)).Value;
        public static GameEntity GetEntityWithOneTag(params string[] privateTags) 
            => Instance._entities.FirstOrDefault(x => x.Value.HasOneTag(privateTags)).Value;
        public static GameEntity GetEntity(string guid) 
            => Instance._entities.GetValueOrDefault(guid);
        
        private protected override void OnManagerAwake()
        {
            JsonConvert.DefaultSettings = () => new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto,
                TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
                Formatting = Formatting.Indented,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                Converters = { new Vector3Converter(), new Vector2Converter(), new QuaternionConverter() },
            };

            _isInited = true;
        }

        private void LoadData()
        {
            _serializationContainer = DataManager.LoadData<SerializationContainer>("SerializationContainer");
            
            //deserialize existing
            _serializationContainer.DeserializeAll(_entitiesTemp, out var missing);

            //deserialize dynamically created
            _isLoadingStage = true;
            Dictionary<string, GameEntity> dynamicEntities = new ();
            foreach (var miss in missing.Where(x => !string.IsNullOrEmpty(x.Value.PrefabKey)))
            {
                var res = Resources.Load(SerializableBehaviour.PrefabPath + miss.Value.PrefabKey);
                var g = Instantiate(res) as GameObject;
                if (g == null)
                {
                    continue;
                }
                
                var gameEntity = g.GetComponent<GameEntity>();
                gameEntity.SetGuid(miss.Key);
                dynamicEntities.Add(miss.Key, gameEntity);
            }
            _isLoadingStage = false;
            foreach (var dynamicEntity in dynamicEntities)
                dynamicEntity.Value.Initialize();
            
            _serializationContainer.DeserializeAll(dynamicEntities, out _);
            Resources.UnloadUnusedAssets();
        }
        public void SaveData()
        {
            _serializationContainer.SerializeALl(_entities.ToDictionary(
                x => x.Value.Guid.ToString(), 
                x => x.Value as SerializableBehaviour)
            );
            DataManager.SaveData("SerializationContainer", _serializationContainer);
        }
        
        private IEnumerator Start()
        {
            yield return null;
            LoadData();
            while (true)
            {
                foreach (var entity in _entitiesTemp)
                {
                    if (_entities.TryAdd(entity.Key, entity.Value))
                    {
                        entity.Value.StartEntity();
                    }
                }
                _entitiesTemp.Clear();
                yield return null;

                while (_entitiesTemp.Count == 0)
                    yield return null;
            }
        }
        private void Update()
        {            
            OnUpdate?.Invoke();
        }
        private void FixedUpdate()
        {            
            OnFixedUpdate?.Invoke();
        }
        private void LateUpdate()
        {            
            OnLateUpdate?.Invoke();
        }
        
        private void OnApplicationQuit()
        {
            if (DontSave && Application.isEditor)
                return;
            SaveData();
        }
        
        public event Action<GameEntity> OnRegisterEntity;
        public event Action<GameEntity> OnUnregisterEntity;
        
        public event Action OnUpdate;
        public event Action OnFixedUpdate;
        public event Action OnLateUpdate;

        [Button]
        public void DeleteSaves()
        {
            DataManager.SaveData("SerializationContainer", new SerializationContainer());
        }

        [Button]
        public void FixAllGuids()
        {
            if (Application.isPlaying)
                return;
            var allEntities = FindObjectsByType<GameEntity>(FindObjectsSortMode.InstanceID);

            for (int i = 0; i < allEntities.Length; i++)
            {
                for (int j = i + 1; j < allEntities.Length; j++)
                {
                    if (allEntities[j].Guid == allEntities[i].Guid
                        || string.IsNullOrEmpty(allEntities[j].Guid))
                    {
                        allEntities[j].UpdateGuid(true);
                        Debug.Log(allEntities[j].name + " fixed " + allEntities[j].Guid);
                    }
                }
            }
            Debug.Log("all guids fixed");
        }
    }
}