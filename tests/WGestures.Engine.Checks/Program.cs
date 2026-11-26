using System.Collections;
using System.Drawing;
using WGestures.Core;
using WGestures.Core.Commands;
using WGestures.Core.Persistence;

int checks = 0;
void Check(bool condition, string description)
{
    if (!condition) throw new Exception("FAIL " + description);
    Console.WriteLine("PASS " + description);
    checks++;
}
var tracker = new FakeTracker();
var finder = new FakeFinder();
using var parser = new GestureParser(tracker, finder) { Enable8DirGesture = true };
var errors = new List<Exception>();
var executions = new List<GestureIntent.ExecutionResult>();
parser.CommandFailed += (_, ex) => errors.Add(ex);
parser.IntentExecuted += (_, result) => executions.Add(result);
Gesture captured = null;
parser.GestureCaptured += g => captured = g;
parser.IsInCaptureMode = true;
void Draw(int x, int y)
{
    tracker.Begin(new Point(x, y));
    tracker.Move(new Point(x + 20, y - 20));
    tracker.Move(new Point(x + 40, y - 20));
    tracker.End();
}
Draw(0,0);
var first = captured.Dirs.ToArray();
Check(first.SequenceEqual(new[] { Gesture.GestureDir.Up, Gesture.GestureDir.Right }), "legacy diagonal correction resolves to up then right");
Draw(100,120);
Check(captured.Dirs.SequenceEqual(first), "direction recognition is invariant under non-equal X/Y translation");
Draw(-850,380);
Check(captured.Dirs.SequenceEqual(first), "negative monitor coordinates preserve recognition");
parser.IsInCaptureMode = false;
Check((GestureModifier.All & GestureModifier.X1) != 0 && (GestureModifier.All & GestureModifier.X2) != 0, "modifier mask includes X1 and X2");
var ok = new TestCommand();
var fail = new TestCommand { Fail = true };
var normal = new GestureIntent { Name = "normal", Gesture = new Gesture(), Command = ok };
normal.Gesture.Add(Gesture.GestureDir.Right);
finder.Current = normal;
tracker.Begin(Point.Empty);
tracker.Move(new Point(40,0));
tracker.End();
Check(ok.Executions == 1 && executions[^1].IsOk, "recognized action executes once and reports success");
tracker.End();
Check(ok.Executions == 1, "duplicate path-end cannot execute a second time");
normal.Command = fail;
tracker.Begin(Point.Empty);
tracker.Move(new Point(40,0));
tracker.End();
Check(fail.Executions == 1 && !executions[^1].IsOk && executions[^1].Exception is InvalidOperationException && errors.Count == 1,
    "throwing action reports failure without crashing the parser");
var badContext = new FakeContext { FailActivation = true };
Check(!normal.Execute(badContext, parser).IsOk && fail.Executions == 1, "target activation failure does not execute command");
normal.Command = ok;
tracker.Begin(Point.Empty);
tracker.Move(new Point(40,0));
tracker.End();
Check(ok.Executions == 2 && executions[^1].IsOk, "subsequent gesture works after failed action");
finder.Store.HotCornerCommands[0] = fail;
finder.Store.HotCornerCommands[4] = fail;
int errorCount = errors.Count;
tracker.Corner(ScreenCorner.LeftBottom);
tracker.Edge(ScreenEdge.Left);
Check(errors.Count == errorCount + 2, "corner and edge failures are contained and reported");

var modifier = new ModifierCommand();
var modified = new GestureIntent
{
    Name = "modifier session", Gesture = new Gesture { Modifier = GestureModifier.WheelForward },
    Command = modifier, ExecuteOnModifier = true
};
finder.Current = modified;
tracker.Begin(Point.Empty);
tracker.Modifier(GestureModifier.WheelForward);
Check(modifier.Recognized == 1 && tracker.IsSuspended && (tracker.Filtered & GestureModifier.X1) == 0 &&
    (tracker.Filtered & GestureModifier.X2) != 0, "modifier session filters unobserved side buttons");
tracker.Modifier(GestureModifier.X1);
Check(modifier.Triggered == 1, "observed modifier reaches active session");
tracker.End();
Check(modifier.Ended == 1 && modifier.Subscribers == 0 && executions[^1].IsOk, "release cleans session exactly once");
tracker.Begin(Point.Empty);
tracker.Modifier(GestureModifier.WheelForward);
tracker.Timeout();
tracker.End();
Check(modifier.Ended == 2 && modifier.Subscribers == 0, "timeout cleans modifier session and suppresses duplicate end");
modifier.FailTrigger = true;
tracker.Begin(Point.Empty);
tracker.Modifier(GestureModifier.WheelForward);
tracker.Modifier(GestureModifier.X1);
int recognized = modifier.Recognized;
tracker.Modifier(GestureModifier.WheelForward);
tracker.End();
Check(modifier.Ended == 3 && modifier.Subscribers == 0 && modifier.Recognized == recognized, "failed modifier session is cleaned and not restarted on same path");
modifier.FailTrigger = false;
modifier.FailEnd = true;
tracker.Begin(Point.Empty);
tracker.Modifier(GestureModifier.WheelForward);
tracker.End();
Check(!executions[^1].IsOk && modifier.Subscribers == 0, "end callback failure reports failure and unsubscribes");
modifier.FailEnd = false;
tracker.Begin(Point.Empty);
tracker.Modifier(GestureModifier.WheelForward);
parser.Dispose();
Check(modifier.Ended == 5 && modifier.Subscribers == 0, "parser disposal releases an active modifier session");
errorCount = errors.Count;
tracker.Edge(ScreenEdge.Left);
Check(errors.Count == errorCount, "disposing parser unsubscribes edge events");
Console.WriteLine($"{checks} engine behavior checks passed (synthetic events, no native input injection).");

