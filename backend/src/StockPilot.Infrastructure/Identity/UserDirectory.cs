using Microsoft.EntityFrameworkCore;
using StockPilot.Application.Auth;
using StockPilot.Infrastructure.Data;

namespace StockPilot.Infrastructure.Identity;

public sealed class UserDirectory(ApplicationDbContext db) : IUserDirectory
{
    public async Task<IReadOnlyList<SessionUser>> ListAsync(CancellationToken cancellationToken)
    {
        // The initial access screen is deliberately limited to 50 accounts.
        var users = await db.Users.AsNoTracking().OrderBy(u => u.Email).Take(50)
            .Select(u => new { u.Id, u.Email, u.DisplayName }).ToListAsync(cancellationToken);
        var ids = users.Select(u => u.Id).ToArray();
        var assignments = await (from ur in db.UserRoles
            join role in db.Roles on ur.RoleId equals role.Id
            where ids.Contains(ur.UserId)
            select new { ur.UserId, role.Name }).ToListAsync(cancellationToken);
        return users.Select(u => new SessionUser(u.Id, u.Email!, u.DisplayName,
            assignments.Where(a => a.UserId == u.Id).Select(a => a.Name!).Order().ToArray())).ToArray();
    }
}
