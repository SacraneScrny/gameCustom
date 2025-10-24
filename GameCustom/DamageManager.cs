using Logic.GameCustom.Entities;
using Logic.Managers;

namespace Logic.GameCustom
{
    public class DamageManager : AManager<DamageManager>
    {
        public event System.Action<DamageInfo> OnAppliedDamage;
        public event System.Action<DamageInfo> OnPendingDamage;
        public event System.Action<DamageInfo> OnLethalDamage;
        
        public static void AppliedDamage(DamageInfo damageInfo) => Instance.OnAppliedDamage?.Invoke(damageInfo);
        public static void PendingDamage(DamageInfo damageInfo) => Instance.OnPendingDamage?.Invoke(damageInfo);
        public static void LethalDamage(DamageInfo damageInfo) => Instance.OnLethalDamage?.Invoke(damageInfo);
    }
}