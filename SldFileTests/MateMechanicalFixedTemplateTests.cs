using System;
using System.IO;
using System.Linq;
using SldFile;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Fixed measured-template authoring for the tutorial Hinge (chain_link1),
/// Cam (cam-follower) and PlanarAngle (key pad_1) assemblies. The API writes the
/// complete measured MatesList and does not parameterize any mate.
/// </summary>
public class MateMechanicalFixedTemplateTests
{
    [Fact]
    public void AddTutorialHingeMates_WritesFourHingeMates()
    {
        string path = CopyClean("Hinge", "chain_link1_clean.sldasm", "hinge_api");
        try
        {
            using (var assembly = new SldAssembly(path, readOnly: false))
            {
                assembly.AddTutorialHingeMates();
                assembly.Save();
            }
            using var reopened = new SldAssembly(path, readOnly: true);
            var hinges = reopened.Mates.Where(m => m.MateType == AssemblyMateType.Hinge).ToArray();
            Assert.Equal(4, hinges.Length);
            Assert.All(hinges, mate =>
            {
                Assert.Equal(22u, mate.MateTypeId);
                Assert.Equal(4, mate.Entities.Count);
                Assert.True(mate.Resolution.EntityReferences);
            });
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void AddTutorialCamMates_WritesTwoCamMates()
    {
        string path = CopyClean("Cam", "cam-follower_clean.sldasm", "cam_api");
        try
        {
            using (var assembly = new SldAssembly(path, readOnly: false))
            {
                assembly.AddTutorialCamMates();
                assembly.Save();
            }
            using var reopened = new SldAssembly(path, readOnly: true);
            var cams = reopened.Mates.Where(m => m.MateType == AssemblyMateType.Cam).ToArray();
            Assert.Equal(2, cams.Length);
            Assert.Contains(cams, m => m.SerializedClassName == "moMateCamTangent");
            Assert.Contains(cams, m => m.SerializedClassName == "moMateCamCoincident");
            Assert.All(cams, mate =>
            {
                Assert.Equal(9u, mate.MateTypeId);
                Assert.Equal(2, mate.Entities.Count);
            });
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void AddTutorialPlanarAngleMates_WritesTwoMeasuredAngles()
    {
        string path = CopyClean("Planar", "key_pad_1_clean.sldasm", "planar_api");
        try
        {
            using (var assembly = new SldAssembly(path, readOnly: false))
            {
                assembly.AddTutorialPlanarAngleMates();
                assembly.Save();
            }
            using var reopened = new SldAssembly(path, readOnly: true);
            var angles = reopened.Mates.Where(m => m.MateType == AssemblyMateType.PlanarAngle).ToArray();
            Assert.Equal(2, angles.Length);
            Assert.Equal(new[] { 3.4033920413889502, 6.02138591938045 },
                angles.Select(a => a.Angle!.Value).OrderBy(v => v).ToArray());
            Assert.All(angles, mate => Assert.InRange(mate.Entities.Count, 1, 2));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void FixedTemplates_RejectWrongBaseAndDoubleCall()
    {
        string hinge = CopyClean("Hinge", "chain_link1_clean.sldasm", "hinge_wrong");
        try
        {
            using (var assembly = new SldAssembly(hinge, readOnly: false))
            {
                byte[] before = (byte[])assembly.GetStreamBytes("Contents/Config-0-MatesList")!.Clone();
                Assert.Throws<NotSupportedException>(() => assembly.AddTutorialCamMates());
                Assert.Equal(before, assembly.GetStreamBytes("Contents/Config-0-MatesList"));

                assembly.AddTutorialHingeMates();
                byte[] pending = (byte[])assembly.GetStreamBytes("Contents/Config-0-MatesList")!.Clone();
                Assert.Throws<NotSupportedException>(() => assembly.AddTutorialHingeMates());
                Assert.Equal(pending, assembly.GetStreamBytes("Contents/Config-0-MatesList"));
            }
        }
        finally { File.Delete(hinge); }
    }

    [Fact]
    public void FixedTemplates_ReadOnly_ThrowsAndDoesNotModify()
    {
        string path = CopyClean("Cam", "cam-follower_clean.sldasm", "cam_ro");
        try
        {
            byte[] before = File.ReadAllBytes(path);
            using (var assembly = new SldAssembly(path, readOnly: true))
                Assert.Throws<InvalidOperationException>(() => assembly.AddTutorialCamMates());
            Assert.Equal(before, File.ReadAllBytes(path));
        }
        finally { File.Delete(path); }
    }

    private static string CopyClean(string folder, string fileName, string prefix)
    {
        string source = TestFilesPath(folder, fileName);
        Assert.True(File.Exists(source), "Missing clean template base: " + source);
        string target = Path.Combine(Path.GetDirectoryName(source)!,
            prefix + "_" + Guid.NewGuid().ToString("N") + Path.GetExtension(source));
        File.Copy(source, target, true);
        return target;
    }

    private static string TestFilesPath(params string[] parts)
        => Path.Combine(new[] { AppContext.BaseDirectory, "TestFiles", "Mates2026" }
            .Concat(parts).ToArray());
}
