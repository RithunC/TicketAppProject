# Complaint Ticket App - Technical Walkthrough Guide

## Project Overview

Full-stack complaint ticket management system with three roles: Admin, Agent, Employee.
- Frontend: Angular 17+ (standalone components, signals, lazy loading)
- Backend: ASP.NET Core Web API (.NET 10), EF Core, SQL Server
- Auth: JWT Bearer tokens
- Testing: xUnit + Moq (190 tests, 90%+ branch coverage)

---

## Architecture: Three-Tier

Presentation Layer  ->  Angular SPA (components, services, guards, interceptors)
Business Layer      ->  ASP.NET Controllers -> Services -> Interfaces
Data Layer          ->  EF Core Repository -> SQL Server (Code First)

Why three-tier? Separation of concerns. Each layer has one responsibility.
Frontend never talks to DB directly. Services never know about HTTP.
Each layer is independently testable and replaceable.

---

## Backend Project Structure

Models/          -> Entity classes (Ticket, User, Comment, etc.)
Models/DTOs/     -> Data Transfer Objects (what the API sends/receives)
Models/Common/   -> Shared types like PagedResult<T>
Contexts/        -> ComplaintContext (EF Core DbContext)
Interfaces/      -> Contracts for every service and repository
Repositories/    -> Generic Repository<K,T> implementation
Services/        -> Business logic (TicketService, AuthService, etc.)
Controllers/     -> HTTP endpoints, route handling, auth attributes
Middleware/      -> AuditMiddleware, ExceptionMiddleware, CorrelationIdMiddleware

---

## 1. Models - Entity Classes

Entity classes map directly to database tables via EF Core Code First.

    public class Ticket : IComparable<Ticket>, IEquatable<Ticket>
    {
        public long Id { get; set; }
        public int PriorityId { get; set; }
        public Priority? Priority { get; set; }   // navigation property
        public int StatusId { get; set; }
        public Status? Status { get; set; }
        public DateTime? DueAt { get; set; }
    }

Why IComparable and IEquatable?
Enables sorting and equality checks on entity collections without reflection.
This is OOP - implementing standard interfaces for value semantics.

Value types vs Reference types:
- int, bool, DateTime are value types stored on the STACK.
- string, Ticket, User are reference types stored on the HEAP.
- DateTime? is Nullable<DateTime> - wraps a value type to allow null.

Boxing/Unboxing:
When a value type like int is assigned to object, it gets boxed (copied to heap).
We avoid this by using generics (IRepository<K,T>) instead of object.

---

## 2. DTOs - Data Transfer Objects

DTOs are separate classes that define exactly what the API sends and receives.
The entity (Ticket) is never exposed directly - only TicketResponseDto is returned.

Why DTOs?
- Security: Never accidentally expose PasswordHash, internal fields.
- Flexibility: API shape can change without changing the DB schema.
- Validation: DTOs carry [Required], [MaxLength] attributes for model validation.

Example:
    public class TicketCreateDto {
        public int PriorityId { get; set; }
        public string Title { get; set; }
        public DateTime? DueAt { get; set; }
    }

    public class TicketResponseDto {
        public long Id { get; set; }
        public string StatusName { get; set; }
        public string PriorityName { get; set; }
        // no PasswordHash, no internal IDs exposed raw
    }

PagedResult<T> is a generic DTO for paginated responses:
    public class PagedResult<T> {
        public List<T> Items { get; set; }
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }

Used for ticket lists, audit logs - any endpoint returning many records.

---

## 3. DbContext - EF Core Code First

