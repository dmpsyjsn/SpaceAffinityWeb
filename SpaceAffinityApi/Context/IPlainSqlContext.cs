namespace SpaceAffinityApi.Context;

public interface IPlainSqlContext<T>
{
    public Task EnsureTableExists();
    public Task<T[]> GetData(int skip, int take);
    public Task<int> UpsertItem(T item);
}