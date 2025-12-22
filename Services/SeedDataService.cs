using AttendenceManagementSystem.Data;

namespace AttendenceManagementSystem.Services
{
    public class SeedDataService : ISeedData
    {
        private readonly IServiceProvider _serviceProvider;

        public SeedDataService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task InitializeAsync()
        {
            await SeedData.Initialize(_serviceProvider);
        }
    }
}
