using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Measured hem subsystems for the two controlled combinations
/// (SheetMetalFormingOptions/sheetmetal_hem_open and sheetmetal_hem_closed,
/// ResolvedFeatures version 0x73).
/// <para>
/// The prefix is the base-flange stream before the <c>moHem_c</c> feature tag
/// (0x4D35). The suffix starts at the feature tag and contains the hem
/// folder/TypeId 0x67, the selection block, all hem dimensions, the hem edge
/// chain and the downstream flat-pattern objects. Prefix + matching suffix
/// reconstructs each native sample byte-for-byte. Each suffix is gzip/base64
/// encoded for source readability and hash-verified before use.
/// </para>
/// </summary>
internal static class HemAuthoringSegments
{
    internal const int MeasuredPrefixLength = 0x4D35;
    internal const ushort MeasuredFolderCount = 23;
    internal const ushort SupportedStreamVersion = 0x73;

    internal const string OpenPrefixSha256 =
        "D98033C40448265399D5034B0C7819B75A5672A22C6E0ECBF3EB8BADB7319C92";
    internal const string ClosedPrefixSha256 =
        "F0896F254AA6E702A8032D5A494D0EA8009DD4C85C8132920F45B629D93A6AC5";
    internal const string OpenSuffixSha256 =
        "70DBA046BBCDCCD1ECCE67700B68E0BA94C34BF2D80B21E338150292F3D38DD0";
    internal const string ClosedSuffixSha256 =
        "6E64CB72CD2478D3565127AF741824FDCF239A17E0AE4C189BE6F7AE7482782C";

    internal const string OpenDisplayName = "Hem_open";
    internal const string ClosedDisplayName = "Hem_closed";

    /// <summary>Measured native values shared by both templates.</summary>
    internal const double MeasuredLength = 0.005;
    internal const double MeasuredRadius = 0.001;
    internal const double MeasuredAngle = 3.1415926535898;
    internal const double MeasuredMiterGap = 0.0005;
    internal const double OpenGapDistance = 0.0005;
    internal const double ClosedGapDistance = 0.0001;

    private const string OpenSuffixGzipBase64 =
        "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";

    private const string ClosedSuffixGzipBase64 =
        "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";

    internal const double TenMillimetreLength = 0.010;
    internal const double OneMillimetreGap = 0.001;

    internal const string Length10PrefixSha256 =
        "DBC72C7B4DDDBD7E1D7D9694E32E6D5307D0FFB00A7CC96C5BECA3523F5A1E6C";
    internal const string Length10SuffixSha256 =
        "DB1178D3987FC29D2804FAA5D0FEE268B772E0B27AB80CA83E2C0E5D746E4825";
    internal const string Gap1PrefixSha256 =
        "1622D3D57DC712843F8AA2214A9C48B90A7573984600392C3DEBA4F4BC41C4E5";
    internal const string Gap1SuffixSha256 =
        "E21731278606243C69E295BA4C787FAD4D8E565886D3E498A5DCC84F2085DCAB";

    private const string Length10SuffixGzipBase64 =
        "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";

    private const string Gap1SuffixGzipBase64 =
        "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";
    internal const double OneMillimetreMiterGap = 0.001;

    internal const string ClosedLength10PrefixSha256 =
        "B3C597251ED20D7BFA3359BD26D29B8BE324583EC1DD5C907851959679994EBC";
    internal const string ClosedLength10SuffixSha256 =
        "BF20DD17A3D70CD6ADEE51CE2696D52F420EA9F77ED9176B83D55F3CC992A613";
    internal const string OpenMiterGap1PrefixSha256 =
        "3C5188BA2069C492270D045973AD7D9ACF24CCF0C4DAAE2CF42DB8CAC057F7A3";
    internal const string OpenMiterGap1SuffixSha256 =
        "5B99521818C7F7CEA8C6D69E8545039AAF0F9AD17948A88ED67E64C0FBF53FAE";

