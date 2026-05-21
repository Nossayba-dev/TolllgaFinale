using SQLite;
using TolllgaFinale.Models;

namespace TolllgaFinale.Services;

public class DatabaseService
{
    private SQLiteAsyncConnection? _db;

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
    }

    // ══ TRUCKS ════════════════════════════════════════════════════════════════

    public async Task<Truck?> GetTruckByMatriculeAsync(string matricule)
    {
        await InitAsync();
        return await _db!.Table<Truck>()
            .Where(t => t.Matricule.ToLower() == matricule.ToLower())
            .FirstOrDefaultAsync();
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

    /// <summary>
    /// Returns the most recent incomplete record for this truck:
    /// has GrossWeight but no Tare, or has Tare but no GrossWeight.
    /// </summary>
    public async Task<WeightRecord?> GetPendingRecordAsync(string matricule)
    {
        await InitAsync();
        var records = await _db!.Table<WeightRecord>()
            .Where(w => w.Matricule.ToLower() == matricule.ToLower())
            .OrderByDescending(w => w.WeighingDate)
            .ToListAsync();

        return records.FirstOrDefault(w =>
            (w.GrossWeight > 0 && w.Tare == 0) ||
            (w.Tare > 0 && w.GrossWeight == 0));
    }

    /// <summary>
    /// Fills the missing weight on an existing partial record and recalculates net.
    /// Also syncs the truck's tare if it changed.
    /// </summary>
    public async Task CompleteWeightRecordAsync(
        int id, double grossWeight, double tare, string? driverName)
    {
        await InitAsync();
        var record = await _db!.FindAsync<WeightRecord>(id);
        if (record is null) return;

        record.GrossWeight = grossWeight;
        record.Tare = tare;
        record.NetWeight = grossWeight - tare;
        if (driverName is not null) record.DriverName = driverName;
        await _db!.UpdateAsync(record);

        var truck = await GetTruckByMatriculeAsync(record.Matricule);
        if (truck is not null && tare > 0 && Math.Abs(truck.Tare - tare) > 0.001)
            await UpdateTruckTareAsync(truck.Id, tare);
    }

    /// <summary>
    /// Saves a new record (can be partial: only brut=0 or tare=0).
    /// Creates/updates the truck entry automatically.
    /// </summary>
    public async Task<WeightRecord> SaveWeightRecordAsync(WeightRecord record)
    {
        await InitAsync();
        record.NetWeight = record.GrossWeight - record.Tare;
        record.WeighingDate = DateTime.UtcNow;
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
        return await _db!.Table<WeightRecord>()
            .OrderByDescending(w => w.WeighingDate).ToListAsync();
    }

    public async Task<List<WeightRecord>> GetWeightsByMatriculeAsync(string matricule)
    {
        await InitAsync();
        return await _db!.Table<WeightRecord>()
            .Where(w => w.Matricule.ToLower() == matricule.ToLower())
            .OrderByDescending(w => w.WeighingDate).ToListAsync();
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