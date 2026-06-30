using Buelo.Contracts;

namespace Buelo.Api;

/// <summary>
/// First-run seeding. When the database is empty, imports the shipped on-disk definitions
/// (<c>definitions/{kind}/{name}.*</c>) into the (now database-backed) definition store, so the
/// bundled example reports/modules/data are available out of the box. The shipped files thus
/// become seed data; the database is the source of truth thereafter.
/// </summary>
public static class BueloSeeding
{
    private const string MarkerKind = "_system";
    private const string MarkerName = "seeded";

    public static async Task SeedBueloContentFromDiskAsync(this IServiceProvider services, IConfiguration configuration)
    {
        using var scope = services.CreateScope();
        var definitions = scope.ServiceProvider.GetRequiredService<IDefinitionStore>();

        // Seed once. The marker keeps user deletions from resurrecting on the next boot.
        if (await definitions.ReadAsync(MarkerKind, MarkerName) is not null)
            return;

        var definitionRoot = configuration["Buelo:DefinitionStorePath"] ?? "definitions";
        if (Directory.Exists(definitionRoot))
        {
            foreach (var kindDir in Directory.EnumerateDirectories(definitionRoot))
            {
                var kind = Path.GetFileName(kindDir);
                foreach (var file in Directory.EnumerateFiles(kindDir))
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    if (string.IsNullOrEmpty(name) || await definitions.ReadAsync(kind, name) is not null)
                        continue;

                    var content = await File.ReadAllTextAsync(file);
                    await definitions.SaveAsync(kind, name, content);
                }
            }
        }

        await definitions.SaveAsync(MarkerKind, MarkerName, "true");
    }
}
