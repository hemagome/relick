using Godot;

namespace Relick;

/// <summary>A real 3D die rendered into a transparent viewport inside the 2D board.</summary>
public partial class DiceFace : Control
{
    public const double RollSeconds = 1.45;
    public const double ResultSeconds = 0.9;
    private const float FloorHeight = -HalfSize;
    private const float HalfSize = 0.68f;
    private static readonly Vector3[] FaceNormals =
        { Vector3.Zero, Vector3.Back, Vector3.Up, Vector3.Right, Vector3.Left, Vector3.Down, Vector3.Forward };
    private readonly Queue<(int Value, string Caption, bool Adjusted)> _queuedRolls = new();
    internal int LatestResult => _queuedRolls.Count > 0 ? _queuedRolls.Last().Value : Value;
    public string RollCaption { get; private set; } = "";
    public bool HasPendingRolls => _queuedRolls.Count > 0;
    public bool IsBusy => IsRolling || HasPendingRolls;
    private int _value;
    private double _elapsed;
    private double _glow;
    private double _resultRemaining;
    private int _launchCount;
    private SubViewport _viewport = null!;
    private Node3D _cube = null!;
    private MeshInstance3D _shadow = null!;
    private StandardMaterial3D _shadowMaterial = null!;
    private Quaternion _start = Quaternion.Identity;
    private Quaternion _landing = Quaternion.Identity;
    private int _renderedValue = -1;
    private bool _renderedRolling;

