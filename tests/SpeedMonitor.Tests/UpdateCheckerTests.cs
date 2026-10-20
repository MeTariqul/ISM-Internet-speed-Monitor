using SpeedMonitor.Services;

namespace SpeedMonitor.Tests;

public sealed class UpdateCheckerTests
{
    private const string Release = """
        {
          "tag_name": "v1.5.0",
          "assets": [
            { "name": "ISM-Internet-Speed-Monitor-win-x64.zip", "browser_download_url": "https://example.test/zip" },
            { "name": "ISM-Internet-Speed-Monitor.exe", "browser_download_url": "https://example.test/exe" },
            { "name": "SHA256SUMS.txt", "browser_download_url": "https://example.test/sums" }
          ]
        }
        """;

    [Fact]
    public void Newer_release_is_parsed_with_both_assets()
    {
        UpdateInfo? update = UpdateChecker.ParseLatestRelease(Release, new Version(1, 4, 3));

        Assert.NotNull(update);
        Assert.Equal(new Version(1, 5, 0), update!.Version);
        Assert.Equal("v1.5.0", update.Tag);
        Assert.Equal("https://example.test/exe", update.ExeUrl);
        Assert.Equal("https://example.test/sums", update.ChecksumsUrl);
    }

    [Fact]
    public void Same_version_is_not_an_update()
    {
        Assert.Null(UpdateChecker.ParseLatestRelease(Release, new Version(1, 5, 0)));
    }

    [Fact]
    public void Assembly_style_four_part_version_is_not_newer_than_the_tag()
    {
        Assert.Null(UpdateChecker.ParseLatestRelease(Release, new Version(1, 5, 0, 0)));
    }

    [Theory]
    [InlineData("v1.4.2")]
    [InlineData("v1.4.0")]
    [InlineData("v1.0.0")]
    public void Older_release_is_not_an_update(string tag)
    {
        string json = Release.Replace("\"v1.5.0\"", $"\"{tag}\"");

        Assert.Null(UpdateChecker.ParseLatestRelease(json, new Version(1, 4, 3)));
    }

    [Fact]
    public void Missing_exe_asset_is_ignored()
    {
        const string json = """
            {
              "tag_name": "v9.9.9",
              "assets": [
                { "name": "SHA256SUMS.txt", "browser_download_url": "https://example.test/sums" }
              ]
            }
            """;

        Assert.Null(UpdateChecker.ParseLatestRelease(json, new Version(1, 4, 3)));
    }

    [Fact]
    public void Release_without_checksums_still_parses()
    {
        const string json = """
            {
              "tag_name": "v9.9.9",
              "assets": [
                { "name": "ISM-Internet-Speed-Monitor.exe", "browser_download_url": "https://example.test/exe" }
              ]
            }
            """;

        UpdateInfo? update = UpdateChecker.ParseLatestRelease(json, new Version(1, 4, 3));

        Assert.NotNull(update);
        Assert.Null(update!.ChecksumsUrl);
    }

    [Fact]
    public void Tag_without_v_prefix_parses()
    {
        Assert.Equal(new Version(2, 0, 1), UpdateChecker.ParseTag("2.0.1"));
        Assert.Equal(new Version(2, 0, 1), UpdateChecker.ParseTag("V2.0.1"));
        Assert.Equal(new Version(2, 0, 1), UpdateChecker.ParseTag("  v2.0.1  "));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("latest")]
    [InlineData("v1")]
    [InlineData("v1.5.0-beta")]
    public void Prerelease_and_malformed_tags_do_not_parse(string? tag)
    {
        Assert.Null(UpdateChecker.ParseTag(tag));
    }

    [Fact]
    public void Two_component_tags_parse_like_the_framework_does()
    {
        // Version.Parse("1.5") succeeds; keep the behaviour consistent with the BCL.
        Assert.Equal(new Version(1, 5), UpdateChecker.ParseTag("v1.5"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("""{"tag_name":"v9.9.9"}""")]
    [InlineData("""{"tag_name":"v9.9.9","assets":{}}""")]
    [InlineData("""{"assets":[]}""")]
    public void Broken_payloads_are_ignored(string? json)
    {
        Assert.Null(UpdateChecker.ParseLatestRelease(json, new Version(1, 4, 3)));
    }

    [Theory]
    [InlineData("1.5.0", "1.4.3", true)]
    [InlineData("1.4.4", "1.4.3.0", true)]
    [InlineData("1.4.3", "1.4.3.0", false)]
    [InlineData("1.4.3.0", "1.4.3", false)]
    [InlineData("1.4.2", "1.4.3", false)]
    [InlineData("2.0.0", "1.99.99", true)]
    public void Version_comparison_ignores_missing_components(string candidate, string current, bool newer)
    {
        Assert.Equal(newer, UpdateChecker.IsNewer(Version.Parse(candidate), Version.Parse(current)));
    }

    [Fact]
    public void Checksum_is_found_in_a_sums_file()
    {
        const string hash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        string sums =
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa  ISM-Internet-Speed-Monitor-win-x64.zip\r\n"
            + $"{hash}  ISM-Internet-Speed-Monitor.exe\r\n";

        Assert.Equal(hash, UpdateChecker.FindChecksum(sums, "ISM-Internet-Speed-Monitor.exe"));

        // sha256sum -b writes an asterisk before the file name.
        Assert.Equal(hash, UpdateChecker.FindChecksum($"{hash} *ISM-Internet-Speed-Monitor.exe", "ISM-Internet-Speed-Monitor.exe"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa  other.exe")]
    [InlineData("not-a-hash  ISM-Internet-Speed-Monitor.exe")]
    public void Missing_checksum_returns_null(string? sums)
    {
        Assert.Null(UpdateChecker.FindChecksum(sums, "ISM-Internet-Speed-Monitor.exe"));
    }

    [Fact]
    public void Staged_path_sits_next_to_the_running_exe()
    {
        string staged = UpdateApplier.StagedPathFor(@"C:\Apps\ISM-Internet-Speed-Monitor.exe");

        Assert.Equal(@"C:\Apps\ISM-Internet-Speed-Monitor.exe.update.exe", staged);
        Assert.Equal(@"C:\Apps\ISM-Internet-Speed-Monitor.exe.old", @"C:\Apps\ISM-Internet-Speed-Monitor.exe" + UpdateApplier.OldSuffix);
    }
}
