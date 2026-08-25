using Microsoft.Extensions.DependencyInjection;
using Truss.EntityFrameworkCore;
using Xunit;

namespace Truss.EntityFrameworkCore.Tests
{
    public class SeederTests
    {
        private sealed class SeededNames
        {
            public List<string> Names { get; } = [];
        }

        private sealed class FirstSeeder(SeededNames seeded) : ITrussSeeder
        {
            public Task Seed(CancellationToken cancellationToken = default)
            {
                seeded.Names.Add("first");
                return Task.CompletedTask;
            }
        }

        private sealed class SecondSeeder(SeededNames seeded) : ITrussSeeder
        {
            public Task Seed(CancellationToken cancellationToken = default)
            {
                seeded.Names.Add("second");
                return Task.CompletedTask;
            }
        }

        [Fact]
        public async Task RunTrussSeeders_RunsEverySeeder_InRegistrationOrder()
        {
            var services = new ServiceCollection();
            services.AddSingleton<SeededNames>();
            services.AddTrussSeeder<FirstSeeder>();
            services.AddTrussSeeder<SecondSeeder>();

            await using var provider = services.BuildServiceProvider();
            await provider.RunTrussSeeders();

            Assert.Equal(["first", "second"], provider.GetRequiredService<SeededNames>().Names);
        }

        [Fact]
        public async Task RunTrussSeeders_WithoutSeeders_IsANoOp()
        {
            var services = new ServiceCollection();
            await using var provider = services.BuildServiceProvider();

            await provider.RunTrussSeeders();
        }
    }
}