    public event Action? RollFinished;
    public bool IsRolling { get; private set; }
    public bool IsPresented => IsRolling || _resultRemaining > 0;
    public float PresentationOpacity => IsRolling ? 1 : (float)Math.Clamp(_resultRemaining / 0.2, 0, 1);
    internal float HeightAboveTable { get; private set; }
    internal float LowestPointHeight => _cube.Position.Y - SupportHeight(_cube.Quaternion);
    public bool Is3DReady => _viewport != null && _cube != null;
    public int Value
    {
        get => _value;
        set
        {
            _queuedRolls.Clear();
            RollCaption = "";
            _value = Math.Clamp(value, 0, 6);
            IsRolling = false;
            _elapsed = 0;
            _glow = 0;
            _resultRemaining = 0;
            _landing = LandingRotation(Math.Max(1, _value));
            UpdatePose();
        }
    }
    public int TopFaceValue
    {
        get
        {
            var rotation = Is3DReady ? _cube.Quaternion : _landing;
            return Enumerable.Range(1, 6).MaxBy(face => (rotation * FaceNormals[face]).Dot(Vector3.Up));
        }
    }
    internal void ShowAdjustedResult(int value, string caption)
    {
        if (IsBusy) { _queuedRolls.Enqueue((value, caption, true)); return; }
        ApplyAdjustedResult(value, caption);
    }
    private void ApplyAdjustedResult(int value, string caption)
    {
        _value = Math.Clamp(value, 0, 6);
        IsRolling = false; _elapsed = 0; _glow = 0;
        _landing = LandingRotation(Math.Max(1, _value));
        RollCaption = caption;
        _resultRemaining = ResultSeconds;
        UpdatePose();
    }
    public int DisplayedValue => _value == 0 ? 0 : TopFaceValue;
    internal float ResultUpAlignment => _value == 0 ? 0 : (_cube.Quaternion * FaceNormals[_value]).Dot(Vector3.Up);

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        TextureFilter = TextureFilterEnum.Linear;
        BuildScene();
        UpdatePose();
    }

    private void BuildScene()
    {
        _viewport = new SubViewport
        {
            Name = "Dice3DViewport", Size = new Vector2I(320, 288), TransparentBg = true,
            OwnWorld3D = true, Msaa3D = Viewport.Msaa.Msaa4X,
            RenderTargetUpdateMode = SubViewport.UpdateMode.WhenVisible
        };
        AddChild(_viewport);
        var world = new Node3D { Name = "DiceWorld" };
        _viewport.AddChild(world);
        world.AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.ClearColor,
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color("bccad9"), AmbientLightEnergy = 0.65f
            }
        });
        var camera = new Camera3D
        {
            Position = new Vector3(2.6f, 7.8f, 3.6f), Current = true,
            Projection = Camera3D.ProjectionType.Orthogonal, Size = 4.6f, Near = 0.1f, Far = 20
        };
        world.AddChild(camera);
        camera.LookAt(new Vector3(0, 0.48f, 0));
        world.AddChild(new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-45, -35, 0), LightColor = new Color("fff2d9"), LightEnergy = 1.35f
        });
        world.AddChild(new DirectionalLight3D
        {
            RotationDegrees = new Vector3(25, 130, -15), LightColor = new Color("91bfcf"), LightEnergy = 0.45f
        });
        world.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 1.85f, BottomRadius = 1.85f, Height = 0.12f, RadialSegments = 64 },
            Position = new Vector3(0, FloorHeight - 0.07f, 0),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("24443f"), Roughness = 0.95f }
        });
        _shadowMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0, 0, 0, 0.3f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
        };
        _shadow = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.8f, Height = 0.04f, RadialSegments = 32, Rings = 8 },
            Position = new Vector3(0, FloorHeight + 0.015f, 0), Scale = new Vector3(1, 1, 0.8f),
            MaterialOverride = _shadowMaterial
        };
        world.AddChild(_shadow);
        _cube = new Node3D { Name = "Die" };
        world.AddChild(_cube);
        _cube.AddChild(new MeshInstance3D
        {
            Mesh = RoundedCube(),
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color("eee4d1"), Metallic = 0.05f, Roughness = 0.28f,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled
            }
        });
        var pipMesh = new SphereMesh { Radius = 0.087f, Height = 0.174f, RadialSegments = 16, Rings = 8 };
        var pipMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color("172438"), Roughness = 0.8f,
            SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled
        };
        for (var face = 1; face <= 6; face++)
        {
            var normal = FaceNormals[face];
            var (u, v) = FaceAxes(normal);
            var points = new List<Vector2>();
            if (face % 2 == 1) points.Add(Vector2.Zero);
            if (face >= 2) { points.Add(new(-0.27f, -0.27f)); points.Add(new(0.27f, 0.27f)); }
            if (face >= 4) { points.Add(new(-0.27f, 0.27f)); points.Add(new(0.27f, -0.27f)); }
            if (face == 6) { points.Add(new(-0.27f, 0)); points.Add(new(0.27f, 0)); }
            foreach (var point in points)
                _cube.AddChild(new MeshInstance3D
                {
                    Mesh = pipMesh, MaterialOverride = pipMaterial,
                    // Mostly embedded spheres form small curved ink inlays on each physical face.
                    Position = normal * (HalfSize - 0.04f) + u * point.X + v * point.Y
                });
        }
    }

    private static (Vector3 U, Vector3 V) FaceAxes(Vector3 normal)
    {
        var u = Math.Abs(normal.Y) > 0.5f ? Vector3.Right : normal.Cross(Vector3.Up).Normalized();
        return (u, normal.Cross(u));
    }

    private static ArrayMesh RoundedCube()
    {
        const int steps = 16;
        const float bevel = 0.12f;
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        foreach (var normal in FaceNormals.Skip(1))
        {
            var (u, v) = FaceAxes(normal);
            void Vertex(int x, int y)
            {
                var p = normal * HalfSize + u * ((float)x / steps * 2 - 1) * HalfSize + v * ((float)y / steps * 2 - 1) * HalfSize;
                var core = p.Clamp(Vector3.One * (-HalfSize + bevel), Vector3.One * (HalfSize - bevel));
                var outward = (p - core).Normalized();
                surface.SetNormal(outward);
                surface.AddVertex(core + outward * bevel);
            }
            for (var y = 0; y < steps; y++)
                for (var x = 0; x < steps; x++)
                {
                    Vertex(x, y); Vertex(x + 1, y + 1); Vertex(x + 1, y);
                    Vertex(x, y); Vertex(x, y + 1); Vertex(x + 1, y + 1);
                }
        }
        var mesh = surface.Commit();
        surface.Dispose();
        return mesh;
    }

    // First orient the chosen physical face forwards, then rotate it onto the tabletop's up axis.
    private static Quaternion LandingRotation(int face) => new Quaternion(Vector3.Right, -Mathf.Pi / 2) * (face switch
    {
        2 => new Quaternion(Vector3.Right, Mathf.Pi / 2),
        3 => new Quaternion(Vector3.Up, -Mathf.Pi / 2),
        4 => new Quaternion(Vector3.Up, Mathf.Pi / 2),
        5 => new Quaternion(Vector3.Right, -Mathf.Pi / 2),
        6 => new Quaternion(Vector3.Up, Mathf.Pi),
        _ => Quaternion.Identity
    });

    public void QueueRoll(int result, string caption = "")
    {
        if (result is < 1 or > 6) throw new ArgumentOutOfRangeException(nameof(result));
        if (IsBusy) _queuedRolls.Enqueue((result, caption, false));
        else StartRoll(result, caption);
    }

    public void RollTo(int result)
    {
        _queuedRolls.Clear();
        StartRoll(result);
    }

    private void StartRoll(int result, string caption = "")
    {
        if (result < 1 || result > 6) throw new ArgumentOutOfRangeException(nameof(result));
        RollCaption = caption;
        _launchCount++;
        _start = Quaternion.FromEuler(new Vector3(0.7f, _launchCount * 0.83f, -0.4f));
        _landing = LandingRotation(result);
        _value = result;
        _elapsed = 0;
        _glow = 0;
        _resultRemaining = 0;
        IsRolling = true;
        UpdatePose();
    }

    public override void _Process(double delta) => AdvancePlayback(delta);

    internal void AdvancePlayback(double delta)
    {
        if (!IsRolling && _resultRemaining <= 0 && _queuedRolls.Count > 0)
        {
            var next = _queuedRolls.Dequeue();
            if (next.Adjusted) { ApplyAdjustedResult(next.Value, next.Caption); RollFinished?.Invoke(); }
            else StartRoll(next.Value, next.Caption);
        }
        if (!IsRolling && _resultRemaining <= 0 && _glow <= 0) return;
        delta = Math.Max(0, delta);
        var finished = false;
        if (IsRolling)
        {
            _elapsed += delta;
            if (_elapsed + 1e-9 >= RollSeconds)
            {
                IsRolling = false; _glow = 0.35; finished = true;
                _resultRemaining = Math.Max(0, ResultSeconds - Math.Max(0, _elapsed - RollSeconds));
            }
        }
        else
        {
            _glow = Math.Max(0, _glow - delta);
            _resultRemaining = Math.Max(0, _resultRemaining - delta);
        }
        UpdatePose();
        // Apply the landing pose before observers inspect the physical face or rebuild the UI.
        if (finished) RollFinished?.Invoke();
    }

    private void UpdatePose()
    {
        if (Is3DReady)
        {
            if (IsRolling || _renderedRolling || _renderedValue != _value)
                _viewport.RenderTargetUpdateMode = IsRolling ? SubViewport.UpdateMode.WhenVisible : SubViewport.UpdateMode.Once;
            _renderedValue = _value; _renderedRolling = IsRolling;
            var seconds = (float)Math.Clamp(_elapsed, 0, RollSeconds);
            var t = seconds / (float)RollSeconds;
            var spin = Math.Clamp(seconds / 1.18f, 0, 1);
            var blend = spin * spin * (3 - 2 * spin);
            var axis = new Vector3(1, 0.6f + _launchCount % 3 * 0.2f, 0.4f).Normalized();
            _cube.Quaternion = IsRolling
                ? (_start.Slerp(_landing, blend) * new Quaternion(axis, Mathf.Tau * 3 * (1 - (1 - spin) * (1 - spin)))).Normalized()
                : _landing;
            if (IsRolling && seconds > 1.18f)
            {
                var settle = (seconds - 1.18f) / ((float)RollSeconds - 1.18f);
                _cube.Quaternion = (new Quaternion(Vector3.Right, Mathf.Sin(settle * Mathf.Tau * 2) * 0.09f * (1 - settle) * (1 - settle)) * _landing).Normalized();
            }
            HeightAboveTable = IsRolling ? BounceHeight(seconds) : 0;
            var x = IsRolling ? -0.85f * (1 - t) * (1 - t) : 0;
            var z = IsRolling ? 0.3f * (1 - t) : 0;
            // Support of the rounded cube keeps corners above the tabletop while tumbling.
            _cube.Position = new Vector3(x, FloorHeight + SupportHeight(_cube.Quaternion) + HeightAboveTable, z);
            _shadow.Position = new Vector3(x, FloorHeight + 0.015f, z);
            _shadow.Scale = new Vector3(1 + HeightAboveTable * 0.2f, 1, 0.8f + HeightAboveTable * 0.16f);
            _shadowMaterial.AlbedoColor = new Color(0, 0, 0, 0.3f / (1 + HeightAboveTable * 1.5f));
        }
        QueueRedraw();
    }

    private static float SupportHeight(Quaternion rotation)
    {
        var basis = new Basis(rotation);
        return (HalfSize - 0.12f) * (Math.Abs(basis.X.Y) + Math.Abs(basis.Y.Y) + Math.Abs(basis.Z.Y)) + 0.12f;
    }

    private static float BounceHeight(float seconds)
    {
        const float gravity = 25;
        if (seconds < 0.34f) return gravity * 0.5f * (0.34f * 0.34f - seconds * seconds);
        foreach (var (start, end) in new[] { (0.34f, 0.76f), (0.76f, 1.02f), (1.02f, 1.18f) })
            if (seconds < end) return Math.Max(0, gravity * 0.5f * (seconds - start) * (end - seconds));
        return 0;
    }

    internal Image Capture3DFrame() => _viewport.GetTexture().GetImage();

    public override void _Draw()
    {
        if (!Is3DReady) return;
        var extent = (Vector2)_viewport.Size * Math.Min(Size.X / _viewport.Size.X, Size.Y / _viewport.Size.Y);
        DrawTextureRect(_viewport.GetTexture(), new Rect2((Size - extent) / 2, extent), false);
        if (_glow > 0)
            DrawArc(Size / 2, Math.Min(Size.X, Size.Y) * 0.43f, 0, Mathf.Tau, 64,
                new Color(0.46f, 0.9f, 0.74f, (float)(_glow / 0.35 * 0.5)), 2, true);
    }
}
