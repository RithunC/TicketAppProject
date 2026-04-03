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

        private static LookupService BuildEmpty(string db)
        {
            var ctx = DbContextFactory.Create(db);
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
            var result = await Build(nameof(GetDepartmentsAsync_ReturnsDepartments)).GetDepartmentsAsync();
            Assert.NotEmpty(result);
        }

        [Fact]
        public async Task GetDepartmentsAsync_ThrowsWhenEmpty()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                BuildEmpty(nameof(GetDepartmentsAsync_ThrowsWhenEmpty)).GetDepartmentsAsync());
        }

        [Fact]
        public async Task GetRolesAsync_ReturnsRoles()
        {
            var result = await Build(nameof(GetRolesAsync_ReturnsRoles)).GetRolesAsync();
            Assert.Equal(3, result.Count);
        }

        [Fact]
        public async Task GetRolesAsync_ThrowsWhenEmpty()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                BuildEmpty(nameof(GetRolesAsync_ThrowsWhenEmpty)).GetRolesAsync());
        }

        [Fact]
        public async Task GetCategoriesAsync_ReturnsCategories()
        {
            var result = await Build(nameof(GetCategoriesAsync_ReturnsCategories)).GetCategoriesAsync();
            Assert.NotEmpty(result);
        }

        [Fact]
        public async Task GetCategoriesAsync_ThrowsWhenEmpty()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                BuildEmpty(nameof(GetCategoriesAsync_ThrowsWhenEmpty)).GetCategoriesAsync());
        }

        [Fact]
        public async Task GetCategoriesAsync_ReturnsParentCategoryName()
        {
            var ctx = DbContextFactory.Create(nameof(GetCategoriesAsync_ReturnsParentCategoryName));
            ctx.Categories.Add(new Category { Id = 1, Name = "Parent" });
            ctx.Categories.Add(new Category { Id = 2, Name = "Child", ParentCategoryId = 1 });
            await ctx.SaveChangesAsync();

            var sut = new LookupService(
                new Repository<int, Department>(ctx),
                new Repository<int, Role>(ctx),
                new Repository<int, Category>(ctx),
                new Repository<int, Priority>(ctx),
                new Repository<int, Status>(ctx));

            var result = await sut.GetCategoriesAsync();

            var child = result.First(c => c.Name == "Child");
            Assert.Equal("Parent", child.ParentCategoryName);
        }

        [Fact]
        public async Task GetCategoriesAsync_ReturnsNullParentNameWhenNoParent()
        {
            var ctx = DbContextFactory.Create(nameof(GetCategoriesAsync_ReturnsNullParentNameWhenNoParent));
            ctx.Categories.Add(new Category { Id = 1, Name = "Root" });
            await ctx.SaveChangesAsync();

            var sut = new LookupService(
                new Repository<int, Department>(ctx),
                new Repository<int, Role>(ctx),
                new Repository<int, Category>(ctx),
                new Repository<int, Priority>(ctx),
                new Repository<int, Status>(ctx));

            var result = await sut.GetCategoriesAsync();

            Assert.Null(result[0].ParentCategoryName);
        }

        [Fact]
        public async Task GetPrioritiesAsync_ReturnsPrioritiesOrderedByRank()
        {
            var result = await Build(nameof(GetPrioritiesAsync_ReturnsPrioritiesOrderedByRank)).GetPrioritiesAsync();
            Assert.Equal(4, result.Count);
            Assert.Equal(1, result[0].Rank);
        }

        [Fact]
        public async Task GetPrioritiesAsync_ThrowsWhenEmpty()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                BuildEmpty(nameof(GetPrioritiesAsync_ThrowsWhenEmpty)).GetPrioritiesAsync());
        }

        [Fact]
        public async Task GetStatusesAsync_ReturnsStatuses()
        {
            var result = await Build(nameof(GetStatusesAsync_ReturnsStatuses)).GetStatusesAsync();
            Assert.Equal(4, result.Count);
        }

        [Fact]
        public async Task GetStatusesAsync_ThrowsWhenEmpty()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                BuildEmpty(nameof(GetStatusesAsync_ThrowsWhenEmpty)).GetStatusesAsync());
        }
    }
}
