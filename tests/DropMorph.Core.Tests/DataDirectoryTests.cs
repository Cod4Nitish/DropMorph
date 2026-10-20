// DROPMORPH_DATA_DIR moves the data folder, so a development or test copy never reads or writes the user's own DropMorph.

using DropMorph.Core.Settings;

namespace DropMorph.Core.Tests;

public class DataDirectoryTests
{
    [Fact]
    public void The_variable_moves_the_default_data_folder()
    {
        using var dir = new TempDir();
        var before = Environment.GetEnvironmentVariable(UserDataStore.DirectoryVariable);
        try
        {
            Environment.SetEnvironmentVariable(UserDataStore.DirectoryVariable, dir.Path + Path.DirectorySeparatorChar);
            Assert.Equal(Path.GetFullPath(dir.Path), UserDataStore.OverrideDirectory);
            Assert.Equal(Path.GetFullPath(dir.Path), UserDataStore.DefaultDirectory);

            Environment.SetEnvironmentVariable(UserDataStore.DirectoryVariable, null);
            Assert.Null(UserDataStore.OverrideDirectory);
            Assert.Equal(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DropMorph"),
                UserDataStore.DefaultDirectory);
        }
        finally
        {
            Environment.SetEnvironmentVariable(UserDataStore.DirectoryVariable, before);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void An_empty_variable_is_no_override(string? value) => Assert.Null(UserDataStore.DirectoryFrom(value));

    [Fact]
    public void A_relative_folder_is_made_absolute() =>
        Assert.Equal(Path.Combine(Environment.CurrentDirectory, "dropmorph-test"), UserDataStore.DirectoryFrom("dropmorph-test"));
}
