using Microsoft.Extensions.DependencyInjection;
using Truss.Application.DependencyInjection;
using Xunit;

namespace Truss.Application.Tests.DependencyInjectionTests
{
    public class ServiceCollectionExtensionsTests
    {
        [Fact]
        public void AddTruss_WithoutAssemblies_Throws()
        {
            var services = new ServiceCollection();

            Assert.Throws<InvalidOperationException>(() => services.AddTruss(_ => { }));
        }
    }
}
