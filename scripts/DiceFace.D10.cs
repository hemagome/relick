using Godot;

namespace Relick;

public partial class DiceFace
{
    private const int D10Sides = 10;
    private sealed record D10Face(Vector3[] Vertices, Vector3 Normal, Vector3 Up);
    private static readonly Vector3 D10Pole = new(0, 1.02f, 0);
    private static readonly Vector3[] D10Ring = BuildD10Ring();
    private static readonly D10Face[] D10Faces = BuildD10Faces();
    private Node3D _d10 = null!;
    private readonly List<Label3D> _d10Labels = [];

    private static Vector3[] BuildD10Ring()
    {
        var angle = Mathf.Tau / D10Sides;
        var cosine = Mathf.Cos(angle);
        // The alternating heights make the four vertices of every kite coplanar.
        var height = D10Pole.Y * (1 - cosine) / (1 + cosine);
        return Enumerable.Range(0, D10Sides).Select(i =>
            new Vector3(0.82f * Mathf.Cos(i * angle), i % 2 == 0 ? -height : height,
                0.82f * Mathf.Sin(i * angle))).ToArray();
    }

    private static D10Face[] BuildD10Faces()
    {
        var faces = new D10Face[D10Sides];
        for (var i = 0; i < D10Sides; i++)
        {
            var upper = i % 2 == 0;
            var pole = upper ? D10Pole : -D10Pole;
            Vector3[] vertices = [pole, D10Ring[(i + 1) % D10Sides], D10Ring[i], D10Ring[(i + D10Sides - 1) % D10Sides]];
            var normal = (vertices[2] - pole).Cross(vertices[1] - pole).Normalized();
            if (normal.Dot(pole) < 0) normal = -normal;
            var value = upper ? i / 2 + 1 : D10Sides - ((i + 5) % D10Sides) / 2;
            faces[value - 1] = new(vertices, normal, (pole - vertices[2]).Normalized());
        }
        return faces;
    }

    private void BuildD10()
    {
        _d10 = new Node3D { Name = "D10", Visible = false };
        _dieBody.AddChild(_d10);
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        for (var i = 0; i < D10Faces.Length; i++)
        {
            var face = D10Faces[i];
            foreach (var vertex in new[] { 0, 1, 2, 0, 2, 3 })
            {
                surface.SetNormal(face.Normal);
                surface.AddVertex(face.Vertices[vertex]);
            }
            var center = (face.Vertices[0] + face.Vertices[1] + face.Vertices[2] + face.Vertices[3]) / 4;
            var label = new Label3D
            {
                Text = (i + 1).ToString(),
                FontSize = 72,
                PixelSize = 0.006f,
                OutlineSize = 0,
                DoubleSided = false,
                Transform = new Transform3D(FaceBasis(face.Up, face.Normal), center + face.Normal * 0.008f)
            };
            _d10.AddChild(label);
            _d10Labels.Add(label);
        }
        _d10.AddChild(new MeshInstance3D { Mesh = surface.Commit(), MaterialOverride = _bodyMaterial });
    }

    private static Basis FaceBasis(Vector3 up, Vector3 normal) => new(up.Cross(normal).Normalized(), up, normal);

    private static Quaternion D10LandingRotation(int value)
    {
        var face = D10Faces[value - 1];
        // The top of the numeral points away from the camera once the result has landed.
        var awayFromCamera = new Vector3(-2.6f, 0, -3.6f).Normalized();
        return (FaceBasis(awayFromCamera, Vector3.Up) * FaceBasis(face.Up, face.Normal).Transposed()).GetRotationQuaternion();
    }

    private static float D10SupportHeight(Quaternion rotation)
    {
        var poleHeight = Math.Abs((rotation * D10Pole).Y);
        return Math.Max(poleHeight, D10Ring.Max(point => -(rotation * point).Y));
    }
}
