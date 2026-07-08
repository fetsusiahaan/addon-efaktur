namespace BlazorApp1.Models;

public class DbConnectionEntry
{
    public string Id          { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Nama        { get; set; } = "";
    public string JenisDb     { get; set; } = "SQL Server"; // "SQL Server" | "SAP HANA Database"
    public string ConnString  { get; set; } = "";
    public bool   IsActive    { get; set; } = false;

    // Computed — tidak disimpan

    /// <summary>
    /// Schema diambil dinamis dari connection string sesuai jenis DB:
    /// - SAP HANA  → "Current Schema" (atau "CurrentSchema"/"CS")
    /// - SQL Server → "Database" (atau "Initial Catalog")
    /// Mis. "...;Current Schema=PEN_1706_T;" atau "...;Database=SBODEMO;" → nilainya.
    /// </summary>
    public string Schema
    {
        get
        {
            var keys = JenisDb == "SAP HANA Database"
                ? new[] { "CurrentSchema", "CS", "Schema" }
                : new[] { "Database", "InitialCatalog" };

            foreach (var part in ConnString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var eq = part.IndexOf('=');
                if (eq <= 0) continue;
                var key = part[..eq].Replace(" ", "");          // "Current Schema" → "CurrentSchema"
                var val = part[(eq + 1)..].Trim();
                if (keys.Any(k => key.Equals(k, StringComparison.OrdinalIgnoreCase)))
                    return val;
            }
            return "";
        }
    }

    public string ProviderName => JenisDb == "SAP HANA Database"
        ? "Sap.Data.Hana"
        : "System.Data.SqlClient";

    public string FormatHint => JenisDb == "SAP HANA Database"
        ? "Server=host:port;UID=...;PWD=...;databaseName=TENANT;encrypt=True;"
        : "Server=host;Database=...;User Id=...;Password=...;";

    public string IconClass => JenisDb == "SAP HANA Database"
        ? "bi-hdd-stack"
        : "bi-server";
}
