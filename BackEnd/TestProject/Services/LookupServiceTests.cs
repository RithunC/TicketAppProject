using TestProject.Helpers;
using TicketWebApp.Models;
using TicketWebApp.Repositories;
using TicketWebApp.Services;

namespace TestProject.Services
{
    public class LookupServiceTests
    {
        private static LookupService Build(string db)
        {
            var ctx = DbContextFactory.CreateWithSeed(db);
            return new LookupService(
                new Repository<int, Department>(ctx),
                new Repository<int, Role>(ctx),
                new Repository<int, Category>(ctx),
                new Repository<int, Priority>(ctx),
                new Repository<int, Status>(ctx));
        }

        [Fact]
        public async Task GetDepartmentsAsync_ReturnsDepartments()
        {
            var sut = Build(nameof(GetDepartmentsAsync_ReturnsDepartments));
            var result = await sut.GetDepartmentsAsync();
            Assert.NotEmpty(result);
        }

        [Fact]
        public async Task GetDepartmentsAsync_ThrowsWhenEmpty()
        {
            var ctx = DbContextFactory.Create(nameof(GetDepartmentsAsync_ThrowsWhenEmpty));
            var sut = new LookupService(
                new Repository<int, Department>(ctx),
                new Repository<int, Role>(ctx),
                new Repository<int, Category>(ctx),
                new Repository<int, Priority>(ctx),
                new Repository<int, Status>(ctx));

            await Assert.ThrowsAsync<InvalidOperationException>(() => sut.GetDepartmentsAsync());
        }

        [Fact]
        public async Task GetRolesAsync_ReturnsRoles()
        {
            var sut = Build(nameof(GetRolesAsync_ReturnsRoles));
            var result = await sut.GetRolesAsync();
            Assert.Equal(3, result.Count);
        }

        [Fact]
        public async Task GetCategoriesAsync_ReturnsCategories()
        {
            var sut = Build(nameof(GetCategoriesAsync_ReturnsCategories));
            var result = await sut.GetCategoriesAsync();
            Assert.NotEmpty(result);
        }

        [Fact]
        public async Task GetPrioritiesAsync_ReturnsPrioritiesOrderedByRank()
        {
            var sut = Build(nameof(GetPrioritiesAsync_ReturnsPrioritiesOrderedByRank));
            var result = await sut.GetPrioritiesAsync();
            Assert.Equal(4, result.Count);
            Assert.Equal(1, result[0].Rank); // Urgent first
        }

        [Fact]
        public async Task GetStatusesAsync_ReturnsStatuses()
        {
            var sut = Build(nameof(GetStatusesAsync_ReturnsStatuses));
            var result = await sut.GetStatusesAsync();
            Assert.Equal(4, result.Count);
        }

        [Fact]
        public async Task GetStatusesAsync_ThrowsWhenEmpty()
        {
            var ctx = DbContextFactory.Create(nameof(GetStatusesAsync_ThrowsWhenEmpty));
            var sut = new LookupService(
                new Repository<int, Department>(ctx),
                new Repository<int, Role>(ctx),
                new Repository<int, Category>(ctx),
                new Repository<int, Priority>(ctx),
                new Repository<int, Status>(ctx));

            await Assert.ThrowsAsync<InvalidOperationException>(() => sut.GetStatusesAsync());
        }
    }
}
