using System.Data;
using appMpower.Orm.Objects;
using PlatformBenchmarks;

using MySqlConnector; 

namespace appMpower.Orm
{
    public static class RawDbMySql
    {
        private const int MaxBatch = 500;

        private static Random _random = new();

        //private static string _connectionString = "Server=tfb-database;Database=hello_world;User ID=benchmarkdbuser;Password=benchmarkdbpass;Pooling=true;";
        private static string _connectionString = "Server=tfb-database;Database=hello_world;User Id=benchmarkdbuser;Password=benchmarkdbpass;Maximum Pool Size=1024;SslMode=None;ConnectionReset=false;ConnectionIdlePingTime=900;ConnectionIdleTimeout=0;AutoEnlist=false;DefaultCommandTimeout=0;ConnectionTimeout=0;IgnorePrepare=false;";

        public static World LoadSingleQueryRow()
        {
            using MySqlConnection mySqlConnection = new(_connectionString);
            mySqlConnection.Open();

            using MySqlCommand mySqlCommand = CreateReadCommand(mySqlConnection);
            
            return ReadSingleRow(mySqlCommand);
        }

        internal static MySqlCommand CreateReadCommand(MySqlConnection mySqlConnection)
        {
            MySqlCommand mySqlCommand = new("SELECT * FROM world WHERE id=@id", mySqlConnection);
            MySqlParameter mySqlParameter = new("@id", MySqlDbType.Int32)
            {
                Value = _random.Next(1, 10001)
            };

            mySqlCommand.Parameters.Add(mySqlParameter);

            return mySqlCommand;
        }

        internal static World ReadSingleRow(MySqlCommand mySqlCommand)
        {
            using MySqlDataReader dataReader = mySqlCommand.ExecuteReader();

            dataReader.Read();

            var world = new World
            {
                Id = dataReader.GetInt32(0),
                RandomNumber = dataReader.GetInt32(1)
            };

            return world;
        }
    }
}