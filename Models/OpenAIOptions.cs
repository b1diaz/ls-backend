namespace LeccionesAprendidas.Models
{
    public class OpenAIOptions
    {
        public string Endpoint { get; set; } = default!;
        public string Key { get; set; } = default!;
        public string EmbeddingDeployment { get; set; } = "text-embedding-3-large";
        public string ChatDeployment { get; set; } = "gpt-4o-mini";
    }
}
