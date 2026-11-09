using System;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Assembly;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests;

public class DrawingVectorAndBomTests
{
    private sealed class CustomMockResolver : IPathResolver
    {
        public string? Resolve(string recordedPath, string? parentAssemblyPath = null)
        {
            return "/custom/virtual/path/" + Path.GetFileName(recordedPath.Replace('\\', '/'));
        }
    }

    [Fact]
    public void CustomPathResolver_InterfaceImplementation_CanBeUsedInBomOptions()
    {
        var custom = new CustomMockResolver();
        var options = new BomOptions { PathResolver = custom };
        Assert.Same(custom, options.PathResolver);
        Assert.Equal("/custom/virtual/path/test.sldprt", custom.Resolve(@"C:\dummy\test.sldprt"));
    }

    [Fact]
    public void PathResolver_Resolve_PrefixSubstitution_MapsWindowsToLocal()
    {
        var options = new PathResolverOptions();
        string tempDir = Path.Combine(Path.GetTempPath(), "SldFileTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            string localPart = Path.Combine(tempDir, "Bracket.SLDPRT");
            File.WriteAllText(localPart, "dummy CAD content");

            options.Substitutions.Add(new PathSubstitution(@"C:\Engineering\ProjectA\", tempDir));
            var resolver = new PathResolver(options);

            string? resolved = resolver.Resolve(@"C:\Engineering\ProjectA\Bracket.SLDPRT");
            Assert.NotNull(resolved);
            Assert.True(File.Exists(resolved));
            Assert.Equal(Path.GetFullPath(localPart), resolved);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void PathResolver_Resolve_ParentAssemblyRelative_FindsSiblingComponent()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "SldFileTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            string asmPath = Path.Combine(tempDir, "MainAsm.SLDASM");
            string partPath = Path.Combine(tempDir, "Motor.SLDPRT");
            File.WriteAllText(asmPath, "dummy");
            File.WriteAllText(partPath, "dummy");

            var resolver = new PathResolver();
            // Windows recorded path from original CAD workstation
            string? resolved = resolver.Resolve(@"D:\OriginalWorkstation\Parts\Motor.SLDPRT", asmPath);
            Assert.NotNull(resolved);
            Assert.Equal(Path.GetFullPath(partPath), resolved);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void BomTransformer_BuildBom_AggregatesFlatAndExportsJson()
    {
        if (!File.Exists(TestFileProvider.Assembly)) return;

        using var asm = new SldAssembly(TestFileProvider.Assembly, readOnly: true);
        var bom = asm.BuildBom();

        Assert.NotNull(bom);
        Assert.NotNull(bom.Tree);
        Assert.NotNull(bom.Flat);

        string json = bom.ToJson(indented: true);
        Assert.NotNull(json);
        Assert.Contains("\"flat\":", json);
        Assert.Contains("\"warnings\":", json);
    }

    [Fact]
    public void Drawing_ExportSheetSvg_GeneratesValidSvgWithLayers()
    {
        if (!File.Exists(TestFileProvider.CubeDrawing)) return;

        using var drw = new SldDrawing(TestFileProvider.CubeDrawing, readOnly: true);
        string svg = drw.ExportSheetSvg(0);

        Assert.NotNull(svg);
        Assert.Contains("<?xml version=\"1.0\" encoding=\"UTF-8\"?>", svg);
        Assert.Contains("<svg", svg);
        Assert.Contains("id=\"sheet-format\"", svg);
        Assert.Contains("id=\"views\"", svg);
        Assert.Contains("id=\"dimensions\"", svg);
        Assert.Contains("id=\"balloons\"", svg);
        Assert.Contains("id=\"notes\"", svg);
        Assert.Contains("ABCD", svg);
        Assert.Contains(System.Xml.Linq.XDocument.Parse(svg).Descendants(), e => e.Name.LocalName == "line");
        Assert.Contains("</svg>", svg);
    }
}
