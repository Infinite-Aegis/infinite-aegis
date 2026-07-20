using Robust.Shared.Serialization;

namespace Content.Shared.Garage;

[Serializable, NetSerializable]
public enum GarageUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class GarageVehicleData
{
    public readonly Guid Id;
    public readonly string PrototypeId;

    public GarageVehicleData(Guid id, string prototypeId)
    {
        Id = id;
        PrototypeId = prototypeId;
    }
}

[Serializable, NetSerializable]
public sealed class GarageBoundUserInterfaceState : BoundUserInterfaceState
{
    public readonly List<GarageVehicleData> Vehicles;

    public GarageBoundUserInterfaceState(List<GarageVehicleData> vehicles)
    {
        Vehicles = vehicles;
    }
}

[Serializable, NetSerializable]
public sealed class GarageRequestUpdateMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class GarageSpawnMessage : BoundUserInterfaceMessage
{
    public readonly Guid VehicleId;

    public GarageSpawnMessage(Guid vehicleId)
    {
        VehicleId = vehicleId;
    }
}
