using Logic.Arsenal.Weapons.Entities;
using Logic.GameCustom.Abstracts;

namespace Logic.GameCustom.Entities
{
    public class DamageInfo
    {
        public GameEntity From;
        public GameEntity To;
        public WeaponData Data;

        public bool Proceed;
        public bool WasLethal;

        public DamageInfo(GameEntity from, GameEntity to, WeaponData data)
        {
            From = from;
            To = to;
            Data = data;
        }
    }
}