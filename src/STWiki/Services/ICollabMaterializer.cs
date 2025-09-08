namespace STWiki.Services;

public interface ICollabMaterializer
{
    (string Title, string Summary, string Body, string BodyFormat) Materialize(byte[] snapshotBytes);
}