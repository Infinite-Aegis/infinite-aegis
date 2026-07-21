using System.Numerics;
using Robust.Shared.Serialization;

namespace Content.Shared.Vehicle;

[Serializable, NetSerializable]
public enum VehicleUiKey : byte
{
    Seats,
}

[Serializable, NetSerializable]
public sealed class VehicleSeatUiData
{
    public readonly string Id;
    public readonly Vector2 Offset;
    public readonly bool Driver;
    public readonly NetEntity? Occupant;
    public readonly string? OccupantName;

    public VehicleSeatUiData(
        string id,
        Vector2 offset,
        bool driver,
        NetEntity? occupant,
        string? occupantName)
    {
        Id = id;
        Offset = offset;
        Driver = driver;
        Occupant = occupant;
        OccupantName = occupantName;
    }
}

[Serializable, NetSerializable]
public sealed class VehicleSeatBoundUserInterfaceState : BoundUserInterfaceState
{
    public readonly List<VehicleSeatUiData> Seats;

    public VehicleSeatBoundUserInterfaceState(List<VehicleSeatUiData> seats)
    {
        Seats = seats;
    }
}

[Serializable, NetSerializable]
public sealed class VehicleSeatRequestUpdateMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class VehicleChangeSeatMessage : BoundUserInterfaceMessage
{
    public readonly string SeatId;

    public VehicleChangeSeatMessage(string seatId)
    {
        SeatId = seatId;
    }
}

[Serializable, NetSerializable]
public sealed class VehicleKickOccupantMessage : BoundUserInterfaceMessage
{
    public readonly NetEntity Occupant;

    public VehicleKickOccupantMessage(NetEntity occupant)
    {
        Occupant = occupant;
    }
}
