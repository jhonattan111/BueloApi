using Buelo.Persistence;

namespace Buelo.Tests.Persistence;

public class EfWorkspaceStoreTests
{
    [Fact]
    public async Task Create_get_and_update_file()
    {
        using var factory = new TestDbFactory();
        var store = new EfWorkspaceStore(factory);

        var created = await store.CreateFileAsync("reports/invoice.report.yml", "kind: report");
        Assert.Equal("reports/invoice.report.yml", created.Path);
        Assert.Equal("invoice.report.yml", created.Name);
        Assert.Equal(".yml", created.Extension);

        var fetched = await store.GetFileAsync("reports/invoice.report.yml");
        Assert.NotNull(fetched);
        Assert.Equal("kind: report", fetched!.Content);

        await store.UpdateFileAsync("reports/invoice.report.yml", "kind: report\nname: invoice");
        Assert.Equal("kind: report\nname: invoice", (await store.GetFileAsync("reports/invoice.report.yml"))!.Content);
    }

    [Fact]
    public async Task Create_existing_without_overwrite_throws()
    {
        using var factory = new TestDbFactory();
        var store = new EfWorkspaceStore(factory);

        await store.CreateFileAsync("a.txt", "1");
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.CreateFileAsync("a.txt", "2"));
        await store.CreateFileAsync("a.txt", "2", overwrite: true);
        Assert.Equal("2", (await store.GetFileAsync("a.txt"))!.Content);
    }

    [Fact]
    public async Task Update_missing_without_flag_throws_but_creates_with_flag()
    {
        using var factory = new TestDbFactory();
        var store = new EfWorkspaceStore(factory);

        await Assert.ThrowsAsync<FileNotFoundException>(() => store.UpdateFileAsync("missing.txt", "x"));
        await store.UpdateFileAsync("missing.txt", "x", createIfMissing: true);
        Assert.Equal("x", (await store.GetFileAsync("missing.txt"))!.Content);
    }

    [Fact]
    public async Task Tree_nests_files_under_synthesized_and_explicit_folders()
    {
        using var factory = new TestDbFactory();
        var store = new EfWorkspaceStore(factory);

        await store.CreateFileAsync("src/a.cs", "");
        await store.CreateFileAsync("src/sub/b.json", "");
        await store.CreateFileAsync("root.txt", "");
        await store.CreateFolderAsync("empty");

        var tree = await store.GetTreeAsync();

        // Folders first (alphabetical), then files: empty, src, root.txt
        Assert.Equal(["empty", "src", "root.txt"], tree.Select(n => n.Name).ToArray());

        var src = tree.Single(n => n.Name == "src");
        Assert.Equal("folder", src.Type);
        Assert.Equal(["sub", "a.cs"], src.Children.Select(n => n.Name).ToArray());

        var empty = tree.Single(n => n.Name == "empty");
        Assert.Empty(empty.Children);
    }

    [Fact]
    public async Task ListFiles_filters_by_extension_and_excludes_folders()
    {
        using var factory = new TestDbFactory();
        var store = new EfWorkspaceStore(factory);

        await store.CreateFileAsync("a.json", "");
        await store.CreateFileAsync("b/c.json", "");
        await store.CreateFileAsync("d.yml", "");
        await store.CreateFolderAsync("folder");

        var json = await store.ListFilesAsync("json");
        Assert.Equal(["a.json", "b/c.json"], json.Select(f => f.Path).ToArray());

        Assert.Equal(3, (await store.ListFilesAsync()).Count);
    }

    [Fact]
    public async Task Move_folder_relocates_whole_subtree()
    {
        using var factory = new TestDbFactory();
        var store = new EfWorkspaceStore(factory);

        await store.CreateFileAsync("a/x.txt", "1");
        await store.CreateFileAsync("a/sub/y.txt", "2");

        await store.MoveAsync("a", "b");

        Assert.Null(await store.GetFileAsync("a/x.txt"));
        Assert.Equal("1", (await store.GetFileAsync("b/x.txt"))!.Content);
        Assert.Equal("2", (await store.GetFileAsync("b/sub/y.txt"))!.Content);
    }

    [Fact]
    public async Task Rename_file_changes_name_in_place()
    {
        using var factory = new TestDbFactory();
        var store = new EfWorkspaceStore(factory);

        await store.CreateFileAsync("dir/old.txt", "data");
        await store.RenameAsync("dir/old.txt", "new.txt");

        Assert.Null(await store.GetFileAsync("dir/old.txt"));
        Assert.Equal("data", (await store.GetFileAsync("dir/new.txt"))!.Content);
    }

    [Fact]
    public async Task Delete_file_and_recursive_folder()
    {
        using var factory = new TestDbFactory();
        var store = new EfWorkspaceStore(factory);

        await store.CreateFileAsync("keep.txt", "");
        await store.CreateFileAsync("gone/a.txt", "");
        await store.CreateFileAsync("gone/b.txt", "");

        await store.DeleteAsync("gone");
        Assert.False(await store.ExistsAsync("gone"));
        Assert.False(await store.ExistsAsync("gone/a.txt"));
        Assert.True(await store.ExistsAsync("keep.txt"));

        await Assert.ThrowsAsync<FileNotFoundException>(() => store.DeleteAsync("nope"));
    }

    [Fact]
    public async Task CreateFolder_then_file_collisions()
    {
        using var factory = new TestDbFactory();
        var store = new EfWorkspaceStore(factory);

        await store.CreateFolderAsync("things");
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.CreateFileAsync("things", "x"));

        await store.CreateFileAsync("note", "x");
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.CreateFolderAsync("note"));
    }
}
