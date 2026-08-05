using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Truss.Application.Tests
{
    public class TrussApplicationModuleTests
    {
        [Fact]
        public void AddTruss_WithoutAssemblies_Throws()
        {
            var services = new ServiceCollection();

            Assert.Throws<InvalidOperationException>(() => services.AddTruss(_ => { }));
        }
    }
}