ComplaintContext inherits DbContext and defines all DbSets (tables).

    public class ComplaintContext : DbContext
    {
        public DbSet<Ticket> Tickets { get; set; }
        public DbSet<User> Users { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Ticket>()
                .HasOne(t => t.Priority)
                .WithMany(p => p.Tickets)
                .HasForeignKey(t => t.PriorityId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

Why Code First (not Database First)?
- Schema lives in code - version controlled with Git.
- Migrations track every schema change with a timestamp.
- Team members run "dotnet ef database update" to sync their local DB.
- No manual SQL scripts to maintain.
- OnDelete(Restrict) prevents accidental cascade deletes.

Migrations:
- dotnet ef migrations add <name>  -> creates a migration file
- dotnet ef database update        -> applies it to SQL Server
- Each migration has Up() and Down() methods for rollback.

---

## 4. Repository Pattern

Generic repository abstracts all data access behind an interface.

    public interface IRepository<K, T> where T : class
    {
        Task<T?> Get(K key);
        Task<T?> Add(T item);
        Task<T?> Update(K key, T item);
        Task<T?> Delete(K key);
        IQueryable<T> GetQueryable();
    }

    public class Repository<K, T> : IRepository<K, T> where T : class
    {
        private readonly ComplaintContext _context;
        public IQueryable<T> GetQueryable() => _context.Set<T>().AsQueryable();
    }

Why IQueryable<T> instead of IEnumerable<T>?
- IQueryable builds an expression tree - the WHERE/ORDER BY runs in SQL Server, not in memory.
- IEnumerable would load ALL rows into memory first, then filter in C#.
- .Where(t => t.StatusId == 1) with IQueryable = "SELECT ... WHERE StatusId=1" in SQL
- With IEnumerable = "SELECT * FROM Tickets" then filter 10,000 rows in RAM.

Why generic Repository<K,T>?
- One class handles ALL entities. No separate TicketRepository, UserRepository, etc.
- Registered in DI: builder.Services.AddScoped(typeof(IRepository<,>), typeof(Repository<,>));
- This is the Open/Closed Principle (SOLID) - open for extension, closed for modification.

---

## 5. Services - Business Logic

Services contain all business rules. Controllers just route HTTP to services.

SOLID Principles in Services:

S - Single Responsibility:
  TicketService only handles ticket operations.
  AuthService only handles login/register/password reset.
  AutoAssignmentService only handles assignment logic.

O - Open/Closed:
  IRepository<K,T> is open for extension (new entity types) without modifying Repository.cs.

L - Liskov Substitution:
  Any class implementing ITicketService can replace TicketService without breaking controllers.

I - Interface Segregation:
  ITicketService, ICommentService, IAttachmentService are separate.
  A controller that only needs comments only depends on ICommentService.

D - Dependency Inversion:
  Controllers depend on ITicketService (abstraction), not TicketService (concrete).
  This is why we can mock ITicketService in unit tests.

---

## 6. Dependency Injection - Transient vs Scoped vs Singleton

In Program.cs:
    builder.Services.AddScoped<ITicketService, TicketService>();
    builder.Services.AddScoped(typeof(IRepository<,>), typeof(Repository<,>));

Scoped: One instance per HTTP request. All services in the same request share the same DbContext.
  -> Used for: Services, Repositories, DbContext.
  -> Why: DbContext tracks changes per request. Sharing it within a request is correct.

Transient: New instance every time it is requested.
  -> Used for: Lightweight, stateless utilities.

Singleton: One instance for the entire app lifetime.
  -> Used for: Caching, configuration, logging.
  -> Risk: Cannot inject Scoped services into Singleton (scope mismatch).

Why Scoped for DbContext?
EF Core DbContext is NOT thread-safe. Scoped ensures one context per request,
preventing concurrent modification issues.

---

## 7. Controllers - HTTP Layer

Controllers handle routing, authorization, and delegate to services.

    [Route("api/tickets")]
    [ApiController]
    [Authorize]
    public class TicketsController : ControllerBase
    {
        private readonly ITicketService _tickets;
        private readonly IAutoAssignmentService _autoAssign;

        [HttpPost]
        [Authorize(Roles = "Employee")]
        public async Task<IActionResult> Create([FromBody] TicketCreateDto dto)
        {
            var created = await _tickets.CreateAsync(currentUserId, dto);
            await _autoAssign.AutoAssignAsync(created.Id, currentUserId, ...);
            return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
        }
    }

Return Codes used:
- 200 OK           -> successful GET, PATCH
- 201 Created      -> POST that creates a resource (with Location header)
- 400 Bad Request  -> validation failure, business rule violation
- 401 Unauthorized -> no/invalid JWT token
- 403 Forbidden    -> authenticated but wrong role
- 404 Not Found    -> resource does not exist
- 500 Internal Server Error -> unhandled exception

[ApiController] attribute:
- Automatically returns 400 if ModelState is invalid.
- Enables [FromBody] binding automatically.

Why ControllerBase not Controller?
ControllerBase has no View support. We are building an API, not MVC with Razor views.

---

## 8. Authentication & Authorization - JWT

Flow:
1. Employee POSTs username+password to /api/authentication/login
2. AuthService verifies password using PBKDF2 hash (PasswordService)
3. TokenService creates a JWT with claims: userId, username, role
4. Frontend stores JWT in localStorage
5. Every subsequent request sends: Authorization: Bearer <token>
6. JwtBearer middleware validates the token on every request
7. [Authorize(Roles = "Admin")] checks the role claim in the token

JWT Structure: header.payload.signature
- Header: algorithm (HS256)
- Payload: claims (nameid, unique_name, role, exp)
- Signature: HMAC-SHA256 of header+payload using secret key

Password Hashing (PBKDF2):
    var hash = _passwords.HashPassword(request.Password, null, out salt);
    user.PasswordHash = Convert.FromBase64String(hash);
    user.PasswordSalt = salt;

Why not MD5/SHA1? They are fast - attackers can brute-force billions/second.
PBKDF2 is intentionally slow (10,000 iterations) - makes brute force impractical.

Password Reset Flow:
1. POST /forgot-password with username/email
2. TokenService.CreatePasswordResetToken() creates a short-lived JWT (15 min)
   with purpose="pwdreset" claim
3. Token returned to client (in production: emailed)
4. POST /reset-password with token + new password
5. TryValidatePasswordResetToken() validates signature, expiry, and purpose claim
6. New password hashed and saved

---

## 9. Middleware Pipeline

Order in Program.cs matters:
    CorrelationIdMiddleware  -> attaches X-Correlation-Id to every request/response
    AuditMiddleware          -> logs every action to AuditLogs table
    ExceptionMiddleware      -> catches unhandled exceptions, logs, returns 500
    UseAuthentication()      -> validates JWT
    UseAuthorization()       -> checks [Authorize] attributes
    MapControllers()         -> routes to controller actions

CorrelationIdMiddleware:
- Generates a unique GUID per request (or reuses client-provided one)
- Stored in context.TraceIdentifier and response header X-Correlation-Id
- Enables tracing a request across logs

AuditMiddleware:
- Runs AFTER the request completes (await _next(context) first, then log)
- Records: who did what, when, how long it took, success/failure
- Skips logging its own endpoint to prevent infinite loop
- Stores DurationMs in MetadataJson (serialized JSON)

ExceptionMiddleware:
- Wraps entire pipeline in try/catch
- Logs to ILogger (console/file) AND ErrorLogs table
- Returns clean JSON: { "error": "Internal server error" }
- Never exposes stack traces to clients

---

## 10. LINQ & EF Core Queries

Example - QueryAsync with dynamic filters:
    var query = _ticketRepo.GetQueryable()
        .Include(t => t.Priority)
        .Include(t => t.Status)
        .AsQueryable();

    if (q.StatusId.HasValue)
        query = query.Where(t => t.StatusId == q.StatusId);

    var total = await query.CountAsync();  // SELECT COUNT(*) - no data loaded yet

    var items = await query
        .Skip((q.Page - 1) * q.PageSize)
        .Take(q.PageSize)
        .ToListAsync();  // NOW the SQL executes

Key LINQ concepts used:
- .Where()          -> filter rows (SQL WHERE)
- .Select()         -> project to DTO (SQL SELECT specific columns)
- .Include()        -> eager load navigation properties (SQL JOIN)
- .OrderBy()        -> sort (SQL ORDER BY)
- .Skip/Take()      -> pagination (SQL OFFSET/FETCH)
- .GroupBy()        -> aggregate (SQL GROUP BY) - used in AutoAssignment load calculation
- .AnyAsync()       -> existence check (SQL EXISTS)
- .FirstOrDefaultAsync() -> get one or null
- .CountAsync()     -> count without loading data

Why async/await everywhere?
- Database I/O is slow (network round trip to SQL Server).
- async/await releases the thread while waiting, allowing it to serve other requests.
- Without async: thread is blocked, server can handle fewer concurrent users.
- With async: thread returns to pool, handles other requests while DB query runs.
- Task<T> is the return type - represents a future value.

---

## 11. Auto-Assignment Logic

When a ticket is created, AutoAssignmentService picks the least-loaded agent:

    // 1. Find all active agents in the ticket's department
    var agents = await _userRepo.GetQueryable()
        .Where(u => u.IsActive && u.Role.Name == "Agent"
                    && (!depId.HasValue || u.DepartmentId == depId))
        .Select(u => new { u.Id })
        .ToListAsync();

    // 2. Count open tickets per agent (GroupBy in SQL)
    var openCounts = await _ticketRepo.GetQueryable()
        .Where(t => agentIds.Contains(t.CurrentAssigneeUserId.Value)
                    && !t.Status.IsClosedState)
        .GroupBy(t => t.CurrentAssigneeUserId.Value)
        .Select(g => new { UserId = g.Key, OpenCount = g.Count() })
        .ToListAsync();

    // 3. Build dictionary for O(1) lookup
    var loadDict = openCounts.ToDictionary(x => x.UserId, x => x.OpenCount);

    // 4. Pick agent with fewest open tickets (tie-break by ID)
    var chosenId = agents
        .Select(a => new { a.Id, Open = loadDict.TryGetValue(a.Id, out var c) ? c : 0 })
        .OrderBy(x => x.Open).ThenBy(x => x.Id)
        .First().Id;

Why .ToDictionary()? O(1) lookup vs O(n) list search. For 100 agents, dictionary is 100x faster.
Why arrow functions? Concise, no need for a named method for a one-liner transformation.
Why .Select() not a foreach? Functional style - transforms data without mutation.

LIVE CHANGE - Assign by Priority (most urgent ticket gets assigned to agent with lightest urgent load):
Change step 4 to:
    var urgencyDict = await _ticketRepo.GetQueryable()
        .Where(t => agentIds.Contains(t.CurrentAssigneeUserId.Value) && !t.Status.IsClosedState)
        .GroupBy(t => t.CurrentAssigneeUserId.Value)
        .Select(g => new { UserId = g.Key, MinRank = g.Min(t => t.Priority.Rank) })
        .ToListAsync();

    var chosenId = agents
        .Select(a => new { a.Id,
            Urgency = urgencyDict.FirstOrDefault(x => x.UserId == a.Id)?.MinRank ?? 999 })
        .OrderByDescending(x => x.Urgency)  // highest rank = least urgent = best to assign
        .ThenBy(x => x.Id)
        .First().Id;

---

## 12. Angular Architecture

Structure:
    src/app/
      pages/
        admin/       -> AdminDashboard, AdminTickets, AdminTicketDetail, AdminUsers, AuditLogs
        agent/       -> AgentDashboard, AgentTickets, AgentTicketDetail
        employee/    -> EmployeeDashboard, CreateTicket, MyTickets, EmpTicketDetail
        auth/        -> Login, Register, ForgotPassword, ResetPassword
        profile/     -> Profile
      services/      -> AuthService, TicketService, ReportService, LookupService, etc.
      guards/        -> authGuard, adminGuard, agentGuard, employeeGuard, guestGuard
      interceptors/  -> authInterceptor (attaches JWT, handles errors globally)
      models/        -> index.ts (all TypeScript interfaces)
      shared/        -> NavbarComponent (reused across all pages)

Standalone Components (Angular 17+):
Every component has standalone: true and imports its own dependencies.
No NgModule needed. This is the modern Angular approach.

    @Component({
      selector: 'app-employee-dashboard',
      standalone: true,
      imports: [RouterLink, NavbarComponent],
      templateUrl: './employee-dashboard.component.html'
    })

Why standalone? Simpler, less boilerplate, better tree-shaking (smaller bundle).

---

## 13. Angular Signals - Reactive State

Signals replace RxJS Subject/BehaviorSubject for simple state.

    // In AuthService:
    token       = signal<string | null>(localStorage.getItem('jwt_token'));
    currentUser = signal<UserLiteDto | null>(this._load());
    isAdmin     = computed(() => this.role() === 'Admin');

    // In component:
    summary = signal<TicketSummaryDto | null>(null);
    ngOnInit(): void {
        this.rpt.getTicketSummary().subscribe(s => this.summary.set(s));
    }

signal()    - reactive value. When it changes, Angular re-renders only parts that use it.
computed()  - derived value. isAdmin automatically updates when role() changes.

Why signals over @Input/@Output for service state?
Signals are globally accessible via inject(). No need to pass data through component chains.

---

## 14. Angular Routing & Lazy Loading

    export const routes: Routes = [
      { path: 'admin', canActivate: [adminGuard],
        children: [
          { path: 'dashboard',
            loadComponent: () => import('./pages/admin/dashboard/admin-dashboard.component')
                                 .then(m => m.AdminDashboardComponent) }
        ]
      }
    ];

Lazy Loading: loadComponent() uses dynamic import(). The component JavaScript bundle
is only downloaded when the user navigates to that route.
Why? Reduces initial bundle size. Employee never downloads Admin code.

canActivate Guards:
    export const adminGuard: CanActivateFn = () => {
        const auth = inject(AuthService);
        if (auth.isAdmin()) return true;
        if (auth.isAuthenticated()) { auth.redirectByRole(); return false; }
        router.navigate(['/login']);
        return false;
    };

Guards run before the component loads. If the user is not Admin, they are redirected.
This is client-side protection. The backend [Authorize(Roles="Admin")] is the real security.

---

## 15. HTTP Interceptor

The authInterceptor runs on EVERY HTTP request automatically.

    export const authInterceptor: HttpInterceptorFn = (req, next) => {
        const token = localStorage.getItem('jwt_token');
        const cloned = token
            ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
            : req;

        loading.show();

        return next(cloned).pipe(
            catchError((err: HttpErrorResponse) => {
                switch (err.status) {
                    case 401: router.navigate(['/login']); break;
                    case 403: toast.error('Access denied.'); break;
                    case 500: toast.error('Server error.'); break;
                }
                return throwError(() => err);
            }),
            finalize(() => loading.hide())
        );
    };

Why interceptor instead of adding headers in every service?
DRY principle. One place handles auth headers, loading spinner, and error toasts.
Without interceptor: every service method would need try/catch and header logic.

finalize() runs whether the request succeeds or fails - ensures loading spinner always hides.

---

## 16. Angular Data Binding & Event Handling

Two-way binding (forms):
    <input [(ngModel)]="form.title" />
[(ngModel)] = [ngModel] (property binding) + (ngModelChange) (event binding) combined.

Property binding:
    <div [style.width.%]="pct(summary()!.urgentPriority, summary()!.total)"></div>
Evaluates the expression and sets the DOM property dynamically.

Event binding:
    <button (click)="onSubmit()">Submit</button>
Calls the component method when the event fires.

@if / @for (Angular 17 control flow):
    @if (summary()) {
        <div>{{ summary()!.total }}</div>
    }
    @for (ticket of tickets(); track ticket.id) {
        <div>{{ ticket.title }}</div>
    }

Why track? Tells Angular which item is which when the list changes.
Without track, Angular re-renders the entire list. With track, only changed items re-render.

Interpolation: {{ summary()!.total }} - reads the signal value and renders it as text.

---

## 17. Angular Forms - Why No Built-in Validators?

The create-ticket form uses ngModel (template-driven) with manual validation:
    onSubmit(): void {
        if (!this.form.priorityId || !this.form.title.trim()) return;
    }

Why not Validators.required from ReactiveFormsModule?
- The form has conditional fields (department, category are optional).
- Custom validation logic (title must not be only whitespace) is cleaner in TypeScript.
- The backend also validates - frontend validation is just UX, not security.

Why not ReactiveFormsModule?
Template-driven forms are simpler for straightforward forms.
Reactive forms add complexity (FormGroup, FormControl, FormBuilder) not needed here.

---

## 18. Inter-Component Communication

NavbarComponent is shared across all dashboards:
    // navbar.component.ts
    auth = inject(AuthService);
    // reads auth.currentUser() signal directly - no @Input needed

Parent -> Child: @Input() (not used much - signals preferred for service state)
Child -> Parent: @Output() EventEmitter
Service (shared state): AuthService signals are read by any component that injects it.

Why signals over @Input/@Output for auth state?
Auth state is global. Passing it through @Input chains would require every parent
to receive and pass it down. Signals in a service are accessible anywhere.

---

## 19. CSS & Style Library

Bootstrap Icons (bi bi-*) used for all icons:
    <i class="bi bi-plus-circle-fill"></i>
Why Bootstrap Icons not Font Awesome? Lighter, no JS dependency, pure CSS.

Custom CSS with BEM-like naming:
    .sp { }          /* stat panel */
    .sp-blue { }     /* blue variant */
    .sp-link { }     /* clickable variant */

No CSS framework (no Bootstrap grid) - custom flexbox/grid layouts.
Why? Full control over design, no unused CSS shipped to browser.

---

## 20. Swagger / OpenAPI

Configured in Program.cs:
    builder.Services.AddSwaggerGen();
    app.UseSwagger();
    app.UseSwaggerUI();

Available at: http://localhost:5000/swagger
Shows all endpoints, request/response schemas, allows testing with JWT.

To test authenticated endpoints in Swagger:
1. Call /api/authentication/login, copy the token
2. Click "Authorize" button, enter: Bearer <token>
3. All subsequent requests include the Authorization header

Why Swagger is mandatory:
- Self-documenting API - no separate docs needed
- Frontend and backend teams can work in parallel
- QA can test without Postman setup
- Follows OpenAPI 3.0 standard

---

## 21. Unit Testing - xUnit + Moq

190 tests, 90%+ branch coverage across all services.

Test structure (AAA pattern):
    [Fact]
    public async Task CreateAsync_CreatesTicketWithNewStatus()
    {
        // Arrange
        var (sut, ctx) = Build(nameof(CreateAsync_CreatesTicketWithNewStatus));
        ctx.Users.Add(MakeUser(1, "creator"));
        await ctx.SaveChangesAsync();

        // Act
        var result = await sut.CreateAsync(1, new TicketCreateDto { Title = "T", PriorityId = 1 });

        // Assert
        Assert.Equal("New", result.StatusName);
    }

Why InMemory database not Moq for repository?
InMemory EF Core database tests the actual LINQ queries.
Moq would mock the repository - you would only test that the service calls the right method,
not that the query actually works.

Why Moq for IFormFile (attachments)?
IFormFile is an interface. We cannot create a real file in a unit test.
Moq creates a fake implementation that returns controlled data.

    var mock = new Mock<IFormFile>();
    mock.Setup(f => f.FileName).Returns("test.txt");
    mock.Setup(f => f.ContentType).Returns("text/plain");

Why arrow functions in tests?
    await Assert.ThrowsAsync<Exception>(() => sut.UploadAsync(0, 1, file));
The lambda () => ... is passed as a Func<Task>. Arrow functions (lambdas) are used
because we need to pass the method call as a value, not execute it immediately.

DbContextFactory.CreateWithSeed():
Seeds roles, departments, statuses, priorities before each test.
Each test gets its own in-memory database (unique name = test method name).
This ensures tests are isolated and do not affect each other.

---

## 22. OOP Concepts Used

Inheritance:
- ComplaintContext : DbContext (inherits EF Core base class)
- ControllerBase is the base for all controllers
- Repository<K,T> can be extended for entity-specific repos

Interfaces (Abstraction):
- ITicketService, IRepository<K,T>, IAuthService, etc.
- Controllers depend on interfaces, not concrete classes
- Enables mocking in tests, swapping implementations without changing callers

Encapsulation:
- PasswordHash is never in any DTO - only in the User entity
- Services expose only what controllers need via DTOs
- private static ToResponse() helper - internal mapping logic hidden from callers

Polymorphism:
- IActionResult return type - can return Ok(), NotFound(), BadRequest() from same method
- IRepository<K,T> - same interface works for Ticket, User, Comment, etc.

virtual / abstract / sealed:
- virtual: can be overridden in derived class (EF Core uses this for lazy loading proxies)
- abstract: must be overridden (DbContext.OnModelCreating is abstract - we override it)
- sealed: cannot be inherited - prevents unintended extension of final classes

---

## 23. Walk-Through: Employee Role

1. Navigate to /login
   - Template-driven form with [(ngModel)] binding
   - POST /api/authentication/login
   - JWT returned, stored in localStorage
   - handleLoginSuccess() decodes JWT payload (base64 decode of middle segment)
   - Navigates to /employee/dashboard

2. Employee Dashboard (/employee/dashboard)
   - ngOnInit() calls ReportService.getTicketSummary()
   - GET /api/reports/tickets/summary
   - Backend: ReportService filters tickets WHERE CreatedByUserId = currentUserId
   - Returns: total, open, inProgress, resolved, overdue counts
   - Overdue = DueAt < DateTime.UtcNow AND status not Closed/Resolved
   - Stats displayed as clickable cards linking to filtered ticket list

3. Submit Ticket (/employee/create-ticket)
   - Loads departments, categories, priorities from /api/lookups/*
   - Form submit: POST /api/tickets
   - Controller: [Authorize(Roles = "Employee")] - only employees can create
   - TicketService.CreateAsync() sets StatusId = "New" status
   - AutoAssignmentService.AutoAssignAsync() immediately assigns to least-loaded agent
   - Returns 201 Created with Location header pointing to new ticket

4. My Tickets (/employee/my-tickets)
   - GET /api/tickets/employee/{employeeId}
   - Backend queries tickets WHERE CreatedByUserId = employeeId
   - Client-side filtering by status using .filter() on the items array
   - isOverdue(dueAt) = new Date(dueAt) < new Date() - local time comparison
   - Pagination: page signal, computed totalPages

5. Ticket Detail (/employee/tickets/:id)
   - GET /api/tickets/{id}
   - GET /api/tickets/{id}/history (status change timeline)
   - GET /api/comments/{ticketId}
   - GET /api/attachments/{ticketId}
   - Employee can add comments, upload attachments, edit their own ticket title/description
   - Cannot change department/category (controller strips those fields for Employee role)

---

## 24. Walk-Through: Agent Role

1. Agent Dashboard (/agent/dashboard)
   - GET /api/tickets/agent/{agentId} - all tickets assigned to this agent
   - Frontend calculates stats locally using .filter() and arrow functions:
       overdue: items.filter(t =>
           !!t.dueAt && new Date(t.dueAt) < now &&
           t.status.toLowerCase() !== 'closed' &&
           t.status.toLowerCase() !== 'resolved').length
   - Why calculate on frontend? The agent dashboard only shows THEIR tickets.
     The backend already filtered by agentId. No need for another API call.

2. Agent Ticket Queue (/agent/tickets)
   - POST /api/tickets/query with filters (status, priority, department, date range)
   - Why POST for query? The filter object is complex - too many params for GET query string.
     POST body is cleaner and not limited in size.
   - Pagination: page, pageSize sent in request body
   - Sort by: createdAt, priority, dueAt - backend switch expression handles all cases

3. Agent Ticket Detail (/agent/tickets/:id)
   - Agent can: change status, reassign ticket, add comments (including internal notes)
   - POST /api/tickets/{id}/status - changes status, records in TicketStatusHistory
   - POST /api/tickets/{id}/assign - manual reassignment
   - POST /api/tickets/{id}/autoAssign - trigger auto-assignment again
   - Internal comments: IsInternal=true - only visible to Admin/Agent, not Employee

---

## 25. Walk-Through: Admin Role

1. Admin Dashboard (/admin/dashboard)
   - GET /api/reports/tickets/summary - global view (all tickets, all users)
   - isAdmin=true in ReportService means no WHERE filter on CreatedByUserId
   - Priority breakdown bar chart (CSS width calculated as percentage)

2. All Tickets (/admin/tickets)
   - Same POST /api/tickets/query as agent
   - Admin can filter by any field, see all tickets from all employees

3. Users (/admin/users)
   - GET /api/users - all users
   - Client-side search and role filter using .filter() on the users array
   - Why client-side filter? User list is small (tens/hundreds), no need for server round trip

4. Audit Logs (/admin/audit-logs)
   - GET /api/auditlogs with date range, action filter, pagination
   - AuditMiddleware recorded every action automatically
   - DurationMs extracted from MetadataJson using JsonDocument.Parse()
   - Why JSON for metadata? Flexible - can add new fields without schema migration

---

## 26. Overdue Ticket Fix (UTC Bug)

Problem: datetime-local input in browser gives local time string (no timezone).
Example: "2026-03-31T10:00" - no Z suffix, no offset.
Backend compared with DateTime.UtcNow - tickets appeared "not overdue" for UTC+ users.

Fix applied:
Frontend (create-ticket.component.ts):
    dueAt: this.form.dueAt ? new Date(this.form.dueAt).toISOString() : undefined

new Date("2026-03-31T10:00") creates a Date in local time.
.toISOString() converts to UTC: "2026-03-31T04:30:00.000Z"

Backend (TicketService.cs):
    DueAt = dto.DueAt.HasValue
        ? DateTime.SpecifyKind(dto.DueAt.Value.ToUniversalTime(), DateTimeKind.Utc)
        : null

ToUniversalTime() converts to UTC. SpecifyKind marks it as UTC so EF Core stores it correctly.

---

## 27. Possible Live Change Questions & Answers

Q: Change auto-assignment from least-loaded to round-robin.
A: Track last assignment time per agent. Sort agents by their last assignment timestamp
   ascending - oldest assignment gets next ticket.
   Add LastAssignedAt to User model or query TicketAssignments for last assignment per agent.
   In AutoAssignmentService, replace the loadDict logic with a lastAssignedDict.

Q: Change auto-assignment to assign based on ticket priority.
A: Add SeniorityLevel field to User. In AutoAssignmentService:
   - If ticket.Priority.Rank == 1 (Urgent), filter agents WHERE SeniorityLevel == "Senior"
   - Otherwise use existing least-loaded logic.

Q: Add a "Reopen" status transition - only Admin can reopen a closed ticket.
A: In TicketsController.UpdateStatus():
   - Check if current status is Closed and new status is "Open"
   - If so, verify User.IsInRole("Admin") - return 403 if not Admin
   - Otherwise proceed with UpdateStatusAsync()

Q: Prevent employees from commenting on tickets not assigned to them.
A: In CommentService.AddAsync():
   - Load the ticket with CurrentAssigneeUserId
   - If poster is Employee AND ticket.CreatedByUserId != postedByUserId, throw InvalidOperationException

Q: Add email notification when a ticket is assigned.
A: In AutoAssignmentService after AssignAsync():
   - Inject IEmailService (new interface)
   - Call emailService.SendAsync(agent.Email, "New ticket assigned: " + ticket.Title)
   - This follows Open/Closed - no changes to existing assignment logic

Q: Add ticket priority escalation - if open for more than 48 hours, upgrade priority.
A: Add a background service (IHostedService) that runs every hour:
   - Query tickets WHERE StatusId = "Open" AND CreatedAt < DateTime.UtcNow.AddHours(-48)
   - AND PriorityId != Urgent
   - Update PriorityId to Urgent, log the change in TicketStatusHistory

Q: Return 422 Unprocessable Entity instead of 400 for business rule violations.
A: In controllers, catch InvalidOperationException and return:
   return UnprocessableEntity(new { error = ex.Message });
   This follows REST standards - 400 is for malformed requests, 422 for semantic errors.

Q: Limit each employee to maximum 5 open tickets at a time.
A: In TicketService.CreateAsync(), before creating:
   var openCount = await _ticketRepo.GetQueryable()
       .CountAsync(t => t.CreatedByUserId == createdByUserId && !t.Status.IsClosedState);
   if (openCount >= 5)
       throw new InvalidOperationException("Maximum 5 open tickets allowed.");

---

## 28. Key Concepts Quick Reference

async/await:
  Why: Database I/O is slow. async releases the thread while waiting.
  Without it: thread blocked, fewer concurrent users handled.
  Task<T> = represents a future value. await unwraps it when ready.

Arrow functions (lambdas):
  Why: Concise syntax for short transformations. Captures outer scope (closure).
  .Select(t => new { t.Id }) is cleaner than a named method for a one-liner.
  In tests: () => sut.UploadAsync(0,1,file) passes the call as a value, not executing it.

.filter() vs .map() in TypeScript:
  .filter() - returns subset of array matching condition (like SQL WHERE)
  .map()     - transforms each element to a new shape (like SQL SELECT)
  Why not for-loop? Functional style is more readable, composable, and testable.

Why interfaces everywhere:
  Testability - can mock ITicketService in unit tests.
  Flexibility - swap SQL Server for PostgreSQL by changing Repository, not services.
  SOLID D - depend on abstractions, not concretions.

Why not 15+ lines in a method:
  Single Responsibility - if a method is long, it is doing too many things.
  Readability - short methods are easier to understand and test.
  Example: ToResponse() is a private static helper that maps Ticket -> TicketResponseDto.
  Extracted so CreateAsync, GetAsync, UpdateAsync all reuse it without duplication.

Transient vs Scoped vs Singleton:
  Scoped    = per HTTP request (DbContext, Services)
  Transient = new every injection (lightweight utilities)
  Singleton = one for app lifetime (config, caching)

Code First vs Database First:
  Code First: schema in C# classes, migrations track changes, version controlled.
  Database First: generate classes from existing DB - harder to maintain, no migration history.

REST API Standards:
  GET    = read (idempotent, safe)
  POST   = create or complex query
  PATCH  = partial update
  DELETE = remove
  Proper status codes: 200, 201, 400, 401, 403, 404, 500

---

## 29. Advanced Concepts

CQRS (Command Query Responsibility Segregation):
  The project partially follows CQRS:
  - Commands (write): CreateAsync, UpdateAsync, AssignAsync - change state
  - Queries (read): GetAsync, QueryAsync, GetStatusHistoryAsync - read only
  Full CQRS would use separate read/write models and databases.
  Our QueryAsync uses a lightweight TicketListItemDto (not the full TicketResponseDto).

Domain-Driven Design (DDD):
  Ticket is the aggregate root - all ticket operations go through TicketService.
  You never directly update TicketAssignment without going through AssignAsync.
  Status changes always go through UpdateStatusAsync which records history.

Clean Code principles applied:
  - Method names describe intent: CreateAsync, AutoAssignAsync, GetStatusHistoryAsync
  - No magic numbers: statusId:1 replaced by looking up "New" by name
  - Private helpers: ToResponse(), IsStaff(), ResolvePhysicalPath()
  - Guard clauses: if (ticket == null) return null; - fail fast, no deep nesting

Event-Driven Architecture (concept):
  Currently: synchronous - ticket created, then auto-assigned in same request.
  Event-driven: ticket creation publishes a "TicketCreated" event to Azure Service Bus.
  AutoAssignment service subscribes and processes asynchronously.
  Benefit: decoupled, scalable, resilient to assignment service failures.

Azure Cloud (concepts):
  Key Vault:       Store JWT secret, connection string - not in appsettings.json
  Blob Storage:    Store attachments instead of wwwroot/uploads folder
  Service Bus:     Event-driven assignment notifications
  App Service:     Host the ASP.NET API
  Static Web Apps: Host the Angular frontend

API Versioning:
  Currently not implemented. Would add:
  [Route("api/v1/tickets")] or use Microsoft.AspNetCore.Mvc.Versioning package.
  Why: allows breaking changes without breaking existing clients.

Micro-services (concept):
  Currently monolithic - all services in one API.
  Could split into: TicketService API, UserService API, NotificationService API.
  Each deployed independently, communicates via HTTP or Service Bus.
  Overkill for this app size - monolith is the right choice here.

---

## 30. Swagger Demo Steps

1. Open http://localhost:5000/swagger
2. Find POST /api/authentication/login
3. Click "Try it out", enter: { "userName": "admin", "password": "Admin@123" }
4. Execute - copy the token from response
5. Click "Authorize" button (top right)
6. Enter: Bearer eyJhbGci... (paste your token)
7. Click Authorize, then Close
8. Now all endpoints will include the Authorization header automatically
9. Test GET /api/reports/tickets/summary - should return summary data
10. Test POST /api/tickets/query - should return paginated ticket list

---

## 31. Key Talking Points During Demo

When creating a ticket:
  "Notice the ticket is automatically assigned to an agent - this is AutoAssignmentService
   which uses LINQ GroupBy to count open tickets per agent and picks the least loaded one.
   The assignment happens in the same HTTP request, synchronously."

When showing the dashboard:
  "The overdue count was previously wrong because datetime-local inputs send local time
   but the backend compared with UTC. I fixed this by converting to ISO UTC string on
   the frontend before sending, and normalizing with ToUniversalTime() on the backend."

When showing audit logs:
  "Every action is logged automatically by AuditMiddleware - it runs after the request
   completes, records who did what, how long it took, and whether it succeeded.
   The duration is stored as JSON in MetadataJson and parsed in memory, not in SQL,
   because JSON functions in SQL Server are not portable and harder to test."

When showing the code:
  "I follow SOLID principles - every service depends on an interface, not a concrete class.
   This is why I have 190 unit tests - I can mock ITicketService and test each layer
   independently without a real database. Branch coverage is above 90% for all services."

When asked about async:
  "Every database call is async because database I/O involves a network round trip to SQL Server.
   async/await releases the thread while waiting, so the server can handle other requests
   concurrently. Without async, each request would block a thread for the entire duration."

When asked about Code First:
  "I chose Code First because the schema lives in version control alongside the code.
   Every schema change is a migration file with Up() and Down() methods.
   The team just runs dotnet ef database update - no manual SQL scripts, no drift."

When asked about the Repository pattern:
  "The generic Repository<K,T> means I wrote data access logic once for all entities.
   Services never call DbContext directly - they go through IRepository.
   This means I can test services with an in-memory database without changing any service code."

---
