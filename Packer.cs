namespace PSD2PNG;

public class Packer
{
    public class Node
    {
        public Node? Right;
        public Node? Down;
        public float X;
        public float Y;
        public float W;
        public float H;
        public bool Used;
    }

    public class Box(float width, float height)
    {
        public readonly float Height = height;
        public readonly float Width = width;
        public float Area => Width * Height;
        public float Max => Math.Max(Width, Height);
        public float Min => Math.Min(Width, Height);
        public Node? Fit;
    }

    public List<Box> Boxes { get; } = new();
    public Node? Root { get; private set; }

    public void AddBox(params Box[] box) => Boxes.AddRange(box);

    public enum FitType
    {
        Width,
        Height,
        Area,
        MaxSide
    }

    public void Fit(FitType fit = FitType.Area)
    {
        if (Boxes.Count == 0) return;
        var boxes = fit switch
        {
            FitType.Width => Boxes.OrderByDescending(x => x.Width)
                .ThenByDescending(x => x.Height).ToList(),
            FitType.Height => Boxes.OrderByDescending(x => x.Height)
                .ThenByDescending(x => x.Width).ToList(),
            FitType.Area => Boxes.OrderByDescending(x => x.Area)
                .ThenByDescending(x => x.Height)
                .ThenByDescending(x => x.Width).ToList(),
            FitType.MaxSide => Boxes.OrderByDescending(x => x.Max)
                .ThenByDescending(x => x.Min)
                .ThenByDescending(x => x.Height)
                .ThenByDescending(x => x.Width).ToList(),
            _ => throw new ArgumentOutOfRangeException(nameof(fit), fit, null)
        };
        Boxes.Clear();
        Boxes.AddRange(boxes);
        Root = new Node { W = Boxes[0].Width, H = Boxes[0].Height };

        foreach (var box in Boxes)
        {
            var node = FindNode(Root, box.Width, box.Height);
            box.Fit = node != null ? SplitNode(node, box.Width, box.Height) : GrowNode(box.Width, box.Height);
        }
    }

    private static Node? FindNode(Node root, float w, float h) =>
        root.Used ? FindNode(root.Right!, w, h) ?? FindNode(root.Down!, w, h) : w <= root.W && h <= root.H ? root : null;

    private static Node SplitNode(Node node, float w, float h)
    {
        node.Used = true;
        node.Down = new Node { X = node.X, Y = node.Y + h, W = node.W, H = node.H - h };
        node.Right = new Node { X = node.X + w, Y = node.Y, W = node.W - w, H = h };
        return node;
    }

    private Node? GrowNode(float w, float h)
    {
        var canGrowDown = w <= Root!.W;
        var canGrowRight = h <= Root.H;

        var shouldGrowRight = canGrowRight && Root.H >= Root.W + w;
        var shouldGrowDown = canGrowDown && Root.W >= Root.H + h;

        if (shouldGrowRight)
            return GrowRight(w, h);
        if (shouldGrowDown)
            return GrowDown(w, h);
        if (canGrowRight)
            return GrowRight(w, h);
        if (canGrowDown)
            return GrowDown(w, h);
        return null;
    }

    private Node? GrowRight(float w, float h)
    {
        Root = new Node
        {
            Used = true,
            X = 0,
            Y = 0,
            W = Root!.W + w,
            H = Root.H,
            Down = Root,
            Right = new Node { X = Root.W, Y = 0, W = w, H = Root.H }
        };

        var node = FindNode(Root, w, h);
        return node != null ? SplitNode(node, w, h) : null;
    }

    private Node? GrowDown(float w, float h)
    {
        Root = new Node
        {
            Used = true,
            X = 0,
            Y = 0,
            W = Root!.W,
            H = Root.H + h,
            Down = new Node { X = 0, Y = Root.H, W = Root.W, H = h },
            Right = Root
        };
        var node = FindNode(Root, w, h);
        return node != null ? SplitNode(node, w, h) : null;
    }
}