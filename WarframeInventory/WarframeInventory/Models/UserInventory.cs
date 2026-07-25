namespace WarframeInventory.Models
{
    public class UserWarframe
    {
        public int Id { get; set; }
        public string UserId { get; set; } = "";
        public string WarframeUnique { get; set; } = "";
        public bool Owned { get; set; }
        public string OwnershipState { get; set; } = "missing";
    }

    public class UserWeapon
    {
        public int Id { get; set; }
        public string UserId { get; set; } = "";
        public string WeaponUnique { get; set; } = "";
        public bool Owned { get; set; }
        public string OwnershipState { get; set; } = "missing";
    }

    public class UserComponent
    {
        public int Id { get; set; }
        public string UserId { get; set; } = "";
        // Unique del padre: Warframe o Weapon
        public string ParentUnique { get; set; } = "";
        public string ComponentName { get; set; } = "";
        public bool Owned { get; set; }
        public int Quantity { get; set; }
    }

    public class UserRelic
    {
        public int Id { get; set; }
        public string UserId { get; set; } = "";
        public string RelicUnique { get; set; } = "";
        public int Quantity { get; set; }
    }
}
