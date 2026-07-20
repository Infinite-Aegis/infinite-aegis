using System.Numerics;
using Robust.Shared.Serialization;

namespace Content.Shared.Venicle;

[Serializable, NetSerializable]
public enum VenicleUiKey : byte
{
    Seats,
}

[Serializable, NetSerializable]
public sealed class VenicleSeatUiData
{
    public readonly string Id;
    public readonly Vector2 Offset;
    public readonly bool Driver;
    public readonly NetEntity? Occupant;
    public readonly string? OccupantName;

    public VenicleSeatUiData(
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
public sealed class VenicleBoundUserInterfaceState : BoundUserInterfaceState
{
    public readonly List<VenicleSeatUiData> Seats;

    public VenicleBoundUserInterfaceState(List<VenicleSeatUiData> seats)
    {
        Seats = seats;
    }
}

[Serializable, NetSerializable]
public sealed class VenicleRequestUpdateMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class VenicleChangeSeatMessage : BoundUserInterfaceMessage
{
    public readonly string SeatId;

    public VenicleChangeSeatMessage(string seatId)
    {
        SeatId = seatId;
    }
}

[Serializable, NetSerializable]
public sealed class VenicleKickOccupantMessage : BoundUserInterfaceMessage
{
    public readonly NetEntity Occupant;

    public VenicleKickOccupantMessage(NetEntity occupant)
    {
        Occupant = occupant;
    }
}
