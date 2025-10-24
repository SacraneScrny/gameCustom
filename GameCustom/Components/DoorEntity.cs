using Logic.GameCustom.Abstracts;

using UnityEngine;

namespace Logic.GameCustom.Components
{
    public class DoorEntity : GameEntity
    {
        private protected override void OnAwake()
        {
            _dontRegisterDefaults = true;
        }
        private protected override void OnRegister()
        {
            RegisterSerializable<Quaternion>("door::openAngle", 
                () => transform.localRotation,
                (x) => transform.localRotation = x);
        }
    }
}