using System.Net.Http.Headers;
using Pdv.Application.Configuration;

namespace Pdv.Infrastructure.Api;

internal static class PdvApiRequestHeaders
{
    public static void Apply(HttpRequestMessage request, PdvOptions options)
    {
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        if (!string.IsNullOrWhiteSpace(options.TerminalToken))
        {
            request.Headers.Remove("x-pdv-token");
            request.Headers.Add("x-pdv-token", options.TerminalToken.Trim());
        }

        if (string.IsNullOrWhiteSpace(options.SupabaseAnonKey))
        {
            return;
        }

        var authorizationToken = options.SupabaseAnonKey.Trim();
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authorizationToken);
        request.Headers.Remove("apikey");
        request.Headers.TryAddWithoutValidation("apikey", authorizationToken);
    }
}
