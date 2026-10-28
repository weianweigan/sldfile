using System;
using Xunit;

namespace SldFile.Tests.Integration.Framework;

/// <summary>
/// A fact attribute that skips execution if SOLIDWORKS is not installed on the host machine.
/// Mimics xCAD's integration test filtering behavior.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class SolidWorksFactAttribute : FactAttribute
{
    public SolidWorksFactAttribute()
    {
        if (!SolidWorksApplicationFixture.IsSolidWorksAvailable)
        {
            Skip = "SOLIDWORKS application is not installed on this machine.";
        }
    }
}
