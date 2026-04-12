namespace Cashflow.Shared.Storage.AzureBlob
{
    public sealed class AzureBlobStorageOptions
    {
        /// <summary>Full connection string. Mutually exclusive with AccountName+UseManagedIdentity.</summary>
        public string? ConnectionString { get; set; }

        /// <summary>Storage account name (e.g. "cashflowstorage"). Used with Managed Identity.</summary>
        public string? AccountName { get; set; }

        /// <summary>When true, authenticates with DefaultAzureCredential instead of a connection string.</summary>
        public bool UseManagedIdentity { get; set; }

        /// <summary>Target blob container for report artifacts.</summary>
        public string ContainerName { get; set; } = "reports";
    }
}
