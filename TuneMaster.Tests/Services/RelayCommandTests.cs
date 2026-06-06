using Forza_Horizon_6_Tune_Master.ViewModels;

namespace TuneMaster.Tests.Services;

public class RelayCommandTests
{
    [Fact]
    public void Execute_RunsAction()
    {
        var executed = false;
        var cmd = new RelayCommand(() => executed = true);

        cmd.Execute(null);

        Assert.True(executed);
    }

    [Fact]
    public void Execute_RunsActionWithParameter()
    {
        object? received = null;
        var cmd = new RelayCommand(p => received = p);

        cmd.Execute("test");

        Assert.Equal("test", received);
    }

    [Fact]
    public void CanExecute_Default_ReturnsTrue()
    {
        var cmd = new RelayCommand(() => { });
        Assert.True(cmd.CanExecute(null));
    }

    [Fact]
    public void CanExecute_UsesPredicate()
    {
        var canExec = false;
        var cmd = new RelayCommand(() => { }, () => canExec);

        Assert.False(cmd.CanExecute(null));

        canExec = true;
        Assert.True(cmd.CanExecute(null));
    }

    [Fact]
    public void CanExecute_UsesPredicateWithParameter()
    {
        var cmd = new RelayCommand(p => { }, p => p is int i && i > 5);

        Assert.False(cmd.CanExecute(3));
        Assert.True(cmd.CanExecute(10));
    }

    [Fact]
    public void Raise_FiresCanExecuteChanged()
    {
        var cmd = new RelayCommand(() => { });
        var raised = false;
        cmd.CanExecuteChanged += (_, _) => raised = true;

        cmd.Raise();

        Assert.True(raised);
    }

    [Fact]
    public void MultipleConstructors_Work()
    {
        var executed = false;
        var cmd = new RelayCommand(() => executed = true, () => true);

        cmd.Execute(null);

        Assert.True(executed);
    }
}
