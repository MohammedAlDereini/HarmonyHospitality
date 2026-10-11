using System.Buffers.Binary;
using System.Text;

namespace Harmony.Identity.Infrastructure.Passkeys;

/// <summary>
/// The makers of passkeys, by the 16-byte AAGUID every creation answer carries inside its attestation object: the name
/// a new passkey gets by itself ("Windows Hello", "Apple Passwords", "YubiKey 5 Series"), the way Microsoft's template
/// and Entra name them. A table in our own code (PasskeyMakers.Names.cs). Attestation is not verified, so the code is
/// what the device SAYS it is: good for a label, never for a security decision. An unknown code gets no name.
/// </summary>
public static partial class PasskeyMakers
{
    private static readonly Guid NoMaker = Guid.Empty;

    public static int Count => Names.Count;

    /// <summary>The maker's name for this code, or null when the code is unknown or all zeros.</summary>
    public static string? NameOf(Guid aaguid)
        => aaguid != NoMaker && Names.TryGetValue(aaguid, out var name) ? name : null;

    /// <summary>The maker's name from a stored attestation object, or null when it cannot be read or is unknown.</summary>
    public static string? NameOf(byte[]? attestationObject)
    {
        var aaguid = AaguidOf(attestationObject);
        return aaguid is null ? null : NameOf(aaguid.Value);
    }

    /// <summary>
    /// The AAGUID inside an attestation object: a CBOR map whose "authData" bytes carry, after the 32-byte RP ID hash,
    /// the flags byte and the 4-byte counter, the 16-byte AAGUID (WebAuthn §6.5.1). Only that one path is read; any other
    /// shape returns null. A real authenticator always writes it; a bad one earns no name, never an exception.
    /// </summary>
    public static Guid? AaguidOf(byte[]? attestationObject)
    {
        if (attestationObject is null || attestationObject.Length < 2)
        {
            return null;
        }

        try
        {
            var authData = AuthDataOf(attestationObject);
            if (authData is null || authData.Value.Length < 37 + 16)
            {
                return null;
            }

            // Attested credential data is present only when the AT flag (0x40) is set.
            var span = authData.Value.Span;
            if ((span[32] & 0x40) == 0)
            {
                return null;
            }

            // WebAuthn writes the AAGUID big-endian; Guid's constructor wants the first three fields little-endian.
            var raw = span.Slice(37, 16);
            var a = BinaryPrimitives.ReadInt32BigEndian(raw[..4]);
            var b = BinaryPrimitives.ReadInt16BigEndian(raw.Slice(4, 2));
            var c = BinaryPrimitives.ReadInt16BigEndian(raw.Slice(6, 2));
            return new Guid(a, b, c, raw[8], raw[9], raw[10], raw[11], raw[12], raw[13], raw[14], raw[15]);
        }
        catch (Exception exception) when (exception is ArgumentException or IndexOutOfRangeException or OverflowException)
        {
            return null;
        }
    }

    // A minimal CBOR walk (RFC 8949) over the top-level map: text keys, and for "authData" the byte string value.
    // Every other value is skipped structurally (definite lengths only, as WebAuthn requires).
    private static ReadOnlyMemory<byte>? AuthDataOf(byte[] bytes)
    {
        var position = 0;
        var (major, count) = Head(bytes, ref position);
        if (major != 5)
        {
            return null;
        }

        for (ulong i = 0; i < count; i++)
        {
            var (keyMajor, keyLength) = Head(bytes, ref position);
            if (keyMajor != 3)
            {
                return null;
            }

            var key = Encoding.UTF8.GetString(bytes, position, checked((int)keyLength));
            position += (int)keyLength;

            var (valueMajor, valueLength) = Head(bytes, ref position);
            if (key == "authData")
            {
                return valueMajor == 2 ? new ReadOnlyMemory<byte>(bytes, position, checked((int)valueLength)) : null;
            }

            Skip(bytes, ref position, valueMajor, valueLength);
        }

        return null;
    }

    private static (int Major, ulong Argument) Head(byte[] bytes, ref int position)
    {
        var initial = bytes[position++];
        var major = initial >> 5;
        var info = initial & 0x1F;
        ulong argument = info switch
        {
            < 24 => (ulong)info,
            24 => bytes[position++],
            25 => Read(bytes, ref position, 2),
            26 => Read(bytes, ref position, 4),
            27 => Read(bytes, ref position, 8),
            _ => throw new ArgumentException("Indefinite-length CBOR is not allowed in WebAuthn."),
        };
        return (major, argument);
    }

    private static ulong Read(byte[] bytes, ref int position, int size)
    {
        ulong value = 0;
        for (var i = 0; i < size; i++)
        {
            value = (value << 8) | bytes[position++];
        }

        return value;
    }

    // Skips one item whose head was read: an integer is done; strings skip their bytes; arrays and maps skip their items.
    private static void Skip(byte[] bytes, ref int position, int major, ulong argument)
    {
        switch (major)
        {
            case 0 or 1 or 7:
                return;
            case 2 or 3:
                position += checked((int)argument);
                return;
            case 4:
                for (ulong i = 0; i < argument; i++)
                {
                    var (m, a) = Head(bytes, ref position);
                    Skip(bytes, ref position, m, a);
                }

                return;
            case 5:
                for (ulong i = 0; i < argument; i++)
                {
                    var (km, ka) = Head(bytes, ref position);
                    Skip(bytes, ref position, km, ka);
                    var (vm, va) = Head(bytes, ref position);
                    Skip(bytes, ref position, vm, va);
                }

                return;
            default:
                // Tags (6): the tagged item follows.
                var (tm, ta) = Head(bytes, ref position);
                Skip(bytes, ref position, tm, ta);
                return;
        }
    }
}
