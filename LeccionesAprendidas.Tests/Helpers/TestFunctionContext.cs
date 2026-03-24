using Microsoft.Azure.Functions.Worker;

namespace LeccionesAprendidas.Tests.Helpers;

/// <summary>
/// Minimal concrete FunctionContext for unit tests. Only InstanceServices is functional.
/// </summary>
internal class TestFunctionContext : FunctionContext
{
    private readonly IServiceProvider _services;
    private IDictionary<object, object> _items = new Dictionary<object, object>();

    public TestFunctionContext(IServiceProvider services)
    {
        _services = services;
    }

    public override IServiceProvider InstanceServices
    {
        get => _services;
        set { }
    }

    public override IDictionary<object, object> Items
    {
        get => _items;
        set => _items = value;
    }

    public override string InvocationId => throw new NotImplementedException();
    public override string FunctionId => throw new NotImplementedException();
    public override TraceContext TraceContext => throw new NotImplementedException();
    public override BindingContext BindingContext => throw new NotImplementedException();
    public override RetryContext RetryContext => throw new NotImplementedException();
    public override FunctionDefinition FunctionDefinition => throw new NotImplementedException();
    public override IInvocationFeatures Features => throw new NotImplementedException();
    public override CancellationToken CancellationToken => default;
}
