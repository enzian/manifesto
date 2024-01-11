using System.Security.Claims;
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
var ttl = (string kind, string version, string group) => (long?)null;
var isAuthorized = (string kind, string version, string group, string verb, ClaimsPrincipal identity) => true;
app.UseManifestoV1(keyspaces, ttl, isAuthorized);

await app.RunAsync();
