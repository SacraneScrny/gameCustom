using System;
using System.Collections;

using Logic.GameCustom.Abstracts;

using UnityEngine;
using UnityEngine.Events;

namespace Logic.GameCustom.MonoComponents
{
    public class GameEntityEventWrapper : MonoBehaviour
    {
        public string[] EventNames;
        public GameEntity GEntity;
        public UnityEvent OnEvent;
        
        private void Awake()
        {
            
        }
        private IEnumerator Start()
        {
            while (GEntity.EntityState is not (EntityState.Initialized or EntityState.Started))
                yield return null;
            GEntityOnOnEntityInitialized();
        }
        private void GEntityOnOnEntityInitialized()
        {
            foreach (var eventName in EventNames)
                GEntity.Subscribe(eventName, PlayEvent);
        }
        private void PlayEvent(GameEntity obj)
        {
            OnEvent?.Invoke();
        }
    }
}