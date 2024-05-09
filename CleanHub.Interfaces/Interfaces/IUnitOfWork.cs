namespace CleanHub.Core.Interfaces
{
    public interface IUnitOfWork<TEntity> : IDisposable where TEntity : class
    {
    }
}
