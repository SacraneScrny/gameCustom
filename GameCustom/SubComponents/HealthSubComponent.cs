using Logic.GameCustom.Abstracts;
using Logic.GameCustom.Components;
using Logic.GameCustom.Entities;

using Sirenix.OdinInspector;

namespace Logic.GameCustom.SubComponents
{
    /// <summary>
    /// messages: Damage (f), health (f), health::max (f)
    /// answers: IsDead (b)
    /// events: OnDied
    /// </summary>
    public class HealthSubComponent : GameEntity, ISubComponent
    {
        [ProgressBar(0, "_maxHealth")] [ShowInInspector] [ReadOnly]
        private float _health = 100f;
        private float _maxHealth = 100f;
        private bool _isDead = false;
        
        private protected override void OnRegister()
        {
            RegisterTag("Health");
            RegisterMessage<DamageInfo>("damage", OnDamaged);
            RegisterMessage<float>("health", (x) => _health = x);
            RegisterMessage<float>("health::max", (x) => _maxHealth = x);
            
            RegisterAnswer<float>("getHealth", () => _health);
            RegisterAnswer<float>("getHealth::max", () => _maxHealth);
            RegisterAnswer<bool>("isDead", () => _isDead);
        }

        private DamageInfo OnDamaged(DamageInfo info)
        {
            if (info.To.GetAnswer<bool>(Unit2DEntity.unitIsDodging)) return info;
            
            DamageManager.PendingDamage(info);
            _health -= info.Data.Damage;
            SendEvent("OnDamaged");
            SendEvent("OnHealthChanged");
            info.Proceed = true;
            if (_health <= 0f)
            {
                info.WasLethal = true;
                _health = 0f;
                _isDead = true;
                DamageManager.LethalDamage(info);
                SendEvent("OnDied");
            }
            DamageManager.AppliedDamage(info);
            return info;
        }
    }
}