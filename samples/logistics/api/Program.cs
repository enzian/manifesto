using Manifesto.AspNet;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddManifesto()
    .AddKeySpaces((string kind, string version, string group) => {
        return  (kind, group, version) switch {
            ("stock", "logistics.stockr.io", "v1alpha1") => $"/registry/stocks",
            ("stocks", "logistics.stockr.io", "v1alpha1") => $"/registry/stocks",
            ("location", "logistics.stockr.io", "v1alpha1") => $"/registry/locations",
            ("locations", "logistics.stockr.io", "v1alpha1") => $"/registry/locations",
            _ => string.Empty
        };
});

var app = builder.Build();

app.UseAuthorization();

app.MapManifestApiControllers();

await app.RunAsync();
