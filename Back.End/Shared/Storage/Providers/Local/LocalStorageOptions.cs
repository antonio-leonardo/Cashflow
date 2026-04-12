namespace Cashflow.Shared.Storage.Local
{
    public sealed class LocalStorageOptions
    {
        public string BasePath { get; set; } = Path.Combine(Path.GetTempPath(), "cashflow-reports");
    }
}
