using Xunit;

namespace SldFile.Tests.Integration.Framework;

[CollectionDefinition("SolidWorksIntegration")]
public class SolidWorksCollection : ICollectionFixture<SolidWorksApplicationFixture>
{
}