    private const string ClosedLength10SuffixGzipBase64 =
        "H4sIAAAAAAAACu1dfWwcRxV/e3dOaeI0JQ0phAApUtUqxErcBjcREXeJPxpHaezYbhso5Ly+W3/Et7fmvCYuhfacVpVoCRZSKG3/QEUJf7QSFKEU1SJg1EgBhT8QCIlIgBqqSBVUBbegUNrEi97uzn7P3vru9ny3nl902b3Z+XgzN/Pem9n3nhWFgxtAlPYLYjqTKCqLyur9IIAIachADiSYBAGyoCM1ot8oi4p+F4MLRbVcvPvxxQfuMXIOunLaIeB/nCVh1wHzftubnyRFFQUgrugAKM571oa5FUV5YvSv3Ka73v9MDH7+wyZb7RpeLcaMe3yqKJ/yqjwBMb0wpiSctSwkyR1S6UQMLhY5lRqSsmi5p4NQG6sKXY9s+PG9f/vaqWQ16EKKOPqYcS7aFIWD1TA50jktd+bldAa/3wyi1D8uyJnRzmm5TxhOH8fUZhCldkmc6MyOCOnMNvXnSQEcA+3jB0KCSYob6pzCdtaAKGEb2HCGjLQ6gDGA3c0A3zh/6vYfdf90gFy1YhuwWD7bxWeE/qnCcHe2T5hIZ7Rnq0CUuvq0zq3FfNNyz9AxIaP398PYs365MJYf2c/nszkhncH5vGMGl8rVZ64easWmr7165Rgmk48dcW0RxC59/8zTzq4dBYA1c1eMUcI2b0WSCpLYP47jbqN428x2jhTBau+esafFPNISlNVZOU47vv/6d4pCPl6tab8v+UUOjuWFfXxmXMjeP9HBy7w24B8BUeqVxvKy/RGNhD0z7iXiBRz/udn5R31Wm52PLcyTlFip1cKVXslYE+eouxyYdXiulS06ucgUAC4WzWxOpkGqoC05ryZGis4c2HtsL65/uCV87N3RxIQ2ttiuxqLotGFrpeCm1682Cy0vPKl9+UVRY2UBEZSVqc9O3gEvmfSlSH5fOeusqFI5S+5pcpYGr5zBSzvR+tzWj373j3NJbSGfMSi2LyecEhtBlPYOTUq5KRlFQG8urzEHwgK06xMlWMFCkqMsw6L9Sn1uVOTf8EIS1m9IXvjLK2eT+nX++ecQLyb1q97ANUVR8OM3sDUgVhvyD/SJMeqoj8jfnrywT8hndTWzqe2bc7PPDm1Pt5J8KZEUqOVURpXx7E3xbcs7lU286eI9Rne0r87ukEEowocgNFgFkudzQ+ippHh8I7DyOOt4uX8ll1QMAbHAPTXTadMTsSFgu+5WTr79ygv/vfCTz5Orf0kcub8HllJLUJbdxbS7t4rYZJe21Yt1QCtFALW0b37rjbbVSQ8+oILwkvdey740OHAiuUPPT0und8mqF9lgMDQng6MxPCCNkcbtmqE64lpryqLC9RnKujkRKJOmuDP1+lee+vaVd3uSlnGpSI+rHpogBnFjAZRa4HbEIAZ3JExJQUYC5wLpKXKjmy1qG0nHvP9SS9on4sI8jvH19jcuB6dj969smndJ+V3qOdZ27bFSrTrrWVD75JwV2LtVRp6pA+s8arKqtOR7KRjtWEbPjyV5lX8GdsFSQVsk/ovHm7JHwtGP8XRqLe6J3eNob+32GaP9qh413DkT7Gxhq2OnTSpx7sCXb6dOdtQdY5MTOf7hvfmRqRxf6BgT3Ttqpp80jn6yNqr6iXbkuDc/khPUSaodA3CwHnd+mNrLF3hRkIWCvv+IdcBOig5zZ0vHwAe33ZgCgDE/ognHu/biO3+4b6jXkOnX9PTt+dPcuaY+Yyjduk2IOoyjPqLD6FoMXYc5+/Yee9HLVjXIRuLGbx34R2Ln4cCag55fJ33wHFRZh1ka/HQYjVLepcOQmVFCh9nLdJjwdRjL2nGuDnOV7Gg+v6P5/HUy1Yr2KWjmJ1P5YwDwDtuBN/AOHKIs4fA906EpcUgo2IQcvhTpzsvCiFDwEHOfpYg5ffmUUn6rgABixwUidpyol60z/itHtJA0p2ix5vUWLQlQlGiJFjPP1BeDbI+hRqLFRr36VsmZi8kIAiYj6khG/Ec9pb1qnNLuorB+bceSQiLHIXQw9s/YfyOxf8bcCRhzr2Pmvrs0c89B6GDMnTH3RmLuiBuX0GbpF6MLSWNtB+sUjQucsbIBtGfB65HdZ3/z1V++nKzEvuVy07SvfcvnlmDfIhZbUDA0a9/wunQwG5dGFrAr3sZFf9+Dg8VD6CjL8sRyfLZyLE/KP1pjlieVWp5c+vM/DwYp/2QZliflgcbCOPeLJACO7fkaWSStmD3fXb52/yrTd1lJVx9sz8f2fI2053ufvdFvYOa+fqUw97tLO3UNQ+iIInMPw0aswt1GpF7k19rOvcVcK77lK4P3GnBTv9G0Jq8m30DDHGu1HHRz2kDWk5n5Jo4zbJD2CyI62beP8mO6gyXx/Lc+Qbsl4q1uTe/MCaKA4QKIdX61AwHU07BVhs1cmF4MP6jQj7Fu1S3s10XfPMyNMdoqV3zG7sZ4D0Xlsrg6D3k1YneFTp0jpv4knVydLgBOcEFFN7FoTtJdBpx2+pRxdT5P7dGJnNeJnvdye/QUhE6atR/l38mvezuKL1kZ9K6Hnr58R8+EIqcySOYBUwaX3+kxYsLM/zkTZCtLkNEsPbW3f5uQSGoopOqBnR2YYGcH9Xl2EK64sPiOGmoGEyEE7Pi5jkVIAHtSIxBoeGAixAQTIfUpQrZuGc+GJ0I06jgjfAGGu+QLeEx6ryDZ4284/Z29vP5VkP3pntd7Tn4vf8KVTq66ukiOAjxDvpGz2wFBnMjxstCV4+VeXpaFQl53Ok1MHn1s5OkH16X1Eqkv0MMGN2EMuBaYf9SMKPVgiSFULRxuCyEOXOfswlZ66GCoItwRJklwhnI+WvlyI2JyKzEi5hZHRdGIiEmDOyLmLRgqeGxayFrDYQarzOQqHGXzXOrkcYkHgoGP6ZCe66XCYdaCWBzv/+mzwmmPpfO9xNzswamuUZPrHdFvfKctFwLXe+hnUeV6fgupcq7HNR7Xc1YUba5nr/G3M8FKMfZWEXuzDrz6LBYCy7qhxd+hqbZzLqRohj3E/MBNJb01M69T7dNUeTnpvC93gxQOEo4P6QGnGzggY9+Mkf6Fwj4p+3D/qCDI9wkyn0MN5pBwHJUYzHdUz3sT/tkJI5O+P4mfPvX777QZbQ5ol8GaKow4jQ+8/B5V8tZ6GjOfvEY+pGQ+eeoh5SAOVh+s9LjTlnTLuNRl8KzqmWCQnjpNMEh6icBaUY07XRNHjKDef8sdd5p5/0Xxjy5AVI08yvD+64XQEcU3dCyaY3SjOTLzi0Zm7pG14CvD++8whI4oMnfm/Rct778gCM/7LxDLm9d6yaKKNLLogZUietpK7yv6IXREUfSwfUV09xW0bxqOWajCF1Hr0ODGZSnXRCzl2ozMX3ZVGnfZypkHAQ8tl62c3PFuRK1GaCzVWTI4vTQ0iNXISraVY1YjVTOKiwc2ijPZ25f0G+f85MJmb68djip7Y0ZxJphRHDOKqwV7w5W3CRVAKZe9f6JDOp7vHxfkzCh+N/5UV1P344sP5GaHMq1kjaaIA4WveP50COzvT5e8LJOwG5/w6MaRYakgepuQY5GP24p0CbxMKcCF3a16MrhazlBSZnkOmkGU2iVxoovPCOlMOJGXsJ01OAv4jNAnDGt/uQ6hHh3GAFYFCsRESnkFWCo3OBPnc2xyy4lwhuPWWOWmmiEZnaZtMa/qxw7UOvbWPvwfiaYxl8COAAA=";

