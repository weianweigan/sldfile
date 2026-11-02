using System;
using System.IO;
using System.Linq;
using SldFile;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Measured tutorial Path mate decoding and fixed-template authoring:
/// four free moMatePath_c records (PathMate1 inline + PathMate2..4 class-ref)
/// with PointN selections and a general-curve path reference; D1 is 0 (free).
/// </summary>
public class MatePathTutorialTests
{
    [Theory]
    [InlineData("Path/motion_path_tutorial.SLDASM")]
    [InlineData("Path/motion_path_tutorial_resave.SLDASM")]
    public void Path_FourFreeMates_DecodeMainAndResave(string relativePath)
    {
        using var assembly = new SldAssembly(TestFilesPath(relativePath.Split('/')), readOnly: true);
        var paths = assembly.Mates.Where(m => m.MateType == AssemblyMateType.Path).ToArray();
        Assert.Equal(4, paths.Length);
        foreach (var mate in paths)
        {
            Assert.Equal("moMatePath_c", mate.SerializedClassName);
            Assert.Equal(15u, mate.MateTypeId);
            Assert.True(mate.Resolution.MateType);
            Assert.True(mate.Resolution.EntityReferences);
            Assert.True(mate.Resolution.PathParameters);
            Assert.Equal(0.0, mate.PathValue);
            Assert.True(mate.PathIsFree);
            // The tutorial path header does not match the controlled signature.
            Assert.Null(mate.Alignment);

            Assert.Equal(2, mate.Entities.Count);
            Assert.Equal(MateReferenceKind.Point, mate.Entities[0].Kind);
            Assert.Contains("chain_link1", mate.Entities[0].ComponentInstanceName);
            Assert.Equal("Point2", mate.Entities[0].FeatureName);
            Assert.StartsWith("Point2@", mate.Entities[0].SelectionName);
            Assert.Equal(MateReferenceKind.Path, mate.Entities[1].Kind);
            Assert.Equal(string.Empty, mate.Entities[1].ComponentInstanceName);
        }
    }

    [Fact]
    public void AddTutorialPathMates_WritesFourFreeMatesAndReReads()
    {
        string path = CreateCleanPathCopy();
        try
        {
            using (var assembly = new SldAssembly(path, readOnly: false))
            {
                assembly.AddTutorialPathMates();
                assembly.Save();
            }

            using var reopened = new SldAssembly(path, readOnly: true);
            var paths = reopened.Mates.Where(m => m.MateType == AssemblyMateType.Path).ToArray();
            Assert.Equal(4, paths.Length);
            Assert.All(paths, mate =>
            {
                Assert.Equal(0.0, mate.PathValue);
                Assert.True(mate.PathIsFree);
                Assert.Equal(2, mate.Entities.Count);
                Assert.Equal(MateReferenceKind.Point, mate.Entities[0].Kind);
                Assert.Equal(MateReferenceKind.Path, mate.Entities[1].Kind);
            });
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AddTutorialPathMates_RejectsWrongBaseAndExistingPath()
    {
        string clean = CreateCleanPathCopy();
        string rack = CreateTempCopy(TestFilesPath("RackPinion", "rack and pinion_clean.sldasm"), "path_wrong");
        try
        {
            using (var assembly = new SldAssembly(clean, readOnly: false))
            {
                byte[] original = (byte[])assembly.GetStreamBytes("Contents/Config-0-MatesList")!.Clone();
                assembly.AddTutorialPathMates();
                byte[] pending = (byte[])assembly.GetStreamBytes("Contents/Config-0-MatesList")!.Clone();
                Assert.NotEqual(original, pending);
                Assert.Throws<NotSupportedException>(() => assembly.AddTutorialPathMates());
                Assert.Equal(pending, assembly.GetStreamBytes("Contents/Config-0-MatesList"));
            }

            using (var assembly = new SldAssembly(rack, readOnly: false))
            {
                Assert.Throws<NotSupportedException>(() => assembly.AddTutorialPathMates());
            }
        }
        finally
        {
            File.Delete(clean);
            File.Delete(rack);
        }
    }

    [Fact]
    public void AddTutorialPathMates_ReadOnly_ThrowsAndDoesNotModify()
    {
        string path = CreateCleanPathCopy();
        try
        {
            byte[] before = File.ReadAllBytes(path);
            using (var assembly = new SldAssembly(path, readOnly: true))
                Assert.Throws<InvalidOperationException>(() => assembly.AddTutorialPathMates());
            Assert.Equal(before, File.ReadAllBytes(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string CreateCleanPathCopy()
    {
        string source = TestFilesPath("Path", "motion_path_tutorial_clean.sldasm");
        return CreateTempCopy(source, "path_api");
    }

    private static string CreateTempCopy(string source, string prefix)
    {
        Assert.True(File.Exists(source), "Missing measured sample: " + source);
        string dir = Path.GetDirectoryName(source)!;
        string target = Path.Combine(dir, prefix + "_" + Guid.NewGuid().ToString("N") + ".sldasm");
        File.Copy(source, target, true);
        return target;
    }

    private static string TestFilesPath(params string[] parts)
    {
        return Path.Combine(new[] { AppContext.BaseDirectory, "TestFiles", "Mates2026" }
            .Concat(parts).ToArray());
    }
}
