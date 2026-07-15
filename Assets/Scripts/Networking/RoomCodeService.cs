using System.Text;
using UnityEngine;

public static class RoomCodeService
{
    public const int CodeLength = 6;
    public const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static string Generate()
    {
        StringBuilder builder = new StringBuilder(CodeLength);
        for (int i = 0; i < CodeLength; i++)
            builder.Append(Alphabet[Random.Range(0, Alphabet.Length)]);

        return builder.ToString();
    }

    public static string Normalize(string roomCode)
    {
        return string.IsNullOrWhiteSpace(roomCode)
            ? string.Empty
            : roomCode.Trim().ToUpperInvariant();
    }

    public static bool IsValid(string roomCode)
    {
        string normalized = Normalize(roomCode);
        if (normalized.Length != CodeLength)
            return false;

        for (int i = 0; i < normalized.Length; i++)
        {
            if (Alphabet.IndexOf(normalized[i]) < 0)
                return false;
        }

        return true;
    }
}
