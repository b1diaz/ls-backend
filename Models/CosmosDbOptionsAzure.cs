namespace LeccionesAprendidas.Models
{
    public class CosmosDbOptions
    {
        public string Endpoint { get; set; } = default!;
        public string Key { get; set; } = default!;
        public string DatabaseName { get; set; } = default!;
        public string ContainerName { get; set; } = default!;
    }
}
