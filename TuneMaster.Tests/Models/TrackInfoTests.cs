using Forza_Horizon_6_Tune_Master.Models;

namespace TuneMaster.Tests.Models;

public class TrackInfoTests
{
    [Fact]
    public void DefaultDiscipline_IsRoad()
    {
        var track = new TrackInfo();
        Assert.Equal(Discipline.Road, track.Discipline);
    }

    [Fact]
    public void DefaultDragDistance_IsQuarter()
    {
        var track = new TrackInfo();
        Assert.Equal(DragDistance.Quarter, track.DragDistance);
    }

    [Fact]
    public void IsDrag_TrueForDrag()
    {
        var track = new TrackInfo { Discipline = Discipline.Drag };
        Assert.True(track.IsDrag);
    }

    [Fact]
    public void IsDrag_FalseForNonDrag()
    {
        var track = new TrackInfo { Discipline = Discipline.Road };
        Assert.False(track.IsDrag);
    }

    [Theory]
    [InlineData(Discipline.Road, false)]
    [InlineData(Discipline.Rally, false)]
    [InlineData(Discipline.Drift, false)]
    [InlineData(Discipline.Drag, true)]
    public void IsDrag_MatchesDiscipline(Discipline d, bool expected)
    {
        var track = new TrackInfo { Discipline = d };
        Assert.Equal(expected, track.IsDrag);
    }

    [Theory]
    [InlineData(Discipline.Road, true)]
    [InlineData(Discipline.Drag, false)]
    public void IsNotDrag_MatchesDiscipline(Discipline d, bool expected)
    {
        var track = new TrackInfo { Discipline = d };
        Assert.Equal(expected, track.IsNotDrag);
    }

    [Fact]
    public void PropertyChanged_RaisedOnDisciplineChange()
    {
        var track = new TrackInfo();
        var changed = new List<string>();
        track.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        track.Discipline = Discipline.Rally;

        Assert.Contains(nameof(TrackInfo.Discipline), changed);
    }

    [Fact]
    public void PropertyChanged_RaisedOnDragRelatedProperties()
    {
        var track = new TrackInfo();
        var changed = new List<string>();
        track.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        track.Discipline = Discipline.Drag;

        Assert.Contains(nameof(TrackInfo.IsDrag), changed);
        Assert.Contains(nameof(TrackInfo.IsNotDrag), changed);
    }

    [Fact]
    public void PropertyChanged_RaisedOnDragDistanceChange()
    {
        var track = new TrackInfo();
        var changed = new List<string>();
        track.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        track.DragDistance = DragDistance.Half;

        Assert.Contains(nameof(TrackInfo.DragDistance), changed);
    }
}
