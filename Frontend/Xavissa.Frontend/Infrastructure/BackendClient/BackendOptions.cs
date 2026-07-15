using System;

namespace Xavissa.Frontend.Services;

public sealed class BackendOptions
{
    public string BaseUrl { get; set; } = "http://localhost:5087/";

    public Uri BaseUri
    {
        get
        {
            var value = string.IsNullOrWhiteSpace(BaseUrl) ? "http://localhost:5087/" : BaseUrl.Trim();
            if (!value.EndsWith("/", StringComparison.Ordinal))
                value += "/";
            return new Uri(value, UriKind.Absolute);
        }
    }
}
