using System;
using System.Collections.Generic;

namespace Chinese_Name.Naming;

internal sealed class NamingCandidate(
    Rule rule, int attempts, Func<string, List<string>, CandidateDraw> openLayout = null,
    string arrangementId = null, SurnameOrder? inheritance = null, string ruleId = null)
{
    internal Rule Rule { get; } = rule;
    internal string RuleId { get; } = ruleId ?? rule?.Id;
    internal string ArrangementId { get; } = arrangementId;
    internal SurnameOrder Inheritance { get; } = inheritance ?? rule?.Inheritance ?? SurnameOrder.New;
    internal int Attempts { get; } = attempts;
    internal Func<string, List<string>, CandidateDraw> OpenLayout { get; } = openLayout;
}

internal sealed class CandidateDraw : IDisposable
{
    private readonly Action _release;
    internal Func<int, (NameResult Name, (NameFailureReason Reason, string Reference)? Failure)> Attempt { get; }
    internal (NameFailureReason Reason, string Reference)? PreparationFailure { get; }

    internal CandidateDraw(Func<int, (NameResult, (NameFailureReason, string)?)> attempt, Action release = null)
    {
        Attempt = attempt;
        _release = release;
    }

    internal CandidateDraw(NameFailureReason reason, string reference = null) => PreparationFailure = (reason, reference);

    public void Dispose() => _release?.Invoke();
}
