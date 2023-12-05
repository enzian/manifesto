using dotnet_etcd;
using dotnet_etcd.interfaces;
using Manifesto.AspNet;

var builder = WebApplication.CreateBuilder(args);

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

var app = builder.Build();

app.UseManifestoV1(keyspaces);

await app.RunAsync();
