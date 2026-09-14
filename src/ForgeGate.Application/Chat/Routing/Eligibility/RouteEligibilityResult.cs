using ForgeGate.Domain.Providers;

namespace ForgeGate.Application.Chat.Routing.Eligibility;

public sealed record RouteEligibilityResult
{
    public bool IsEligible { get; }
    public IReadOnlyList<string> Reasons { get; }

    private RouteEligibilityResult(bool isEligible, IReadOnlyList<string> reasons)
    {
        IsEligible = isEligible;
        Reasons = reasons;
    }

    public static RouteEligibilityResult Eligible() => new(true, new List<string>());
    public static RouteEligibilityResult Ineligible(string reason) => new(false, new List<string> { reason });
    public static RouteEligibilityResult Ineligible(IReadOnlyList<string> reasons) => new(false, reasons);
}
