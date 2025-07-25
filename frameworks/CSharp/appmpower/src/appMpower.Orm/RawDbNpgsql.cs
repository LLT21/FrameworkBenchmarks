using appMpower.Orm.Objects;

using Npgsql; 

namespace appMpower.Orm
{
    public static class RawDbNpgsql
    {
        private static Random _random = new();
        //private static string _connectionString = "Server=tfb-database;Database=hello_world;User Id=benchmarkdbuser;Password=benchmarkdbpass;SSL Mode=Disable;Maximum Pool Size=512;NoResetOnClose=true;Enlist=false;Max Auto Prepare=4";
        private static string _connectionString = "Server=tfb-database;Database=hello_world;User Id=benchmarkdbuser;Password=benchmarkdbpass;SSL Mode=Disable;";

        public static World LoadSingleQueryRow()
        {
            using NpgsqlConnection npgsqlConnection = new(_connectionString);
            npgsqlConnection.Open();

            using NpgsqlCommand npgsqlCommand = CreateReadCommand(npgsqlConnection);

            return ReadSingleRow(npgsqlCommand);
        }

        internal static NpgsqlCommand CreateReadCommand(NpgsqlConnection npgsqlConnection)
        {
            NpgsqlCommand npgsqlCommand = new("SELECT * FROM world WHERE id=@id", npgsqlConnection);
            NpgsqlParameter npgsqlParameter = new("@id", NpgsqlTypes.NpgsqlDbType.Integer)
            {
                Value = _random.Next(1, 10001)
            };

            npgsqlCommand.Parameters.Add(npgsqlParameter);

            return npgsqlCommand;
        }

        internal static World ReadSingleRow(NpgsqlCommand npgsqlCommand)
        {
            using NpgsqlDataReader npgsqlDataReader = npgsqlCommand.ExecuteReader();

            npgsqlDataReader.Read();

            var world = new World
            {
                Id = npgsqlDataReader.GetInt32(0),
                RandomNumber = npgsqlDataReader.GetInt32(1)
            };

            return world;
        }
    }
}