namespace LeccionesAprendidas.Models
{
    public class AzureSearchOptions
    {
        public string Endpoint { get; set; } = default!;
        public string AdminKey { get; set; } = default!;
        public string IndexName { get; set; } = default!;

        // Pipeline (skillset + indexer)
        public string CognitiveServicesKey { get; set; } = default!;
        public string CognitiveServicesEndpoint { get; set; } = default!;
        public string FormatearSuggestUrl { get; set; } = default!;
        public string CosmosDbConnectionString { get; set; } = default!;
        public string DataSourceName { get; set; } = "lecciones-datasource";
        public string SkillsetName { get; set; } = "lecciones-skillset";
        public string IndexerName { get; set; } = "lecciones-indexer";
    }
}
