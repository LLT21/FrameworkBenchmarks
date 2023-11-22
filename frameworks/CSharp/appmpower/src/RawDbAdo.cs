// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if ADO

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using PlatformBenchmarks;
using Npgsql;

namespace appMpower;

public static class RawDb
{
    private static readonly ConcurrentRandom _random = new ConcurrentRandom();
    private static readonly MemoryCache _cache
        = new(new MemoryCacheOptions { ExpirationScanFrequency = TimeSpan.FromMinutes(60) });

    private static readonly NpgsqlDataSource _dataSource = NpgsqlDataSource.Create(appMpower.Data.DbProviderFactory.ConnectionString);

    public static async Task<World> LoadSingleQueryRow()
    {
        using var connection = await _dataSource.OpenConnectionAsync();

        var (cmd, _) = CreateReadCommand(connection);
        using var command = cmd;

        return await ReadSingleRow(cmd);
    }

    public static Task<CachedWorld[]> LoadCachedQueries(int count)
    {
        var result = new CachedWorld[count];
        var cacheKeys = _cacheKeys;
        var cache = _cache;
        var random = _random;
        for (var i = 0; i < result.Length; i++)
        {
            var id = random.Next(1, 10001);
            var key = cacheKeys[id];
            if (cache.TryGetValue(key, out var cached))
            {
                result[i] = (CachedWorld)cached;
            }
            else
            {
                return LoadUncachedQueries(id, i, count, result);
            }
        }

        return Task.FromResult(result);

        static async Task<CachedWorld[]> LoadUncachedQueries(int id, int i, int count, CachedWorld[] result)
        {
            using var connection = await _dataSource.OpenConnectionAsync();

            var (cmd, idParameter) = CreateReadCommand(connection);
            using var command = cmd;
            async Task<CachedWorld> create(ICacheEntry _) => await ReadSingleRow(cmd);

            var cacheKeys = _cacheKeys;
            var key = cacheKeys[id];

            idParameter.TypedValue = id;

            for (; i < result.Length; i++)
            {
                result[i] = await _cache.GetOrCreateAsync(key, create);

                id = _random.Next(1, 10001);
                idParameter.TypedValue = id;
                key = cacheKeys[id];
            }

            return result;
        }
    }

    public static async Task PopulateCache()
    {
        using var connection = await _dataSource.OpenConnectionAsync();

        var (cmd, idParameter) = CreateReadCommand(connection);
        using var command = cmd;

        var cacheKeys = _cacheKeys;
        var cache = _cache;
        for (var i = 1; i < 10001; i++)
        {
            idParameter.TypedValue = i;
            cache.Set<CachedWorld>(cacheKeys[i], await ReadSingleRow(cmd));
        }

        Console.WriteLine("Caching Populated");
    }

    public static async Task<World[]> LoadMultipleQueriesRows(int count)
    {
        var results = new World[count];

        using var connection = await _dataSource.OpenConnectionAsync();

        using var batch = new NpgsqlBatch(connection)
        {
            // Inserts a PG Sync message between each statement in the batch, required for compliance with
            // TechEmpower general test requirement 7
            // https://github.com/TechEmpower/FrameworkBenchmarks/wiki/Project-Information-Framework-Tests-Overview
            EnableErrorBarriers = true
        };

        for (var i = 0; i < count; i++)
        {
            batch.BatchCommands.Add(new()
            {
                CommandText = "SELECT id, randomnumber FROM world WHERE id = $1",
                Parameters = { new NpgsqlParameter<int> { TypedValue = _random.Next(1, 10001) } }
            });
        }

        using var reader = await batch.ExecuteReaderAsync();

        for (var i = 0; i < count; i++)
        {
            await reader.ReadAsync();
            results[i] = new World { Id = reader.GetInt32(0), RandomNumber = reader.GetInt32(1) };
            await reader.NextResultAsync();
        }

        return results;
    }

    public static async Task<World[]> LoadMultipleUpdatesRows(int count)
    {
        var results = new World[count];

        using var connection = CreateConnection();
        await connection.OpenAsync();

        var (queryCmd, queryParameter) = CreateReadCommand(connection);
        using (queryCmd)
        {
            for (var i = 0; i < results.Length; i++)
            {
                results[i] = await ReadSingleRow(queryCmd);
                queryParameter.TypedValue = _random.Next(1, 10001);
            }
        }

        using (var updateCmd = new NpgsqlCommand(BatchUpdateString.Query(count), connection))
        {
            for (var i = 0; i < results.Length; i++)
            {
                var randomNumber = _random.Next(1, 10001);

                updateCmd.Parameters.Add(new NpgsqlParameter<int> { TypedValue = results[i].Id });
                updateCmd.Parameters.Add(new NpgsqlParameter<int> { TypedValue = randomNumber });

                results[i].RandomNumber = randomNumber;
            }

            await updateCmd.ExecuteNonQueryAsync();
        }

        return results;
    }

    public static async Task<List<Fortune>> LoadFortunesRows()
    {
        // Benchmark requirements explicitly prohibit pre-initializing the list size
        var result = new List<Fortune>();

        using var connection = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand("SELECT id, message FROM fortune", connection);
        using var rdr = await cmd.ExecuteReaderAsync();

        while (await rdr.ReadAsync())
        {
            result.Add(new Fortune
            (
                id: rdr.GetInt32(0),
                message: rdr.GetFieldValue<byte[]>(1).ToString()
            ));
        }

        result.Add(new Fortune(id: 0, AdditionalFortune.ToString()));
        result.Sort();

        return result;
    }

    private static readonly byte[] AdditionalFortune = "Additional fortune added at request time."u8.ToArray();

    private static (NpgsqlCommand readCmd, NpgsqlParameter<int> idParameter) CreateReadCommand(NpgsqlConnection connection)
    {
        var cmd = new NpgsqlCommand("SELECT id, randomnumber FROM world WHERE id = $1", connection);
        var parameter = new NpgsqlParameter<int> { TypedValue = _random.Next(1, 10001) };

        cmd.Parameters.Add(parameter);

        return (cmd, parameter);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static async Task<World> ReadSingleRow(NpgsqlCommand cmd)
    {
        using var rdr = await cmd.ExecuteReaderAsync(System.Data.CommandBehavior.SingleRow);
        await rdr.ReadAsync();

        return new World
        {
            Id = rdr.GetInt32(0),
            RandomNumber = rdr.GetInt32(1)
        };
    }

    private static NpgsqlConnection CreateConnection() => _dataSource.CreateConnection();

    private static readonly object[] _cacheKeys = Enumerable.Range(0, 10001).Select((i) => new CacheKey(i)).ToArray();

    public sealed class CacheKey : IEquatable<CacheKey>
    {
        private readonly int _value;

        public CacheKey(int value)
            => _value = value;

        public bool Equals(CacheKey key)
            => key._value == _value;

        public override bool Equals(object obj)
            => ReferenceEquals(obj, this);

        public override int GetHashCode()
            => _value;

        public override string ToString()
            => _value.ToString();
    }
}

#endif