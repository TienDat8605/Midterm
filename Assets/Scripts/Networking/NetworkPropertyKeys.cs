public static class NetworkPropertyKeys
{
    public const string SchemaVersion = "v";
    public const string SaveOwner = "so";
    public const string Phase = "phase";
    public const string Map = "map";
    public const string MapRevision = "maprev";
    public const string Mode = "mode";
    public const string Checkpoint = "cp";
    public const string Eggs = "eggs";
    public const string Shop = "shop";
    public const string Revision = "rev";
    public const string AnchorReservation = "ra";
    public const string BounceReservation = "rb";
    public const string StickyReservation = "rs";

    public const string PlayerRole = "role";
    public const string PlayerReady = "ready";
    public const string PlayerItem = "item";
    public const string PlayerConnection = "conn";
    public const string PlayerLoaded = "loaded";
    public const string PlayerPing = "ping";
    public const string PlayerMapAcknowledgement = "mapack";

    public static string ReservationKey(SlimeRole role)
    {
        switch (role)
        {
            case SlimeRole.Anchor:
                return AnchorReservation;
            case SlimeRole.Bounce:
                return BounceReservation;
            case SlimeRole.Sticky:
                return StickyReservation;
            default:
                return string.Empty;
        }
    }
}
