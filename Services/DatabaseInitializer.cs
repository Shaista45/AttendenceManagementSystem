// Services/DatabaseInitializer.cs
using Microsoft.EntityFrameworkCore;
using AttendenceManagementSystem.Data;
using Microsoft.Data.SqlClient;

namespace AttendenceManagementSystem.Services
{
    public interface IDatabaseInitializer
    {
        Task InitializeAsync();
        Task MigrateAsync();
    }

    public class DatabaseInitializer : IDatabaseInitializer
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DatabaseInitializer> _logger;

        public DatabaseInitializer(ApplicationDbContext context, ILogger<DatabaseInitializer> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            try
            {
                _logger.LogInformation("Starting database initialization...");

                // Try to create database if it doesn't exist
                await EnsureDatabaseCreatedAsync();

                // Apply migrations
                await MigrateAsync();

                _logger.LogInformation("Database initialization completed successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while initializing the database.");
                throw;
            }
        }

        private async Task EnsureDatabaseCreatedAsync()
        {
            try
            {
                _logger.LogInformation("Checking if database exists...");

                // Simple query to test connection and database existence
                var canConnect = await _context.Database.CanConnectAsync();

                if (!canConnect)
                {
                    _logger.LogInformation("Database doesn't exist or cannot connect. Creating database...");
                    await _context.Database.EnsureCreatedAsync();
                    _logger.LogInformation("Database created successfully.");
                }
                else
                {
                    _logger.LogInformation("Database already exists and can be connected to.");
                }
            }
            catch (SqlException sqlEx) when (sqlEx.Number == 4060) // Database doesn't exist
            {
                _logger.LogInformation("Database doesn't exist. Creating database...");
                await _context.Database.EnsureCreatedAsync();
                _logger.LogInformation("Database created successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ensuring database is created.");
                throw;
            }
        }

        public async Task MigrateAsync()
        {
            try
            {
                var pendingMigrations = await _context.Database.GetPendingMigrationsAsync();
                var pendingMigrationsList = pendingMigrations.ToList();

                if (pendingMigrationsList.Count > 0)
                {
                    _logger.LogInformation($"Applying {pendingMigrationsList.Count} pending migrations...");
                    await _context.Database.MigrateAsync();
                    _logger.LogInformation("Migrations applied successfully.");
                }
                else
                {
                    _logger.LogInformation("No pending migrations.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while applying migrations.");
                throw;
            }
        }
    }
}