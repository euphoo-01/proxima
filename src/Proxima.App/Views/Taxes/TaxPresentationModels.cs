using Proxima.Core.Application.Taxes;

namespace Proxima.App.Views.Taxes;

public sealed record TaxBreakdownRowViewModel(string Name, string ValueText, string Note, TaxBreakdownKind Kind);