sealed class FakeContext : GestureContext
{
    public bool FailActivation { get; set; }
    public override void ActivateTargetWindow()
    {
        if (FailActivation) throw new InvalidOperationException("fake activation");
    }
}
sealed class FakeTracker : IPathTracker
{
    private FakeContext _context = new();
    public bool Paused { get; set; }
    public bool IsSuspended { get; private set; }
    public GestureModifier Filtered { get; private set; }
    public void Start() { }
    public void Stop() { }
    public void Dispose() { }
    public void SuspendTemprarily(GestureModifier filteredModifiers)
    { IsSuspended = true; Filtered = filteredModifiers; }
    public event BeforePathStartEventHandler BeforePathStart;
    public event PathTrackEventHandler PathStart, PathGrow, EffectivePathGrow, PathEnd, PathTimeout, PathModifier;
    public event Action<ScreenCorner> HotCornerTriggered;
    public event Action<ScreenEdge> EdgeRubbed;
    private PathEventArgs Args(GestureModifier modifier = GestureModifier.None) =>
        new(_context.EndPoint, _context) { Button = _context.GestureButton, Modifier = modifier };
    public void Begin(Point start)
    {
        IsSuspended = false;
        _context = new FakeContext { StartPoint = start, EndPoint = start, GestureButton = GestureTriggerButton.Right };
        var before = new BeforePathStartEventArgs(Args());
        BeforePathStart?.Invoke(before);
        if (before.ShouldPathStart) PathStart?.Invoke(Args());
    }
    public void Move(Point p) { _context.EndPoint = p; PathGrow?.Invoke(Args()); EffectivePathGrow?.Invoke(Args()); }
    public void End() { PathEnd?.Invoke(Args()); IsSuspended = false; }
    public void Timeout() { PathTimeout?.Invoke(Args()); IsSuspended = false; }
    public void Modifier(GestureModifier m) => PathModifier?.Invoke(Args(m));
    public void Corner(ScreenCorner c) => HotCornerTriggered?.Invoke(c);
    public void Edge(ScreenEdge e) => EdgeRubbed?.Invoke(e);
}
sealed class FakeFinder : IGestureIntentFinder
{
    public GestureIntent Current { get; set; }
    public FakeStore Store { get; } = new();
    public IGestureIntentStore IntentStore => Store;
    public bool IsGesturingEnabledForContext(GestureContext context, out ExeApp app) { app = null; return true; }
    public GestureIntent Find(Gesture g, GestureContext c) => Find(g, (ExeApp)null);
    public GestureIntent Find(Gesture g, ExeApp app) => Current?.Gesture.Equals(g) == true ? Current : null;
}
sealed class FakeStore : IGestureIntentStore
{
    public GlobalApp GlobalApp { get; } = new();
    public AbstractCommand[] HotCornerCommands { get; set; } = new AbstractCommand[8];
    public bool TryGetExeApp(string key, out ExeApp found) { found = null; return false; }
    public ExeApp GetExeApp(string key) => null;
    public void Remove(string app) { }
    public void Remove(ExeApp app) { }
    public void Add(ExeApp app) { }
    public void Save() { }
    public IEnumerator<ExeApp> GetEnumerator() => Enumerable.Empty<ExeApp>().GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
sealed class TestCommand : AbstractCommand
{
    public int Executions { get; private set; }
    public bool Fail { get; set; }
    public override void Execute() { Executions++; if (Fail) throw new InvalidOperationException("fake action failure"); }
}
sealed class ModifierCommand : AbstractCommand, IGestureModifiersAware
{
    public int Recognized, Triggered, Ended, Subscribers;
    public bool FailTrigger, FailEnd;
    public event Action<string> ReportStatus
    {
        add { Subscribers++; }
        remove { Subscribers--; }
    }
    public override void Execute() { }
    public void GestureRecognized(out GestureModifier observeModifiers)
    { Recognized++; observeModifiers = GestureModifier.X1; }
    public void ModifierTriggered(GestureModifier modifier)
    { Triggered++; if (FailTrigger) throw new InvalidOperationException("modifier callback"); }
    public void GestureEnded()
    { Ended++; if (FailEnd) throw new InvalidOperationException("end callback"); }
}
