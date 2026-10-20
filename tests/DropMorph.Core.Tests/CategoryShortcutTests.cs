using DropMorph.Core.Presets;
using DropMorph.Core.Profiles;
using DropMorph.Core.Settings;

namespace DropMorph.Core.Tests;

public class CategoryShortcutTests
{
    [Fact]
    public void Defaults_have_two_key_shortcuts_for_images_documents_video_and_any_files()
    {
        var categories = TriggerGesture.Defaults().Where(t => t.ProfileId is not null).ToList();
        Assert.Equal(["images", "office", "video", "mixed"], categories.Select(t => t.ProfileId));
        Assert.Equal(["I", "D", "V", "O"], categories.Select(t => t.Key));
        Assert.All(categories, t =>
        {
            Assert.Equal(TriggerKind.KeyChord, t.Kind);
            Assert.Equal(ModifierKeys.Alt, t.Modifiers);
            Assert.Equal(TriggerScope.FileManager, t.Scope);
        });
        // Each names a toolbar the app has.
        var profiles = BuiltInData.CreateProfiles().Select(p => p.Id).ToHashSet();
        Assert.All(categories, t => Assert.Contains(t.ProfileId!, profiles));
    }

    [Fact]
    public void A_category_shortcut_opens_its_toolbar_for_the_files_of_its_type()
    {
        var profiles = BuiltInData.CreateProfiles();
        string[] files = ["/a/photo.jpg", "/a/report.docx", "/a/clip.mp4"];

        var (images, imageFiles) = ProfileSelector.ForShortcut(profiles, files, "images");
        Assert.Equal("images", images!.Id);
        Assert.Equal(["/a/photo.jpg"], imageFiles);

        var (video, videoFiles) = ProfileSelector.ForShortcut(profiles, files, "video");
        Assert.Equal("video", video!.Id);
        Assert.Equal(["/a/clip.mp4"], videoFiles);

        // None of its type: the toolbar still opens, for every selected file.
        var (documents, documentFiles) = ProfileSelector.ForShortcut(profiles, ["/a/photo.jpg"], "office");
        Assert.Equal("office", documents!.Id);
        Assert.Equal(["/a/photo.jpg"], documentFiles);

        // The mixed-files toolbar takes the whole selection.
        var (mixed, mixedFiles) = ProfileSelector.ForShortcut(profiles, files, "mixed");
        Assert.True(mixed!.IsFallback);
        Assert.Equal(files, mixedFiles);
    }

    [Fact]
    public void Without_a_profile_or_with_a_deleted_one_the_files_choose_the_toolbar()
    {
        var profiles = BuiltInData.CreateProfiles();
        string[] files = ["/a/photo.jpg"];
        Assert.Equal("images", ProfileSelector.ForShortcut(profiles, files, null).Profile!.Id);
        Assert.Equal("images", ProfileSelector.ForShortcut(profiles, files, "deleted").Profile!.Id);
        Assert.Equal(files, ProfileSelector.ForShortcut(profiles, files, "deleted").Files);
    }

    [Fact]
    public void Upgrading_adds_the_category_shortcuts_once_and_keeps_the_users_keys()
    {
        using var dir = new TempDir();
        var store = new UserDataStore(dir.Path);
        store.Load();
        // An older DropMorph: no category shortcuts, and the user put something else on Alt + V.
        store.Settings.Triggers =
        [
            new() { Kind = TriggerKind.Drag, Modifiers = ModifierKeys.Ctrl, Button = TriggerMouseButton.Left },
            new() { Kind = TriggerKind.KeyChord, Modifiers = ModifierKeys.Alt, Button = TriggerMouseButton.None, Key = "V" },
        ];
        store.SaveSettings();
        var path = Path.Combine(dir.Path, "settings.json");
        var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        json["schemaVersion"] = 28;
        File.WriteAllText(path, json.ToJsonString());

        var migrated = new UserDataStore(dir.Path);
        migrated.Load();
        var triggers = migrated.Settings.Triggers;
        Assert.Equal(5, triggers.Count);
        Assert.Null(triggers[1].ProfileId);
        Assert.Equal(["images", "office", "mixed"], triggers.Skip(2).Select(t => t.ProfileId));

        // Saved: loading again adds nothing.
        var reloaded = new UserDataStore(dir.Path);
        reloaded.Load();
        Assert.Equal(5, reloaded.Settings.Triggers.Count);
        Assert.Equal("office", reloaded.Settings.Triggers[3].ProfileId);
    }
}
