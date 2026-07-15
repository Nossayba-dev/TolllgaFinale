using SQLite;
using TolllgaFinale.Models;

namespace TolllgaFinale.Services;

public class DatabaseService
{
    private const string WeightRecordTable = "WeightRecord";

    private SQLiteAsyncConnection? _db;
    private readonly AuthService _auth;

    public DatabaseService(AuthService auth)
    {
        _auth = auth;
    }

    public string DbPath =>
        Path.Combine(FileSystem.AppDataDirectory, "weightsync.db3");

    // ── Init ──────────────────────────────────────────────────────────────────
    public async Task InitAsync()
    {
        if (_db is not null) return;

        _db = new SQLiteAsyncConnection(DbPath,
            SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);
        await _db.CreateTableAsync<Truck>();
        await _db.CreateTableAsync<WeightRecord>();
        await MigrateWeightRecordSchemaAsync();
    }

    private async Task MigrateWeightRecordSchemaAsync()
    {
        if (_db is null) return;

        var columns = await GetTableColumnsAsync(WeightRecordTable);
        var hasLegacyDate = columns.Contains("WeighingDate");

        await AddColumnIfMissingAsync(WeightRecordTable, "Product", "TEXT");
        await AddColumnIfMissingAsync(WeightRecordTable, "Observation", "TEXT");
        await AddColumnIfMissingAsync(WeightRecordTable, "Amount", "REAL");
        await AddColumnIfMissingAsync(WeightRecordTable, "WeighingDateTare", "TEXT");
        await AddColumnIfMissingAsync(WeightRecordTable, "WeighingDateGross", "TEXT");
        await AddColumnIfMissingAsync(WeightRecordTable, "OperatorTare", "TEXT");
        await AddColumnIfMissingAsync(WeightRecordTable, "OperatorGross", "TEXT");

        if (hasLegacyDate)
        {
            await _db.ExecuteAsync($@"
UPDATE {WeightRecordTable}
SET WeighingDateTare = CASE
        WHEN WeighingDateTare IS NULL OR WeighingDateTare = '0001-01-01 00:00:00' THEN WeighingDate
        ELSE WeighingDateTare
    END,
    WeighingDateGross = CASE
        WHEN WeighingDateGross IS NULL OR WeighingDateGross = '0001-01-01 00:00:00' THEN WeighingDate
        ELSE WeighingDateGross
    END
WHERE WeighingDate IS NOT NULL;");
        }
    }

    private async Task<HashSet<string>> GetTableColumnsAsync(string tableName)
    {
        var columns = await _db!.QueryAsync<TableInfo>($"PRAGMA table_info({tableName});");
        return columns
            .Select(c => c.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private async Task AddColumnIfMissingAsync(string tableName, string columnName, string type)
    {
        var columns = await GetTableColumnsAsync(tableName);
        if (columns.Contains(columnName)) return;
        await _db!.ExecuteAsync($"ALTER TABLE {tableName} ADD COLUMN {columnName} {type};");
    }

    private sealed class TableInfo
    {
        public string Name { get; set; } = "";
    }

    private string GetCurrentOperatorName() => _auth.CurrentUserLabel;

    private static void StampNewWeighingFields(WeightRecord record, DateTime now, string operatorName)
    {
        if (record.GrossWeight > 0)
        {
            record.WeighingDateGross = now;
            record.OperatorGross = operatorName;
        }

        if (record.Tare > 0)
        {
            record.WeighingDateTare = now;
            record.OperatorTare = operatorName;
        }
    }

    private static void StampMissingWeighingFields(
        WeightRecord record,
        double grossWeight,
        double tare,
        DateTime now,
        string operatorName)
    {
        if (grossWeight > 0 && (record.GrossWeight <= 0 || record.WeighingDateGross == default))
        {
            record.WeighingDateGross = now;
            record.OperatorGross = operatorName;
        }

        if (tare > 0 && (record.Tare <= 0 || record.WeighingDateTare == default))
        {
            record.WeighingDateTare = now;
            record.OperatorTare = operatorName;
        }
    }

    private static void StampWeightFields(WeightRecord record, DateTime now, string operatorName)
    {
        StampNewWeighingFields(record, now, operatorName);
        record.NetWeight = record.GrossWeight - record.Tare;
    }

    // ══ TRUCKS ════════════════════════════════════════════════════════════════

    public async Task<Truck?> GetTruckByMatriculeAsync(string matricule)
    {
        await InitAsync();
        var norm = matricule.Trim().ToLowerInvariant();
        var trucks = await _db!.Table<Truck>().ToListAsync();
        return trucks.FirstOrDefault(t => t.Matricule.ToLowerInvariant() == norm);
    }

    public async Task<List<Truck>> GetAllTrucksAsync()
    {
        await InitAsync();
        return await _db!.Table<Truck>().OrderBy(t => t.Matricule).ToListAsync();
    }

    public async Task<Truck> InsertTruckAsync(Truck truck)
    {
        await InitAsync();
        truck.CreatedAt = DateTime.UtcNow;
        truck.UpdatedAt = DateTime.UtcNow;
        await _db!.InsertAsync(truck);
        return truck;
    }

    public async Task UpdateTruckAsync(int id, string driverName, double tare)
    {
        await InitAsync();
        var t = await _db!.FindAsync<Truck>(id);
        if (t is null) return;
        t.DriverName = string.IsNullOrWhiteSpace(driverName) ? null : driverName;
        t.Tare = tare;
        t.UpdatedAt = DateTime.UtcNow;
        await _db!.UpdateAsync(t);
    }

    public async Task UpdateTruckTareAsync(int id, double newTare)
    {
        await InitAsync();
        var t = await _db!.FindAsync<Truck>(id);
        if (t is null) return;
        t.Tare = newTare;
        t.UpdatedAt = DateTime.UtcNow;
        await _db!.UpdateAsync(t);
    }

    public async Task ToggleTruckActiveAsync(int id, bool isActive)
    {
        await InitAsync();
        var t = await _db!.FindAsync<Truck>(id);
        if (t is null) return;
        t.IsActive = isActive;
        t.UpdatedAt = DateTime.UtcNow;
        await _db!.UpdateAsync(t);
    }

    // ══ WEIGHTS ═══════════════════════════════════════════════════════════════

    public async Task<WeightRecord?> GetWeightRecordByIdAsync(int id)
    {
        await InitAsync();
        return await _db!.FindAsync<WeightRecord>(id);
    }

    /// <summary>
    /// Returns the most recent incomplete record for this truck:
    /// has GrossWeight but no Tare, or has Tare but no GrossWeight.
    /// </summary>
    public async Task<WeightRecord?> GetPendingRecordAsync(string matricule)
    {
        await InitAsync();
        var normM = matricule.Trim().ToLowerInvariant();
        var records = (await _db!.Table<WeightRecord>().ToListAsync())
            .Where(w => w.Matricule.ToLowerInvariant() == normM)
            .OrderByDescending(w => w.GetLatestWeighingDate())
            .ToList();

        return records.FirstOrDefault(w =>
            (w.GrossWeight > 0 && w.Tare == 0) ||
            (w.Tare > 0 && w.GrossWeight == 0));
    }

    /// <summary>
    /// Fills the missing weight on an existing partial record and recalculates net.
    /// Also syncs the truck's tare if it changed.
    /// </summary>
    public async Task<WeightRecord?> CompleteWeightRecordAsync(
        int id,
        double grossWeight,
        double tare,
        string? driverName,
        string? product,
        string? observation,
        double amount)
    {
        await InitAsync();
        var record = await _db!.FindAsync<WeightRecord>(id);
        if (record is null) return null;

        var now = DateTime.UtcNow;
        var operatorName = GetCurrentOperatorName();
        StampMissingWeighingFields(record, grossWeight, tare, now, operatorName);

        record.GrossWeight = grossWeight;
        record.Tare = tare;
        record.Amount = amount;
        record.NetWeight = grossWeight - tare;
        record.Product = product;
        record.Observation = observation;
        if (driverName is not null) record.DriverName = driverName;
        await _db!.UpdateAsync(record);

        var truck = await GetTruckByMatriculeAsync(record.Matricule);
        if (truck is not null && tare > 0 && Math.Abs(truck.Tare - tare) > 0.001)
            await UpdateTruckTareAsync(truck.Id, tare);

        return record;
    }

    public async Task UpdateWeightRecordAsync(WeightRecord record)
    {
        await InitAsync();

        var existing = await _db!.FindAsync<WeightRecord>(record.Id);
        if (existing is null) return;

        record.NetWeight = record.GrossWeight - record.Tare;
        await _db.UpdateAsync(record);
    }

    /// <summary>
    /// Saves a new record (can be partial: only brut=0 or tare=0).
    /// Creates/updates the truck entry automatically.
    /// </summary>
    public async Task<WeightRecord> SaveWeightRecordAsync(WeightRecord record)
    {
        await InitAsync();

        var now = DateTime.UtcNow;
        var operatorName = GetCurrentOperatorName();
        StampNewWeighingFields(record, now, operatorName);

        record.NetWeight = record.GrossWeight - record.Tare;
        await _db!.InsertAsync(record);

        var truck = await GetTruckByMatriculeAsync(record.Matricule);
        if (truck is null)
        {
            await InsertTruckAsync(new Truck
            {
                Matricule = record.Matricule,
                DriverName = record.DriverName,
                Tare = record.Tare,
                IsActive = true
            });
        }
        else if (record.Tare > 0 && Math.Abs(truck.Tare - record.Tare) > 0.001)
        {
            await UpdateTruckTareAsync(truck.Id, record.Tare);
        }

        return record;
    }

    public async Task<List<WeightRecord>> GetAllWeightsAsync()
    {
        await InitAsync();
        return (await _db!.Table<WeightRecord>().ToListAsync())
            .OrderByDescending(w => w.GetLatestWeighingDate())
            .ToList();
    }

    public async Task<List<WeightRecord>> GetWeightsByMatriculeAsync(string matricule)
    {
        await InitAsync();
        var normW = matricule.Trim().ToLowerInvariant();
        var all = await _db!.Table<WeightRecord>().ToListAsync();
        return all
            .Where(w => w.Matricule.ToLowerInvariant() == normW)
            .OrderByDescending(w => w.GetLatestWeighingDate())
            .ToList();
    }

    public async Task DeleteWeightRecordAsync(int id)
    {
        await InitAsync();
        await _db!.DeleteAsync<WeightRecord>(id);
    }

    public async Task ClearAllWeightsAsync()
    {
        await InitAsync();
        await _db!.DeleteAllAsync<WeightRecord>();
    }

    public async Task ResetDatabaseAsync()
    {
        await InitAsync();
        await _db!.DeleteAllAsync<WeightRecord>();
        await _db!.DeleteAllAsync<Truck>();
    }
}