using Logic.GameCustom.Abstracts;
using Logic.GameCustom.Enums;

using Sirenix.OdinInspector;

using UnityEngine;

namespace Logic.GameCustom.SubComponents
{
    public class StaminaSubComponent : GameEntity
    {
        public float RegenerateDelay = 1;
        public float RegenerateRate = 1;
        
        [ProgressBar(0, "_maxStamina")] [ShowInInspector] [ReadOnly]
        private float _stamina = 100f;
        private float _maxStamina = 100f;
        private float _currentDelay;
        
        private protected override void OnRegister()
        {
            RegisterComposition(CompositionKey.update);
            RegisterTag("Stamina");
            RegisterMessage<float>("action", OnActionProceed);
            RegisterMessage<float>("stamina", (x) => _stamina = x);
            RegisterMessage<float>("stamina::max", (x) => _maxStamina = x);
            
            RegisterAnswer<bool>("hasStamina", () => _stamina > 0);
            RegisterAnswer<float>("getStamina", () => _stamina);
            RegisterAnswer<float>("getStamina::max", () => _maxStamina);
        }
        
        public override void OnUpdate()
        {
            if (_stamina >= _maxStamina) return;
            if (_currentDelay > 0)
            {
                _currentDelay -= Time.deltaTime;
                return;
            }
            _stamina += RegenerateRate * Time.deltaTime;
            _stamina = Mathf.Clamp(_stamina, 0, _maxStamina);
            
            SendEvent("OnStaminaChanged");
        }
        
        private float OnActionProceed(float dmg)
        {
            _stamina -= dmg;
            _currentDelay = RegenerateDelay;
            SendEvent("OnActionProceed");
            SendEvent("OnStaminaChanged");
            return _stamina;
        }
    }
}