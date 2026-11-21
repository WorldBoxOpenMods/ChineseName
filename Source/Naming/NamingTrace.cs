using System.Collections.Generic;

namespace Chinese_Name.Naming;

internal sealed class NamingTrace
{
    private readonly List<NameFailure> _failures = new();
    private readonly HashSet<(string Rule, string Arrangement, NameFailureReason Reason, string Reference)> _seen = new();
    private bool _nativeFailed;

    internal void Add(string rule, string arrangement, NameFailureReason reason, string reference = null, bool native = false)
    {
        _nativeFailed |= native;
        if (_seen.Add((rule, arrangement, reason, reference)))
            _failures.Add(new NameFailure(rule, arrangement, reason, reference));
    }

    internal NameResult Finish(NameResult result)
    {
        if (_failures.Count != 0) result.SetFailures(_failures, _nativeFailed);
        return result;
    }
}
