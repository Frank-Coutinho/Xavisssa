using Microsoft.EntityFrameworkCore;
using Xavissa.Frontend.Data.Repositories;

namespace Xavissa.Frontend.Helpers
{
    public static class EfHelpers
    {
        public static void AttachOrDetach<TEntity>(this DbContext context, TEntity entity)
            where TEntity : class
        {
            var entry = context.Entry(entity);
            if (entry.State == EntityState.Detached)
            {
                entry.State = EntityState.Unchanged;
            }
        }
    }
}
