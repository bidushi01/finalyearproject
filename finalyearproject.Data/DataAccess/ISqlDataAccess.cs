namespace finalyearproject.Data.DataAccess
{
    public interface ISqlDataAccess
    {
        Task<IEnumerable<T>> LoadDataAsync<T, U>(string storedProcedure, U parameters, string connectionId = "conn");
        Task<T> LoadSingleDataAsync<T, U>(string storedProcedure, U parameters, string connectionId = "conn");
        Task<int> SaveDataAsync<T>(string storedProcedure, T parameters, string connectionId = "conn");
    }
}
