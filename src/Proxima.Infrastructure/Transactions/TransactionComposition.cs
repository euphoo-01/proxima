using Microsoft.Extensions.DependencyInjection;
using Proxima.Core.Application.Transactions;
using Proxima.Infrastructure.Persistence.Repositories;

namespace Proxima.Infrastructure.Transactions;

public static class TransactionComposition
{
    public static IServiceCollection AddTransactionModule(this IServiceCollection services)
    {
        services.AddSingleton<ITransactionRepository, PostgresTransactionRepository>();
        services.AddSingleton<ITransactionService, TransactionService>();
        services.AddSingleton<IImportCommitService, PostgresImportCommitService>();
        return services;
    }
}
