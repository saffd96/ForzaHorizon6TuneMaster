namespace Forza_Horizon_6_Tune_Master.Models;

public class TrackInfo : NotifyBase
{
    private Discipline _discipline = Discipline.Road;
    public Discipline Discipline
    {
        get => _discipline;
        set { Set(ref _discipline, value); OnPropertyChanged(nameof(IsDrag)); OnPropertyChanged(nameof(IsNotDrag)); }
    }

    public bool IsDrag    => Discipline == Discipline.Drag;
    public bool IsNotDrag => Discipline != Discipline.Drag;

    private DragDistance _dragDistance = DragDistance.Quarter;
    public DragDistance DragDistance
    {
        get => _dragDistance;
        set { Set(ref _dragDistance, value); }
    }
}
