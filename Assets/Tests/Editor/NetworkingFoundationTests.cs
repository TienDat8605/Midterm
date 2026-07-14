#if UNITY_INCLUDE_TESTS
// Pure networking contract tests run in Unity Edit Mode.
using System.Collections.Generic;
using NUnit.Framework;

public sealed class NetworkingFoundationTests
{
    [Test]
    public void GenerateRoomCode_UsesSixUnambiguousCharacters()
    {
        for (int i = 0; i < 100; i++)
        {
            string code = RoomCodeService.Generate();
            Assert.That(code, Has.Length.EqualTo(RoomCodeService.CodeLength));
            Assert.That(RoomCodeService.IsValid(code), Is.True);
            Assert.That(code, Does.Not.Contain("0").And.Not.Contain("O").And.Not.Contain("1").And.Not.Contain("I"));
        }
    }

    [Test]
    public void NormalizeRoomCode_TrimsAndUppercases()
    {
        Assert.That(RoomCodeService.Normalize("  ab2cd3  "), Is.EqualTo("AB2CD3"));
    }

    [Test]
    public void CanStart_RequiresThreeUniqueReadyRoles()
    {
        List<LobbyPlayerState> players = new List<LobbyPlayerState>
        {
            Player(1, SlimeRole.Anchor, true),
            Player(2, SlimeRole.Bounce, true),
            Player(3, SlimeRole.Sticky, true)
        };

        Assert.That(LobbyRules.CanStart(players), Is.True);
    }

    [Test]
    public void CanStart_RejectsMissingReadyOrDuplicateRole()
    {
        List<LobbyPlayerState> notReady = new List<LobbyPlayerState>
        {
            Player(1, SlimeRole.Anchor, true),
            Player(2, SlimeRole.Bounce, false),
            Player(3, SlimeRole.Sticky, true)
        };
        List<LobbyPlayerState> duplicate = new List<LobbyPlayerState>
        {
            Player(1, SlimeRole.Anchor, true),
            Player(2, SlimeRole.Anchor, true),
            Player(3, SlimeRole.Sticky, true)
        };

        Assert.That(LobbyRules.CanStart(notReady), Is.False);
        Assert.That(LobbyRules.CanStart(duplicate), Is.False);
    }

    [Test]
    public void ReservationKeys_AreDistinctForEveryPlayableRole()
    {
        Assert.That(NetworkPropertyKeys.ReservationKey(SlimeRole.Anchor), Is.Not.EqualTo(NetworkPropertyKeys.ReservationKey(SlimeRole.Bounce)));
        Assert.That(NetworkPropertyKeys.ReservationKey(SlimeRole.Bounce), Is.Not.EqualTo(NetworkPropertyKeys.ReservationKey(SlimeRole.Sticky)));
        Assert.That(NetworkPropertyKeys.ReservationKey(SlimeRole.Sticky), Is.Not.Empty);
    }

    private static LobbyPlayerState Player(int actor, SlimeRole role, bool ready)
    {
        return new LobbyPlayerState(actor, $"user-{actor}", $"Player {actor}", role, ready, false, false, actor == 1, 50);
    }
}
#endif
