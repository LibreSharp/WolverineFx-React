using System.Buffers.Binary;
using System.Formats.Cbor;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.WebUtilities;

namespace ApiHost.Tests;

// A passkey that lives in the test: it answers the API's WebAuthn options the way a browser and
// authenticator would (navigator.credentials.create and .get), with an ES256 key and "none"
// attestation, so the tests exercise Identity's real checks of challenges, origins and signatures.
// (No method is called Create or Apply: Marten's event source generator would take the class for
// an event-sourced aggregate.)
public sealed class SoftwarePasskey : IDisposable
{
    // The page's origin as the browser reports it: the Vite dev server (App:ClientUrl).
    public const string Origin = "http://localhost:5173";

    private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private readonly byte[] _credentialId = RandomNumberGenerator.GetBytes(32);
    private string _userHandle = string.Empty;
    private uint _signCount;

    public string Id => WebEncoders.Base64UrlEncode(_credentialId);

    // Answers creation options (from POST /api/account/passkeys/options) with a new credential.
    public string Register(string optionsJson)
    {
        var options = JsonNode.Parse(optionsJson)!;
        _userHandle = options["user"]!["id"]!.GetValue<string>();
        var clientData = ClientData("webauthn.create", options["challenge"]!.GetValue<string>());

        var parameters = _key.ExportParameters(includePrivateParameters: false);
        var publicKey = new CborWriter();
        publicKey.WriteStartMap(5);
        publicKey.WriteInt32(1); publicKey.WriteInt32(2);    // kty: EC2
        publicKey.WriteInt32(3); publicKey.WriteInt32(-7);   // alg: ES256
        publicKey.WriteInt32(-1); publicKey.WriteInt32(1);   // crv: P-256
        publicKey.WriteInt32(-2); publicKey.WriteByteString(parameters.Q.X!);
        publicKey.WriteInt32(-3); publicKey.WriteByteString(parameters.Q.Y!);
        publicKey.WriteEndMap();

        // Flags: user present, user verified, attested credential data included.
        var credentialIdLength = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(credentialIdLength, (ushort)_credentialId.Length);
        var authenticatorData = Concat(
            RpIdHash(options["rp"]!["id"]!.GetValue<string>()), [0x45], SignCount(),
            new byte[16], credentialIdLength, _credentialId, publicKey.Encode());

        var attestation = new CborWriter();
        attestation.WriteStartMap(3);
        attestation.WriteTextString("fmt"); attestation.WriteTextString("none");
        attestation.WriteTextString("attStmt"); attestation.WriteStartMap(0); attestation.WriteEndMap();
        attestation.WriteTextString("authData"); attestation.WriteByteString(authenticatorData);
        attestation.WriteEndMap();

        return JsonSerializer.Serialize(new
        {
            id = Id,
            rawId = Id,
            type = "public-key",
            authenticatorAttachment = "platform",
            clientExtensionResults = new { },
            response = new
            {
                clientDataJSON = WebEncoders.Base64UrlEncode(clientData),
                attestationObject = WebEncoders.Base64UrlEncode(attestation.Encode()),
                transports = new[] { "internal" },
            },
        });
    }

    // Answers request options (from POST /api/auth/passkey/options) by signing the challenge.
    public string Authenticate(string optionsJson, string? origin = null)
    {
        var options = JsonNode.Parse(optionsJson)!;
        var clientData = ClientData("webauthn.get", options["challenge"]!.GetValue<string>(), origin);
        var authenticatorData = Concat(RpIdHash(options["rpId"]!.GetValue<string>()), [0x05], SignCount());
        var signature = _key.SignData(Concat(authenticatorData, SHA256.HashData(clientData)),
            HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);

        return JsonSerializer.Serialize(new
        {
            id = Id,
            rawId = Id,
            type = "public-key",
            authenticatorAttachment = "platform",
            clientExtensionResults = new { },
            response = new
            {
                clientDataJSON = WebEncoders.Base64UrlEncode(clientData),
                authenticatorData = WebEncoders.Base64UrlEncode(authenticatorData),
                signature = WebEncoders.Base64UrlEncode(signature),
                userHandle = _userHandle,
            },
        });
    }

    public void Dispose() => _key.Dispose();

    private static byte[] ClientData(string type, string challenge, string? origin = null)
        => Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { type, challenge, origin = origin ?? Origin, crossOrigin = false }));

    private static byte[] RpIdHash(string rpId) => SHA256.HashData(Encoding.UTF8.GetBytes(rpId));

    private byte[] SignCount()
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, ++_signCount);
        return bytes;
    }

    private static byte[] Concat(params byte[][] parts) => [.. parts.SelectMany(p => p)];
}
