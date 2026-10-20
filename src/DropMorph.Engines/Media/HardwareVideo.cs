// Hardware video encoders (NVENC, Quick Sync, AMF, VideoToolbox, VA-API) for H.264 and HEVC: which ones to try on
// which system, and their FFmpeg options. Pure (no processes), so every choice is unit tested without a GPU; whether
// an encoder actually works on this computer is found out by HardwareProbe.

using System.Globalization;
using DropMorph.Core.Presets;

namespace DropMorph.Engines.Media;

/// <summary>The vendor interface a hardware encoder goes through; it decides the encoder's options.</summary>
internal enum HardwareApi
{
    /// <summary>NVIDIA (<c>h264_nvenc</c>, <c>hevc_nvenc</c>).</summary>
    Nvenc,

    /// <summary>Intel Quick Sync Video (<c>h264_qsv</c>, <c>hevc_qsv</c>).</summary>
    Qsv,

    /// <summary>AMD Advanced Media Framework (<c>h264_amf</c>, <c>hevc_amf</c>).</summary>
    Amf,

    /// <summary>Apple VideoToolbox (<c>h264_videotoolbox</c>, <c>hevc_videotoolbox</c>).</summary>
    VideoToolbox,

    /// <summary>Linux VA-API (<c>h264_vaapi</c>, <c>hevc_vaapi</c>): frames are uploaded to the GPU first.</summary>
    Vaapi,
}

/// <summary>How the preset's quality reaches a hardware encoder.</summary>
internal enum HardwareRateControl
{
    /// <summary>A constant quality or quantizer, like x264's CRF.</summary>
    Quality,

    /// <summary>
    /// An average bitrate from the picture size and frame rate: VideoToolbox on Intel Macs has no constant quality mode
    /// (FFmpeg rejects <c>-q:v</c> there).
    /// </summary>
    Bitrate,
}

/// <summary>The operating systems hardware encoders are chosen for.</summary>
internal enum HostPlatform
{
    Windows,
    MacOS,
    Linux,
    Other,
}

/// <summary>One hardware encoder of FFmpeg, with the way it is told the quality.</summary>
/// <param name="Name">FFmpeg encoder name, e.g. <c>hevc_nvenc</c>.</param>
internal sealed record HardwareEncoder(string Name, HardwareApi Api, HardwareRateControl RateControl = HardwareRateControl.Quality)
{
    /// <summary>"h264" or "hevc": the part before the underscore.</summary>
    public string Codec => Name[..Name.IndexOf('_', StringComparison.Ordinal)];
}

/// <summary>Hardware encoder candidates and command-line options.</summary>
internal static class HardwareVideo
{
    /// <summary>
    /// The first DRM render node: the integrated or only GPU on almost every Linux system. A second GPU has
    /// renderD129, but NVIDIA's (the usual second one) is taken through NVENC, which is tried first.
    /// </summary>
    public const string VaapiDevice = "/dev/dri/renderD128";

    /// <summary>The system DropMorph runs on.</summary>
    public static HostPlatform Current { get; } =
        OperatingSystem.IsWindows() ? HostPlatform.Windows
        : OperatingSystem.IsMacOS() ? HostPlatform.MacOS
        : OperatingSystem.IsLinux() ? HostPlatform.Linux
        : HostPlatform.Other;