    private const string OpenMiterGap1SuffixGzipBase64 =
        "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";
    private static readonly Lazy<byte[]> OpenSuffix =
        new(() => Decompress(OpenSuffixGzipBase64, OpenSuffixSha256), isThreadSafe: true);
    private static readonly Lazy<byte[]> ClosedSuffix =
        new(() => Decompress(ClosedSuffixGzipBase64, ClosedSuffixSha256), isThreadSafe: true);
    private static readonly Lazy<byte[]> Length10Suffix =
        new(() => Decompress(Length10SuffixGzipBase64, Length10SuffixSha256), isThreadSafe: true);
    private static readonly Lazy<byte[]> Gap1Suffix =
        new(() => Decompress(Gap1SuffixGzipBase64, Gap1SuffixSha256), isThreadSafe: true);
    private static readonly Lazy<byte[]> ClosedLength10Suffix =
        new(() => Decompress(ClosedLength10SuffixGzipBase64, ClosedLength10SuffixSha256), isThreadSafe: true);
    private static readonly Lazy<byte[]> OpenMiterGap1Suffix =
        new(() => Decompress(OpenMiterGap1SuffixGzipBase64, OpenMiterGap1SuffixSha256), isThreadSafe: true);

    internal const int TemplateOpenBaseline = 0;
    internal const int TemplateClosedBaseline = 1;
    internal const int TemplateOpenLength10 = 2;
    internal const int TemplateOpenGap1 = 3;
    internal const int TemplateClosedLength10 = 4;
    internal const int TemplateOpenMiterGap1 = 5;

