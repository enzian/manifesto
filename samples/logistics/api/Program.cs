using System.Net;
using System.Security.Claims;
using dotnet_etcd;
using dotnet_etcd.interfaces;
using Manifesto.AspNet;

var builder = WebApplication.CreateBuilder(args);
HttpClient.DefaultProxy = new WebProxy();

builder.AddManifestoV1();
builder.Services.AddSingleton<IEtcdClient>(new EtcdClient("http://localhost:2379"));

var keyspaces = (string group, string version, string kind) => {
        return  (kind, group, version) switch {
            ("stock", "logistics.stockr.io", "v1alpha1") => $"/registry/stocks",
            ("stocks", "logistics.stockr.io", "v1alpha1") => $"/registry/stocks",
            ("location", "logistics.stockr.io", "v1alpha1") => $"/registry/locations",
            ("locations", "logistics.stockr.io", "v1alpha1") => $"/registry/locations",
            ("transport", "logistics.stockr.io", "v1alpha1") => $"/registry/transports",
            ("transports", "logistics.stockr.io", "v1alpha1") => $"/registry/transports",
            ("production-order", "logistics.stockr.io", "v1alpha1") => $"/registry/production-orders",
            ("production-orders", "logistics.stockr.io", "v1alpha1") => $"/registry/production-orders",
            ("event", "events.stockr.io", "v1") => $"/registry/events",
            ("events", "events.stockr.io", "v1") => $"/registry/events",
            _ => string.Empty
        };
    };

var ttl = (string group, string version, string kind) => {
        return  (kind, group, version) switch {
            ("event", "events.stockr.io", "v1") => (long?)120,
            ("events", "events.stockr.io", "v1") => (long?)120,
            _ => null
        };
    };
var isAuth = (string _, string _, string _, string _, ClaimsPrincipal _) => {
        return true;
    };

var app = builder.Build();

app.UseManifestoV1(keyspaces, ttl, isAuth);

await app.RunAsync();