    /// <summary>
    /// The hardware encoders for <paramref name="codec"/> ("h264" or "hevc") worth trying, best first. Windows:
    /// NVIDIA, then Intel, then AMD (a laptop with an Intel chip and an NVIDIA card encodes on the card). macOS:
    /// VideoToolbox with constant quality (Apple Silicon), then with a bitrate (Intel Macs). Linux: NVIDIA, then
    /// VA-API when a render node exists. Only encoders the FFmpeg in use lists are tried (all of them when the list
    /// could not be read); the probe decides which one really works.
    /// </summary>
    public static IReadOnlyList<HardwareEncoder> Candidates(string codec, HostPlatform platform, IReadOnlySet<string>? encoders, bool vaapiDevice)
    {
        if (codec is not ("h264" or "hevc"))
            return [];
        List<HardwareEncoder> candidates = platform switch
        {
            HostPlatform.Windows => [new($"{codec}_nvenc", HardwareApi.Nvenc), new($"{codec}_qsv", HardwareApi.Qsv), new($"{codec}_amf", HardwareApi.Amf)],
            HostPlatform.MacOS =>
            [
                new($"{codec}_videotoolbox", HardwareApi.VideoToolbox),
                new($"{codec}_videotoolbox", HardwareApi.VideoToolbox, HardwareRateControl.Bitrate),
            ],
            HostPlatform.Linux => vaapiDevice
                ? [new($"{codec}_nvenc", HardwareApi.Nvenc), new($"{codec}_vaapi", HardwareApi.Vaapi)]
                : [new($"{codec}_nvenc", HardwareApi.Nvenc)],
            _ => [],
        };
        return encoders is null ? candidates : [.. candidates.Where(c => encoders.Contains(c.Name))];
    }

    /// <summary>
    /// True when the video is encoded in 10 bits: HEVC keeps 10-bit and HDR sources in Main 10 (as x265 does). H.264
    /// is always 8-bit 4:2:0, the only format every player decodes (as with x264).
    /// </summary>
    public static bool TenBit(string codec, VideoStreamInfo video) => codec == "hevc" && (video.IsHighBitDepth || video.IsHdr);

    /// <summary>
    /// The preset's quality on the encoders' 0–51 quantizer scale (NVENC <c>-cq</c>, Quick Sync <c>-global_quality</c>,
    /// AMF and VA-API <c>-qp</c>): one above x264's CRF 18/23/28 for H.264 and x265's 22/26/30 for HEVC. Hardware
    /// encoders lack x264/x265's psychovisual tuning and spend bits less wisely, so the same number gives a larger
    /// file; one step coarser keeps the size close at a slightly lower quality.
    /// </summary>
    public static int Quantizer(string codec, MediaQuality quality) => codec == "hevc"
        ? Choose(quality, 23, 27, 31)
        : Choose(quality, 19, 24, 29);

    /// <summary>VideoToolbox's constant quality (<c>-q:v</c>, 1–100, higher is better) on Apple Silicon.</summary>
    public static int VideoToolboxQuality(MediaQuality quality) => Choose(quality, 70, 60, 50);

    /// <summary>
    /// The average bitrate (kbit/s) for VideoToolbox on Intel Macs: bits per pixel per frame of the output picture,
    /// e.g. 1080p at 30 fps gets about 9 / 5.6 / 3.1 Mbit/s as H.264 and two thirds of that as HEVC.
    /// </summary>
    public static int BitrateKbps(string codec, MediaQuality quality, VideoStreamInfo video, int maxHeight)
    {
        double height = video.DisplayHeight > 0 ? video.DisplayHeight : 1080;
        if (maxHeight > 0 && height > Math.Max(16, maxHeight))
            height = Math.Max(16, maxHeight);
        var width = height * (video.DisplayAspect > 0 && double.IsFinite(video.DisplayAspect) ? video.DisplayAspect : 16 / 9.0);
        var fps = video.FrameRate is > 0 and <= 240 ? video.FrameRate : 30;
        var bitsPerPixel = Choose(quality, 0.14, 0.09, 0.05) * (codec == "hevc" ? 0.65 : 1);
        return (int)Math.Clamp(Math.Round(width * height * fps * bitsPerPixel / 1000), 300, 100_000);
    }

    /// <summary>Global options before the input: VA-API needs its device.</summary>
    public static string[] InputArguments(HardwareEncoder encoder) =>
        encoder.Api == HardwareApi.Vaapi ? ["-vaapi_device", VaapiDevice] : [];

    /// <summary>
    /// Appended to the video filter chain: VA-API encodes frames in GPU memory, so the scaled frames are converted to
    /// NV12 / P010 and uploaded. The others take frames from system memory (<c>-pix_fmt</c>).
    /// </summary>
    public static string FilterSuffix(HardwareEncoder encoder, bool tenBit) =>
        encoder.Api == HardwareApi.Vaapi ? $",format={(tenBit ? "p010le" : "nv12")},hwupload" : "";