    /// <summary>
    /// Resolves the measured template. Only the combinations present in the
    /// controlled samples are accepted; null means the template default.
    /// </summary>
    internal static bool TryGetTemplate(
        int type, double? length, double? gap, double? miterGap, out int template)
    {
        double l = length ?? MeasuredLength;
        double g = gap ?? GetGapDistance(type);
        double m = miterGap ?? MeasuredMiterGap;
        if (type == 0 && l == MeasuredLength && g == OpenGapDistance && m == MeasuredMiterGap)
            template = TemplateOpenBaseline;
        else if (type == 1 && l == MeasuredLength && g == ClosedGapDistance && m == MeasuredMiterGap)
            template = TemplateClosedBaseline;
        else if (type == 0 && l == TenMillimetreLength && g == OpenGapDistance && m == MeasuredMiterGap)
            template = TemplateOpenLength10;
        else if (type == 0 && l == MeasuredLength && g == OneMillimetreGap && m == MeasuredMiterGap)
            template = TemplateOpenGap1;
        else if (type == 1 && l == TenMillimetreLength && g == ClosedGapDistance && m == MeasuredMiterGap)
            template = TemplateClosedLength10;
        else if (type == 0 && l == MeasuredLength && g == OpenGapDistance && m == OneMillimetreMiterGap)
            template = TemplateOpenMiterGap1;
        else
        {
            template = -1;
            return false;
        }
        return true;
    }

    internal static byte[] GetTemplateSuffix(int template)
    {
        return template switch
        {
            TemplateOpenBaseline => (byte[])OpenSuffix.Value.Clone(),
            TemplateClosedBaseline => (byte[])ClosedSuffix.Value.Clone(),
            TemplateOpenLength10 => (byte[])Length10Suffix.Value.Clone(),
            TemplateOpenGap1 => (byte[])Gap1Suffix.Value.Clone(),
            TemplateClosedLength10 => (byte[])ClosedLength10Suffix.Value.Clone(),
            TemplateOpenMiterGap1 => (byte[])OpenMiterGap1Suffix.Value.Clone(),
            _ => throw new ArgumentOutOfRangeException(nameof(template))
        };
    }

    internal static string GetTemplatePrefixSha256(int template)
    {
        return template switch
        {
            TemplateOpenBaseline => OpenPrefixSha256,
            TemplateClosedBaseline => ClosedPrefixSha256,
            TemplateOpenLength10 => Length10PrefixSha256,
            TemplateOpenGap1 => Gap1PrefixSha256,
            TemplateClosedLength10 => ClosedLength10PrefixSha256,
            TemplateOpenMiterGap1 => OpenMiterGap1PrefixSha256,
            _ => throw new ArgumentOutOfRangeException(nameof(template))
        };
    }

    internal static byte[] GetSuffix(int type)
    {
        return type switch
        {
            0 => (byte[])OpenSuffix.Value.Clone(),
            1 => (byte[])ClosedSuffix.Value.Clone(),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
    }

    internal static string GetDisplayName(int type)
        => type == 0 ? OpenDisplayName : type == 1 ? ClosedDisplayName
            : throw new ArgumentOutOfRangeException(nameof(type));

    internal static string GetPrefixSha256(int type)
        => type == 0 ? OpenPrefixSha256 : type == 1 ? ClosedPrefixSha256
            : throw new ArgumentOutOfRangeException(nameof(type));

    internal static double GetGapDistance(int type)
        => type == 0 ? OpenGapDistance : type == 1 ? ClosedGapDistance
            : throw new ArgumentOutOfRangeException(nameof(type));

    private static byte[] Decompress(string base64, string expectedSha256)
    {
        byte[] gzip = Convert.FromBase64String(base64);
        using var input = new MemoryStream(gzip, writable: false);
        using var decompressor = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        decompressor.CopyTo(output);
        byte[] bytes = output.ToArray();
        string hash;
        using (var sha = SHA256.Create())
            hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty);
        if (!string.Equals(hash, expectedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Measured hem suffix hash mismatch.");
        return bytes;
    }
}
