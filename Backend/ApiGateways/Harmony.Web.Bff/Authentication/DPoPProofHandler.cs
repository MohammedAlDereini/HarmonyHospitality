namespace Harmony.Web.Bff.Authentication;

using System.Net;
using System.Net.Http.Headers;

/// <summary>
/// Puts a DPoP proof on every call that leaves through it: the token calls (code redemption, refresh), which carry no
/// token yet, and any call that already carries a bound token under the DPoP scheme, whose hash then goes into the
/// proof. When the server answers "use_dpop_nonce" with a DPoP-Nonce header (RFC 9449 §8), the call is made once more
/// with that nonce inside the proof; the nonce is remembered for the calls after it.
/// </summary>
public sealed class DPoPProofHandler : DelegatingHandler
{
    private const string UseNonceError = "use_dpop_nonce";
    private const string NonceHeader = "DPoP-Nonce";

    private readonly DPoPProofFactory proofs;
    private string? nonce;

    public DPoPProofHandler(DPoPProofFactory proofs)
    {
        this.proofs = proofs;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        this.Prove(request, this.nonce);
        var response = await base.SendAsync(request, cancellationToken);
        if (!await WantsNonceAsync(response, cancellationToken) || !response.Headers.TryGetValues(NonceHeader, out var values))
        {
            return response;
        }

        // One more try, with the nonce the server just handed out; a second refusal is the caller's to read.
        this.nonce = values.FirstOrDefault();
        response.Dispose();
        this.Prove(request, this.nonce);
        return await base.SendAsync(request, cancellationToken);
    }

    private void Prove(HttpRequestMessage request, string? withNonce)
    {
        var accessToken = request.Headers.Authorization is { Scheme: DPoPProofFactory.HeaderName } authorization ? authorization.Parameter : null;
        request.Headers.Remove(DPoPProofFactory.HeaderName);
        request.Headers.TryAddWithoutValidation(DPoPProofFactory.HeaderName, this.proofs.Create(request.Method, request.RequestUri!, accessToken, withNonce));
    }

    private static async Task<bool> WantsNonceAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.BadRequest && response.Content.Headers.ContentType?.MediaType == "application/json")
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return body.Contains(UseNonceError, StringComparison.Ordinal);
        }

        return response.StatusCode == HttpStatusCode.Unauthorized
            && response.Headers.WwwAuthenticate.Any(h => h.Scheme == DPoPProofFactory.HeaderName && h.Parameter?.Contains(UseNonceError, StringComparison.Ordinal) == true);
    }
}
