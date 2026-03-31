using Microsoft.EntityFrameworkCore;
using TicketWebApp.Contexts;
using TicketWebApp.Models;

namespace TestProject.Helpers
{
    public static class DbContextFactory
    {
        public static ComplaintContext Create(string dbName)
        {
            var options = new DbContextOptionsBuilder<ComplaintContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
            return new ComplaintContext(options);
        }

        public static ComplaintContext CreateWithSeed(string dbName)
        {
            var ctx = Create(dbName);

            var adminRole = new Role { Id = 1, Name = "Admin" };
            var agentRole = new Role { Id = 2, Name = "Agent" };
            var employeeRole = new Role { Id = 3, Name = "Employee" };
            ctx.Roles.AddRange(adminRole, agentRole, employeeRole);

            var dept = new Department { Id = 1, Name = "IT" };
            ctx.Departments.Add(dept);

            ctx.Statuses.AddRange(
                new Status { Id = 1, Name = "New", IsClosedState = false },
                new Status { Id = 2, Name = "In Progress", IsClosedState = false },
                new Status { Id = 3, Name = "Resolved", IsClosedState = true },
                new Status { Id = 4, Name = "Closed", IsClosedState = true }
            );

            ctx.Priorities.AddRange(
                new Priority { Id = 1, Name = "Urgent", Rank = 1, ColorHex = "#FF0000" },
                new Priority { Id = 2, Name = "High", Rank = 2, ColorHex = "#FF8800" },
                new Priority { Id = 3, Name = "Medium", Rank = 3, ColorHex = "#FFFF00" },
                new Priority { Id = 4, Name = "Low", Rank = 4, ColorHex = "#00FF00" }
            );

            ctx.Categories.Add(new Category { Id = 1, Name = "Network" });

            ctx.SaveChanges();
            return ctx;
        }
    }
}
