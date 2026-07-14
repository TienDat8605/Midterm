using System.Collections.Generic;

public static class LobbyRules
{
    public static bool CanStart(IReadOnlyList<LobbyPlayerState> players, int mapRevision = 0)
    {
        if (players == null || players.Count != 3)
            return false;

        bool hasAnchor = false;
        bool hasBounce = false;
        bool hasSticky = false;

        for (int i = 0; i < players.Count; i++)
        {
            LobbyPlayerState player = players[i];
            if (player.IsInactive || !player.IsReady || player.MapAcknowledgement != mapRevision)
                return false;

            switch (player.Role)
            {
                case SlimeRole.Anchor:
                    if (hasAnchor) return false;
                    hasAnchor = true;
                    break;
                case SlimeRole.Bounce:
                    if (hasBounce) return false;
                    hasBounce = true;
                    break;
                case SlimeRole.Sticky:
                    if (hasSticky) return false;
                    hasSticky = true;
                    break;
                default:
                    return false;
            }
        }

        return hasAnchor && hasBounce && hasSticky;
    }
}
