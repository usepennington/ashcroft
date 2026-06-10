namespace Ashcroft;

/// <summary>One of nine positions a content stack can be pinned to.</summary>
public enum Anchor
{
    TopLeft, TopCenter, TopRight,
    MiddleLeft, Center, MiddleRight,
    BottomLeft, BottomCenter, BottomRight,
}

/// <summary>Built-in canvas presets. <see cref="OpenGraph"/> is the default 1200×630.</summary>
public enum CardSize { OpenGraph, Square, Wide, Story }

/// <summary>Encoded output formats.</summary>
public enum ImageFormat { Png, Jpeg, Webp }

/// <summary>How an image element is clipped.</summary>
public enum ImageShape { Rect, Rounded, Circle }

/// <summary>Horizontal alignment of elements within a stack.</summary>
public enum HorizontalAlign { Left, Center, Right }
