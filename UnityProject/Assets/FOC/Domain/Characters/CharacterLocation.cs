using System;

namespace FOC.Domain.Characters
{
    public enum CharacterLocationKind
    {
        City = 0,
        Army = 1,
        Caravan = 2,
        WorldPosition = 3,
        Travelling = 4,
        Captivity = 5,
    }

    public readonly struct WorldPosition : IEquatable<WorldPosition>
    {
        public WorldPosition(long x, long y)
        {
            X = x;
            Y = y;
        }

        public long X { get; }
        public long Y { get; }
        public bool Equals(WorldPosition other) => X == other.X && Y == other.Y;
        public override bool Equals(object? obj) => obj is WorldPosition other && Equals(other);
        public override int GetHashCode() => unchecked((X.GetHashCode() * 397) ^ Y.GetHashCode());
    }

    public enum CaptivitySiteKind
    {
        City = 0,
        Army = 1,
        Caravan = 2,
        WorldPosition = 3,
    }

    public sealed class CaptivitySite
    {
        private CaptivitySite(CaptivitySiteKind kind, CityId? cityId, ArmyId? armyId, CaravanId? caravanId, WorldPosition position)
        {
            Kind = kind;
            CityId = cityId;
            ArmyId = armyId;
            CaravanId = caravanId;
            Position = position;
        }

        public CaptivitySiteKind Kind { get; }
        public CityId? CityId { get; }
        public ArmyId? ArmyId { get; }
        public CaravanId? CaravanId { get; }
        public WorldPosition Position { get; }

        public static CaptivitySite InCity(CityId id) { Require(id.IsValid); return new CaptivitySite(CaptivitySiteKind.City, id, null, null, default); }
        public static CaptivitySite WithArmy(ArmyId id) { Require(id.IsValid); return new CaptivitySite(CaptivitySiteKind.Army, null, id, null, default); }
        public static CaptivitySite WithCaravan(CaravanId id) { Require(id.IsValid); return new CaptivitySite(CaptivitySiteKind.Caravan, null, null, id, default); }
        public static CaptivitySite At(WorldPosition position) => new CaptivitySite(CaptivitySiteKind.WorldPosition, null, null, null, position);

        public CharacterLocation ToReleasedLocation()
        {
            switch (Kind)
            {
                case CaptivitySiteKind.City: return CharacterLocation.InCity(CityId!.Value);
                case CaptivitySiteKind.Army: return CharacterLocation.WithArmy(ArmyId!.Value);
                case CaptivitySiteKind.Caravan: return CharacterLocation.WithCaravan(CaravanId!.Value);
                case CaptivitySiteKind.WorldPosition: return CharacterLocation.At(Position);
                default: throw new InvalidOperationException("Unknown captivity site kind.");
            }
        }

        private static void Require(bool isValid)
        {
            if (!isValid) throw new ArgumentException("Captivity site identifier is invalid.");
        }
    }

    public enum CaptivityStatus
    {
        Held = 0,
    }

    public sealed class CaptivityState
    {
        public CaptivityState(CharacterId captorId, CaptivitySite site, CaptivityStatus status = CaptivityStatus.Held)
        {
            if (!captorId.IsValid) throw new ArgumentException("Captor identifier is invalid.", nameof(captorId));
            if (!Enum.IsDefined(typeof(CaptivityStatus), status)) throw new ArgumentOutOfRangeException(nameof(status));
            CaptorId = captorId;
            Site = site ?? throw new ArgumentNullException(nameof(site));
            Status = status;
        }

        public CharacterId CaptorId { get; }
        public CaptivitySite Site { get; }
        public CaptivityStatus Status { get; }
    }

    public sealed class CharacterLocation
    {
        private CharacterLocation(CharacterLocationKind kind, CityId? cityId, ArmyId? armyId, CaravanId? caravanId, WorldPosition position, CaptivityState? captivity)
        {
            Kind = kind;
            CityId = cityId;
            ArmyId = armyId;
            CaravanId = caravanId;
            Position = position;
            Captivity = captivity;
        }

        public CharacterLocationKind Kind { get; }
        public CityId? CityId { get; }
        public ArmyId? ArmyId { get; }
        public CaravanId? CaravanId { get; }
        public WorldPosition Position { get; }
        public CaptivityState? Captivity { get; }
        public bool IsActiveMovement => Kind == CharacterLocationKind.Travelling;

        public static CharacterLocation InCity(CityId id) { Require(id.IsValid); return new CharacterLocation(CharacterLocationKind.City, id, null, null, default, null); }
        public static CharacterLocation WithArmy(ArmyId id) { Require(id.IsValid); return new CharacterLocation(CharacterLocationKind.Army, null, id, null, default, null); }
        public static CharacterLocation WithCaravan(CaravanId id) { Require(id.IsValid); return new CharacterLocation(CharacterLocationKind.Caravan, null, null, id, default, null); }
        public static CharacterLocation At(WorldPosition position) => new CharacterLocation(CharacterLocationKind.WorldPosition, null, null, null, position, null);
        public static CharacterLocation TravellingAt(WorldPosition position) => new CharacterLocation(CharacterLocationKind.Travelling, null, null, null, position, null);
        public static CharacterLocation Captive(CaptivityState state) => new CharacterLocation(CharacterLocationKind.Captivity, null, null, null, default, state ?? throw new ArgumentNullException(nameof(state)));

        private static void Require(bool isValid)
        {
            if (!isValid) throw new ArgumentException("Location target identifier is invalid.");
        }
    }
}
