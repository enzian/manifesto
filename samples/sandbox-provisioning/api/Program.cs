using Manifesto.AspNet;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddManifesto()
    .AddKeySpaces((string kind, string version, string group) => {
        return  (kind, group, version) switch {
            ("sandbox", "infra.developer.io", "v1alpha1") => $"/registry/sandboxes",
            ("sandboxes", "infra.developer.io", "v1alpha1") => $"/registry/sandboxes",
            _ => string.Empty
        };
});

var app = builder.Build();

app.UseAuthorization();

app.MapManifestApiControllers();

await app.RunAsync();
