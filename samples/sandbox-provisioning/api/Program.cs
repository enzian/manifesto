using Manifesto.AspNet;

var builder = WebApplication.CreateBuilder(args);

builder.AddManifestoV1();

var app = builder.Build();

var keyspaces = (string kind, string version, string group) =>
    (kind, group, version) switch
    {
        ("sandbox", "infra.developer.io", "v1alpha1") => $"/registry/sandboxes",
        ("sandboxes", "infra.developer.io", "v1alpha1") => $"/registry/sandboxes",
        _ => string.Empty
    };
app.UseManifestoV1(keyspaces);

await app.RunAsync();
