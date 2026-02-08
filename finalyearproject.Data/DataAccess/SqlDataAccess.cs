using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;

namespace finalyearproject.Data.DataAccess
{
    public class SqlDataAccess : ISqlDataAccess
    {
        private readonly IConfiguration _configuration;

        public SqlDataAccess(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<IEnumerable<T>> LoadDataAsync<T, U>(string storedProcedure, U parameters, string connectionId = "conn")
        {
            string connectionString = _configuration.GetConnectionString(connectionId);

            using IDbConnection connection = new SqlConnection(connectionString);
            return await connection.QueryAsync<T>(storedProcedure, parameters, commandType: CommandType.StoredProcedure);
        }

        public async Task<T> LoadSingleDataAsync<T, U>(string storedProcedure, U parameters, string connectionId = "conn")
        {
            string connectionString = _configuration.GetConnectionString(connectionId);

            using IDbConnection connection = new SqlConnection(connectionString);
            return await connection.QueryFirstOrDefaultAsync<T>(storedProcedure, parameters, commandType: CommandType.StoredProcedure);
        }

        public async Task<int> SaveDataAsync<T>(string storedProcedure, T parameters, string connectionId = "conn")
        {
            string connectionString = _configuration.GetConnectionString(connectionId);

            using IDbConnection connection = new SqlConnection(connectionString);
            return await connection.ExecuteAsync(storedProcedure, parameters, commandType: CommandType.StoredProcedure);
        }
    }
}
