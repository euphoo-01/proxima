using Proxima.Application.Taxes;

namespace Proxima.Infrastructure.Taxes;

public static class ProximaTaxComposition
{
    public static ITaxCalculator CreateTaxCalculator()
    {
        return new DraftTaxCalculator(new MockNbrbExchangeRateProvider());
    }
}
