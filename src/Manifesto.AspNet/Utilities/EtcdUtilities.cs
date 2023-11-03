
using Manifesto.AspNet.Controllers;

public static class EtcdUtilities
{
    public static string EtcdKvToManifestRevision(this Mvccpb.KeyValue kv) =>
         (kv.ModRevision > 0 ? kv.ModRevision : kv.CreateRevision).ToString();

    public static Manifest AddRevisionFromEtcdKv(this Manifest manifest, Mvccpb.KeyValue kv) =>
        manifest with {
            Metadata = manifest.Metadata with {
                Revision = kv.EtcdKvToManifestRevision()
            }
        };
}