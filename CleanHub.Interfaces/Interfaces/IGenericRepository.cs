using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace CleanHub.Core.Interfaces
{
    public interface IGenericRepository<TEntity> where TEntity : class
    {
        IEnumerable<TEntity> Get(
          Expression<Func<TEntity, bool>> filter = null,
          Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>> orderBy = null,
          string includeProperties = "");
        TEntity GetById(int entityId);
        void Insert(TEntity entity);
        void Delete(int entityId);
        void Update(TEntity entity);
    }
}