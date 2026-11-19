using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace MusicApi.IntegrationTests.Infrastructure;

/// <summary>
/// Crea desde cero la base TonyxDb_Test ejecutando los scripts REALES de la carpeta database/.
/// Así las pruebas también verifican que los scripts SQL funcionan.
/// El servidor sale de la variable de entorno TONYX_TEST_SQLSERVER (por defecto, localhost).
/// </summary>
public static partial class TestDatabase
{
    public const string DatabaseName = "TonyxDb_Test";

    private static string ServerConnectionString =>
        Environment.GetEnvironmentVariable("TONYX_TEST_SQLSERVER")
        ?? "Server=localhost;Trusted_Connection=True;TrustServerCertificate=True";

    public static string ConnectionString =>
        new SqlConnectionStringBuilder(ServerConnectionString) { InitialCatalog = DatabaseName }.ConnectionString;

    // "GO" solo en una línea es el separador de lotes de SSMS; SQL Server no lo entiende.
    [GeneratedRegex(@"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex BatchSeparator();

    public static async Task RecreateAsync()
    {
        var scripts = Directory
            .GetFiles(FindScriptsDirectory(), "*.sql")
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .ToList();

        if (scripts.Count == 0)
        {
            throw new InvalidOperationException("No se encontraron scripts SQL en la carpeta database/.");
        }

        var master = new SqlConnectionStringBuilder(ServerConnectionString) { InitialCatalog = "master" };

        await using var connection = new SqlConnection(master.ConnectionString);
        await connection.OpenAsync();

        await ExecuteAsync(
            connection,
            $"IF DB_ID(N'{DatabaseName}') IS NOT NULL BEGIN " +
            $"ALTER DATABASE [{DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
            $"DROP DATABASE [{DatabaseName}]; END",
            "borrar la base de pruebas anterior");

        foreach (var script in scripts)
        {
            // Los scripts nombran la base TonyxDb; aquí se redirigen a TonyxDb_Test.
            var text = (await File.ReadAllTextAsync(script)).Replace("TonyxDb", DatabaseName);

            foreach (var batch in BatchSeparator().Split(text))
            {
                if (!string.IsNullOrWhiteSpace(batch))
                {
                    await ExecuteAsync(connection, batch, $"el script {Path.GetFileName(script)}");
                }
            }
        }
    }

    private static async Task ExecuteAsync(SqlConnection connection, string sql, string context)
    {
        try
        {
            await using var command = new SqlCommand(sql, connection) { CommandTimeout = 120 };
            await command.ExecuteNonQueryAsync();
        }
        catch (SqlException ex)
        {
            var preview = sql.Trim();
            if (preview.Length > 200)
            {
                preview = preview[..200];
            }

            throw new InvalidOperationException($"Falló {context}: {ex.Message}\nLote: {preview}", ex);
        }
    }

    /// <summary>Sube desde la carpeta de las pruebas hasta encontrar MusicApi.slnx.</summary>
    private static string FindScriptsDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MusicApi.slnx")))
            {
                return Path.Combine(directory.FullName, "database");
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("No se encontró MusicApi.slnx subiendo desde la carpeta de pruebas.");
    }
}