    /// <summary>
    /// The encoder options for one video: rate control from the preset's quality, NV12 (8-bit) or P010 with the Main 10
    /// profile (10-bit HEVC), the source's HDR colour tags, and the <c>hvc1</c> tag Apple players need for HEVC in
    /// MP4/MOV.
    /// </summary>
    public static List<string> CodecArguments(HardwareEncoder encoder, string target, MediaQuality quality, VideoStreamInfo video, int maxHeight)
    {
        var codec = encoder.Codec;
        var tenBit = TenBit(codec, video);
        var quantizer = Text(Quantizer(codec, quality));
        List<string> args = ["-c:v", encoder.Name];
        switch (encoder.Api)
        {
            case HardwareApi.Nvenc:
                // Constant quality inside VBR; -b:v 0 lifts NVENC's default 2 Mbit/s cap. P5 is the "slow, good quality"
                // preset, still many times faster than x264's medium.
                args.AddRange(["-preset", "p5", "-rc", "vbr", "-cq", quantizer, "-b:v", "0"]);
                break;
            case HardwareApi.Qsv:
                // global_quality without a bitrate selects ICQ (intelligent constant quality), or CQP where the driver
                // has no ICQ.
                args.AddRange(["-preset", "medium", "-global_quality", quantizer]);
                break;
            case HardwareApi.Amf:
                // Constant quantizer; I-frames two steps finer, as AMF's own presets do.
                args.AddRange(["-quality", "balanced", "-rc", "cqp", "-qp_i", Text(Math.Max(0, Quantizer(codec, quality) - 2)), "-qp_p", quantizer]);
                if (codec == "h264")
                    args.AddRange(["-qp_b", quantizer]);
                break;
            case HardwareApi.VideoToolbox when encoder.RateControl == HardwareRateControl.Bitrate:
                args.AddRange(["-b:v", Text(BitrateKbps(codec, quality, video, maxHeight)) + "k"]);
                break;
            case HardwareApi.VideoToolbox:
                args.AddRange(["-q:v", Text(VideoToolboxQuality(quality))]);
                break;
            case HardwareApi.Vaapi:
                args.AddRange(["-rc_mode", "CQP", "-qp", quantizer]);
                break;
        }

        if (codec == "h264")
            args.AddRange(["-profile:v", "high"]);
        else
            args.AddRange(["-profile:v", tenBit ? "main10" : "main"]);
        // VA-API's frames are already uploaded in the right format (FilterSuffix).
        if (encoder.Api != HardwareApi.Vaapi)
            args.AddRange(["-pix_fmt", tenBit ? "p010le" : "nv12"]);
        if (tenBit)
            args.AddRange(MediaEncoding.ColorArguments(video));
        if (codec == "hevc" && target is "mp4" or "m4v" or "mov")
            args.AddRange(["-tag:v", "hvc1"]);
        return args;
    }

    /// <summary>The source of a probe: a tenth of a second of test pattern, three small frames.</summary>
    private const string ProbeSource = "testsrc=size=256x144:rate=30:duration=0.1";

    /// <summary>
    /// A tiny test encode with exactly the options a real one uses (rate control, profile, pixel format), so an
    /// encoder FFmpeg lists but the computer can't run (no GPU, old driver, no 10-bit support, no constant quality on
    /// an Intel Mac) fails here instead of on the user's file.
    /// </summary>
    public static List<string> ProbeArguments(HardwareEncoder encoder, bool tenBit)
    {
        var picture = new VideoStreamInfo(256, 144, 1, 0, 30, PixelFormat: tenBit ? "yuv420p10le" : "yuv420p");
        return
        [
            .. MediaEncoding.Common, .. InputArguments(encoder), "-f", "lavfi", "-i", ProbeSource,
            "-vf", "null" + FilterSuffix(encoder, tenBit),
            .. CodecArguments(encoder, "mkv", MediaQuality.Balanced, picture, 0),
            "-f", "null", "-",
        ];
    }

    private static T Choose<T>(MediaQuality quality, T high, T balanced, T small) => quality switch
    {
        MediaQuality.High => high,
        MediaQuality.Small => small,
        _ => balanced,
    };

    private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);
}
