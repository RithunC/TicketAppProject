using System.Security.Claims;
using System.Text.Json;
using TicketWebApp.Interfaces;
using TicketWebApp.Models;

namespace TicketWebApp.Middleware
{
    public class AuditMiddleware
    {
        private readonly RequestDelegate _next;

        public AuditMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context, IAuditLogService audit)
        {
            var start = DateTime.UtcNow; //calculates how long req took

            await _next(context); //let request execute

            var controller = context.Request.RouteValues["controller"]?.ToString() ?? "";
            var action = context.Request.RouteValues["action"]?.ToString() ?? "";
            var entityId = context.Request.RouteValues["id"]?.ToString();

            if (controller == "AuditLogs") return; //prevent Infinite logging loop

            int status = context.Response.StatusCode; //get response status

            string friendlyAction = GetFriendlyAction(controller, action);
            string friendlyEntity = GetFriendlyEntity(controller);
            string description = GetDescription(friendlyAction, friendlyEntity, entityId);

            long? userId = long.TryParse(context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid)
                ? uid : null;

            string? userName = context.User.Identity?.Name;
            string? role = context.User.FindFirst(ClaimTypes.Role)?.Value;

            var log = new AuditLog //create auditlog object
            {
                ActorUserId = userId,
                ActorUserName = userName,
                ActorRole = role,

                Action = friendlyAction,
                EntityType = friendlyEntity,
                EntityId = entityId,

                Success = status >= 200 && status < 300,
                StatusCode = status,
                Message = description,  // ✅ human readable

                HttpMethod = context.Request.Method,
                Path = "",  // ✅ no API paths
                OccurredAtUtc = DateTime.UtcNow,

                MetadataJson = JsonSerializer.Serialize(new
                {
                    DurationMs = (DateTime.UtcNow - start).TotalMilliseconds
                })
            };

            await audit.LogAsync(log); //save log
        }

        private static string GetFriendlyAction(string controller, string action)
        {
            return (controller, action) switch
            {
                ("Auth", "Login") => "LOGIN",
                ("Auth", "Logout") => "LOGOUT",
                ("Tickets", "Create") => "TICKET_CREATED",
                ("Tickets", "Update") => "TICKET_UPDATED",
                ("Tickets", "Close") => "TICKET_CLOSED",
                ("Tickets", "Delete") => "TICKET_DELETED",
                ("Comments", "Add") => "COMMENT_ADDED",
                ("Users", "Register") => "USER_REGISTERED",
                ("Users", "Update") => "PROFILE_UPDATED",
                _ => action.ToUpper()
            };
        }

        private static string GetFriendlyEntity(string controller)
        {
            return controller switch
            {
                "Tickets" => "Ticket",
                "Comments" => "Comment",
                "Users" => "User",
                "Auth" => "User",
                _ => controller
            };
        }

        private static string GetDescription(string action, string entity, string? id)
        {
            return action switch
            {
                "LOGIN" => "User logged in",
                "LOGOUT" => "User logged out",
                "USER_REGISTERED" => "New user account created",
                "PROFILE_UPDATED" => "User profile updated",

                "TICKET_CREATED" => $"Ticket #{id} was created",
                "TICKET_UPDATED" => $"Ticket #{id} was updated",
                "TICKET_CLOSED" => $"Ticket #{id} was closed",
                "TICKET_DELETED" => $"Ticket #{id} was deleted",

                "COMMENT_ADDED" => $"Comment added to ticket #{id}",

                _ => $"{action} {entity}".Trim()
            };
        }
    }
}
