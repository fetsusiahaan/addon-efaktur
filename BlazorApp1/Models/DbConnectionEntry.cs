namespace BlazorApp1.Models;

public class DbConnectionEntry
{
    public string Id          { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Nama        { get; set; } = "";
    public string JenisDb     { get; set; } = "SQL Server"; // "SQL Server" | "SAP HANA Database"
    public string ConnString  { get; set; } = "";
    public bool   IsActive    { get; set; } = false;

    // Computed — tidak disimpan
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
