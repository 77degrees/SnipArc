using ScreenCaptureApp.Core.Annotations;
using ScreenCaptureApp.Core.Geometry;

namespace ScreenCaptureApp.Core.Tests.Annotations;

public sealed class AnnotationTests
{
    [Fact]
    public void FreehandPath_CopiesMutablePointInput()
    {
        var points = new List<PhysicalPoint> { new(1, 2), new(3, 4) };
        var annotation = new FreehandPathAnnotation(Guid.NewGuid(), points, RgbaColor.RedColor, 2);

        points[0] = new PhysicalPoint(99, 99);
        points.Add(new PhysicalPoint(5, 6));

        Assert.True(annotation.Points.SequenceEqual([new PhysicalPoint(1, 2), new PhysicalPoint(3, 4)]));
    }

    [Fact]
    public void OpaqueRedaction_RejectsTransparentColor()
    {
        Assert.Throws<ArgumentException>(() => new OpaqueRedactionAnnotation(
            Guid.NewGuid(),
            new PhysicalRect(0, 0, 20, 20),
            new RgbaColor(0, 0, 0, 254)));
    }

    [Fact]
    public void PixelateAndOpaqueRedaction_AreDistinctAnnotationTypes()
    {
        Annotation pixelate = new PixelateAnnotation(Guid.NewGuid(), new PhysicalRect(0, 0, 20, 20), 8);
        Annotation redaction = new OpaqueRedactionAnnotation(Guid.NewGuid(), new PhysicalRect(0, 0, 20, 20), RgbaColor.Black);

        Assert.IsType<PixelateAnnotation>(pixelate);
        Assert.IsType<OpaqueRedactionAnnotation>(redaction);
    }

    [Fact]
    public void AnnotationConstructors_RejectInvalidDrawingGeometry()
    {
        var id = Guid.NewGuid();
        var point = new PhysicalPoint(10, 10);

        Assert.Throws<ArgumentException>(() => new LineAnnotation(id, point, point, RgbaColor.Black, 1));
        Assert.Throws<ArgumentException>(() => new ArrowAnnotation(id, point, point, RgbaColor.Black, 1));
        Assert.Throws<ArgumentException>(() => new RectangleAnnotation(id, new PhysicalRect(0, 0, 0, 5), RgbaColor.Black, 1));
        Assert.Throws<ArgumentException>(() => new TextAnnotation(id, point, " ", "Segoe UI", 12, RgbaColor.Black));
    }
}
