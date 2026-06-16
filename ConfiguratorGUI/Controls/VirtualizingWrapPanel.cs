using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace ConfiguratorGUI.Controls;

/// <summary>
/// A virtualizing wrap panel that lays out fixed-size items in a wrapping grid.
/// Supports both orientations, scroll-unit modes (pixel/item), and container recycling.
/// Requires ScrollViewer.CanContentScroll="True" on the owning ItemsControl.
/// </summary>
public class VirtualizingWrapPanel : VirtualizingPanel, IScrollInfo
{
    public const double DefaultScrollLineDeltaPixel = 16.0;
    public const double DefaultMouseWheelDeltaPixel = 48.0;
    public const int DefaultScrollLineDeltaItem = 1;
    public const int DefaultMouseWheelDeltaItem = 3;
    #region Dependency Properties

    public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(
        nameof(Orientation), typeof(Orientation), typeof(VirtualizingWrapPanel),
        new FrameworkPropertyMetadata(Orientation.Horizontal, FrameworkPropertyMetadataOptions.AffectsMeasure,
            (d, _) => ((VirtualizingWrapPanel)d).OnOrientationChanged()));

    public static readonly DependencyProperty ItemWidthProperty = DependencyProperty.Register(
        nameof(ItemWidth), typeof(double), typeof(VirtualizingWrapPanel),
        new FrameworkPropertyMetadata(230.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty ItemHeightProperty = DependencyProperty.Register(
        nameof(ItemHeight), typeof(double), typeof(VirtualizingWrapPanel),
        new FrameworkPropertyMetadata(250.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public Orientation Orientation
    {
        get => (Orientation)GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public double ItemWidth
    {
        get => (double)GetValue(ItemWidthProperty);
        set => SetValue(ItemWidthProperty, value);
    }

    public double ItemHeight
    {
        get => (double)GetValue(ItemHeightProperty);
        set => SetValue(ItemHeightProperty, value);
    }

    #endregion

    #region Fields

    private bool _canHScroll, _canVScroll;
    private Size _extent;
    private Size _viewport;
    private Point _offset;

    private int _itemsPerLine = 1;
    private int _firstRealizedIndex = -1;
    private int _lastRealizedIndex = -1;
    private int _prevFirstIndex = -1;
    private int _prevLastIndex = -1;

    #endregion

    #region Internal Accessors

    // Accessing InternalChildren forces ItemContainerGenerator initialization
    private ItemContainerGenerator Generator =>
        (InternalChildren, ItemContainerGenerator.GetItemContainerGeneratorForPanel(this)).Item2;

    private IRecyclingItemContainerGenerator RecyclingGenerator => Generator;

    // Cached items owner; resolved once (a panel's owner is fixed for its lifetime), so the
    // IScrollInfo getters don't walk the visual tree via GetItemsOwner on every read. The
    // attached-property reads below stay live, so a runtime ScrollUnit change is still honoured.
    private ItemsControl? _itemsControlCache;
    private ItemsControl? ItemsControl => _itemsControlCache ??= ItemsControl.GetItemsOwner(this);
    private ScrollUnit ScrollUnit => ItemsControl is { } ic ? GetScrollUnit(ic) : ScrollUnit.Pixel;
    private VirtualizationMode VirtualizationMode => ItemsControl is { } ic ? GetVirtualizationMode(ic) : VirtualizationMode.Recycling;
    private bool IsRecycling => VirtualizationMode == VirtualizationMode.Recycling;

    private VirtualizationCacheLength CacheLength => ItemsControl is { } ic ? GetCacheLength(ic) : new VirtualizationCacheLength(2);
    private VirtualizationCacheLengthUnit CacheLengthUnit => ItemsControl is { } ic ? GetCacheLengthUnit(ic) : VirtualizationCacheLengthUnit.Item;

    #endregion
    #region Math Helpers

    // Machine epsilon (2^-52), matching WPF's internal DoubleUtil.
    private const double DoubleEpsilon = 2.2204460492503131E-16;

    // comparing doubles to avoid floating-point layout loops
    private static bool AreClose(double value1, double value2)
    {
        if (value1 == value2) return true;
        double eps = (Math.Abs(value1) + Math.Abs(value2) + 10.0) * DoubleEpsilon;
        double delta = value1 - value2;
        return (-eps < delta) && (eps > delta);
    }

    private static bool AreSizesClose(Size size1, Size size2) =>
        AreClose(size1.Width, size2.Width) && AreClose(size1.Height, size2.Height);

    #endregion

    #region Orientation Helpers

    private double GetSizeOnMainAxis(Size size) =>
        Orientation == Orientation.Horizontal ? size.Width : size.Height;

    private double GetSizeOnCrossAxis(Size size) =>
        Orientation == Orientation.Horizontal ? size.Height : size.Width;

    private Size CreateSize(double mainAxis, double crossAxis) =>
        Orientation == Orientation.Horizontal
            ? new Size(mainAxis, crossAxis)
            : new Size(crossAxis, mainAxis);

    private Rect CreateRect(double mainPos, double crossPos, double mainSize, double crossSize) =>
        Orientation == Orientation.Horizontal
            ? new Rect(mainPos, crossPos, mainSize, crossSize)
            : new Rect(crossPos, mainPos, crossSize, mainSize);

    private double GetScrollOffset() =>
        Orientation == Orientation.Horizontal ? _offset.Y : _offset.X;

    #endregion

    #region Scroll Unit Conversion

    private double MainAxisItemSize => GetSizeOnMainAxis(new Size(ItemWidth, ItemHeight));
    private double CrossAxisItemSize => GetSizeOnCrossAxis(new Size(ItemWidth, ItemHeight));

    /// <summary>Converts a cross-axis pixel value to scroll units (rows for Item mode, pixels for Pixel mode).</summary>
    private double PixelsToScroll(double pixels) =>
        ScrollUnit == ScrollUnit.Item && CrossAxisItemSize > 0 ? pixels / CrossAxisItemSize : pixels;

    /// <summary>Converts a cross-axis scroll-unit value back to pixels.</summary>
    private double ScrollToPixels(double scrollUnits) =>
        ScrollUnit == ScrollUnit.Item ? scrollUnits * CrossAxisItemSize : scrollUnits;

    #endregion

    #region Scroll Delta Helpers

    private double GetLineDelta() =>
        ScrollUnit == ScrollUnit.Pixel
            ? DefaultScrollLineDeltaPixel
            : DefaultScrollLineDeltaItem;

    private double GetMouseWheelDelta() =>
        ScrollUnit == ScrollUnit.Pixel
            ? DefaultMouseWheelDeltaPixel
            : DefaultMouseWheelDeltaItem;

    private double GetPageDelta(bool vertical)
    {
        double itemDim = vertical ? ItemHeight : ItemWidth;
        double vpDim = vertical ? _viewport.Height : _viewport.Width;
        return ScrollUnit == ScrollUnit.Pixel
            ? vpDim
            : Math.Max(1, Math.Floor(vpDim / itemDim));
    }

    #endregion

    #region Layout

    private void OnOrientationChanged()
    {
        SetVerticalOffset(0);
        SetHorizontalOffset(0);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var itemSize = new Size(ItemWidth, ItemHeight);

        double mainAxisItemSize = Math.Max(1.0, MainAxisItemSize);
        double crossAxisItemSize = Math.Max(1.0, CrossAxisItemSize);

        double vpWidth = ResolveViewport(availableSize.Width,
            ScrollOwner?.ViewportWidth ?? 0, _viewport.Width, itemSize.Width);
        double vpHeight = ResolveViewport(availableSize.Height,
            ScrollOwner?.ViewportHeight ?? 0, _viewport.Height, itemSize.Height);
        var vpSize = new Size(vpWidth, vpHeight);

        double vpMainAxis = GetSizeOnMainAxis(vpSize);
        double vpCrossAxis = GetSizeOnCrossAxis(vpSize);

        int itemCount = Generator.Items.Count;
        _itemsPerLine = Math.Max(1, vpMainAxis > 0 ? (int)(vpMainAxis / mainAxisItemSize) : 1);
        int totalLines = (int)Math.Ceiling((double)itemCount / _itemsPerLine);

        var newExtent = CreateSize(_itemsPerLine * mainAxisItemSize, totalLines * crossAxisItemSize);
        var newViewport = CreateSize(vpMainAxis, vpCrossAxis);

        if (!AreSizesClose(_extent, newExtent) || !AreSizesClose(_viewport, newViewport))
        {
            _extent = newExtent;
            _viewport = newViewport;
            ScrollOwner?.InvalidateScrollInfo();
        }

        // Clamp scroll offset on cross axis
        double maxScroll = Math.Max(0, GetSizeOnCrossAxis(_extent) - GetSizeOnCrossAxis(_viewport));
        double scrollOffset = GetScrollOffset();
        if (scrollOffset > maxScroll)
        {
            if (Orientation == Orientation.Horizontal)
                _offset.Y = maxScroll;
            else
                _offset.X = maxScroll;
            scrollOffset = maxScroll;
        }

        if (itemCount == 0 || vpCrossAxis <= 0)
            return CreateSize(vpMainAxis, 0);

        // Compute visible range with cache-length buffers
        var cache = CacheLength;
        int bufferBefore = CacheToLines(cache.CacheBeforeViewport, crossAxisItemSize, vpCrossAxis);
        int bufferAfter  = CacheToLines(cache.CacheAfterViewport,  crossAxisItemSize, vpCrossAxis);

        int firstLine = Math.Max(0, (int)(scrollOffset / crossAxisItemSize) - bufferBefore);
        int lastLine = Math.Min(totalLines - 1,
            (int)((scrollOffset + vpCrossAxis) / crossAxisItemSize) + bufferAfter);
        int firstIndex = firstLine * _itemsPerLine;
        int lastIndex = Math.Min(itemCount - 1, (lastLine + 1) * _itemsPerLine - 1);

        bool rangeChanged = firstIndex != _prevFirstIndex || lastIndex != _prevLastIndex;

        // Virtualize: remove/recycle children outside the realized range
        if (rangeChanged)
        {
            int runStart = -1; // start index of a contiguous run to remove
            int runCount = 0;

            for (int i = InternalChildren.Count - 1; i >= 0; i--)
            {
                var pos = new GeneratorPosition(i, 0);
                int idx = RecyclingGenerator.IndexFromGeneratorPosition(pos);
                if (idx < firstIndex || idx > lastIndex)
                {
                    if (IsRecycling)
                        RecyclingGenerator.Recycle(pos, 1);
                    else
                        RecyclingGenerator.Remove(pos, 1);

                    if (runStart == -1)
                    {
                        runStart = i;
                        runCount = 1;
                    }
                    else if (i == runStart - 1)
                    {
                        runStart = i;
                        runCount++;
                    }
                    else
                    {
                        RemoveInternalChildRange(runStart, runCount);
                        runStart = i;
                        runCount = 1;
                    }
                }
                else if (runStart != -1)
                {
                    RemoveInternalChildRange(runStart, runCount);
                    runStart = -1;
                    runCount = 0;
                }
            }

            if (runStart != -1)
                RemoveInternalChildRange(runStart, runCount);

            _prevFirstIndex = firstIndex;
            _prevLastIndex = lastIndex;
        }

        _firstRealizedIndex = firstIndex;
        _lastRealizedIndex = lastIndex;

        // Realize visible children
        var startPos = RecyclingGenerator.GeneratorPositionFromIndex(firstIndex);
        int childIndex = startPos.Index + (startPos.Offset > 0 ? 1 : 0);

        using (RecyclingGenerator.StartAt(startPos, GeneratorDirection.Forward, true))
        {
            for (int i = firstIndex; i <= lastIndex; i++, childIndex++)
            {
                if (RecyclingGenerator.GenerateNext(out bool isNew) is not UIElement child)
                    break;

                bool alreadyInPlace = !isNew
                    && childIndex < InternalChildren.Count
                    && InternalChildren[childIndex] == child;

                if (!alreadyInPlace)
                {
                    if (childIndex >= InternalChildren.Count)
                        AddInternalChild(child);
                    else
                        InsertInternalChild(childIndex, child);

                    RecyclingGenerator.PrepareItemContainer(child);
                }

                if (!alreadyInPlace || child.DesiredSize != itemSize)
                    child.Measure(itemSize);
            }
        }

        return CreateSize(vpMainAxis, Math.Min(vpCrossAxis, GetSizeOnCrossAxis(newExtent)));
    }

    private static double ResolveViewport(double available, double ownerVp, double cached, double fallback)
    {
        if (!double.IsInfinity(available)) return available;
        if (ownerVp > 0) return ownerVp;
        if (cached > 0) return cached;
        return fallback;
    }

    private int CacheToLines(double cacheValue, double crossAxisItemSize, double vpCrossAxis) =>
        CacheLengthUnit switch
        {
            VirtualizationCacheLengthUnit.Pixel =>
                (int)Math.Ceiling(cacheValue / crossAxisItemSize),
            VirtualizationCacheLengthUnit.Page =>
                (int)Math.Ceiling(cacheValue * vpCrossAxis / crossAxisItemSize),
            _ => (int)Math.Ceiling(cacheValue), // Item -> one unit = one line
        };

    protected override Size ArrangeOverride(Size finalSize)
    {
        double mainAxisItemSize = MainAxisItemSize;
        double crossAxisItemSize = CrossAxisItemSize;
        double scrollOffset = GetScrollOffset();
        int itemsPerLine = _itemsPerLine;

        for (int i = 0; i < InternalChildren.Count; i++)
        {
            int index = RecyclingGenerator.IndexFromGeneratorPosition(new GeneratorPosition(i, 0));
            if (index < 0) continue;

            double mainPos = (index % itemsPerLine) * mainAxisItemSize;
            double crossPos = (index / itemsPerLine) * crossAxisItemSize - scrollOffset;
            InternalChildren[i].Arrange(CreateRect(mainPos, crossPos, mainAxisItemSize, crossAxisItemSize));
        }

        return finalSize;
    }

    protected override void OnItemsChanged(object sender, ItemsChangedEventArgs args)
    {
        switch (args.Action)
        {
            case NotifyCollectionChangedAction.Remove:
            case NotifyCollectionChangedAction.Replace:
                if (args.Position.Offset == 0 && args.Position.Index >= 0)
                    RemoveInternalChildRange(args.Position.Index, args.ItemUICount);
                break;

            case NotifyCollectionChangedAction.Move:
                if (args.OldPosition.Offset == 0 && args.OldPosition.Index >= 0)
                    RemoveInternalChildRange(args.OldPosition.Index, args.ItemUICount);
                break;

            case NotifyCollectionChangedAction.Reset:
                if (InternalChildren.Count > 0)
                {
                    if (IsRecycling)
                    {
                        for (int i = InternalChildren.Count - 1; i >= 0; i--)
                            RecyclingGenerator.Recycle(new GeneratorPosition(i, 0), 1);
                    }
                    RemoveInternalChildRange(0, InternalChildren.Count);
                }
                _prevFirstIndex = -1;
                _prevLastIndex = -1;
                if (Orientation == Orientation.Horizontal)
                    SetVerticalOffset(0);
                else
                    SetHorizontalOffset(0);
                break;
        }

        base.OnItemsChanged(sender, args);
    }

    protected override void BringIndexIntoView(int index)
    {
        if (index < 0 || index >= Generator.Items.Count || _itemsPerLine <= 0)
            return;

        double crossAxisItemSize = CrossAxisItemSize;
        double vpCrossAxis = GetSizeOnCrossAxis(_viewport);

        int line = index / _itemsPerLine;
        double itemStart = line * crossAxisItemSize;
        double itemEnd = itemStart + crossAxisItemSize;
        double scrollOffset = GetScrollOffset();

        double newOffset;
        if (itemStart < scrollOffset)
            newOffset = itemStart;
        else if (itemEnd > scrollOffset + vpCrossAxis)
            newOffset = itemEnd - vpCrossAxis;
        else
            return; // already visible

        double scrollUnits = PixelsToScroll(newOffset);
        if (Orientation == Orientation.Horizontal)
            SetVerticalOffset(scrollUnits);
        else
            SetHorizontalOffset(scrollUnits);
    }

    #endregion

    #region IScrollInfo

    public bool CanHorizontallyScroll { get => _canHScroll; set => _canHScroll = value; }
    public bool CanVerticallyScroll { get => _canVScroll; set => _canVScroll = value; }
    public double ExtentWidth => Orientation == Orientation.Vertical ? PixelsToScroll(_extent.Width) : _extent.Width;
    public double ExtentHeight => Orientation == Orientation.Horizontal ? PixelsToScroll(_extent.Height) : _extent.Height;
    public double ViewportWidth => Orientation == Orientation.Vertical ? PixelsToScroll(_viewport.Width) : _viewport.Width;
    public double ViewportHeight => Orientation == Orientation.Horizontal ? PixelsToScroll(_viewport.Height) : _viewport.Height;
    public double HorizontalOffset => Orientation == Orientation.Vertical ? PixelsToScroll(_offset.X) : _offset.X;
    public double VerticalOffset => Orientation == Orientation.Horizontal ? PixelsToScroll(_offset.Y) : _offset.Y;
    public ScrollViewer? ScrollOwner { get; set; }

    public void LineUp() => SetVerticalOffset(VerticalOffset - GetLineDelta());
    public void LineDown() => SetVerticalOffset(VerticalOffset + GetLineDelta());
    public void LineLeft() => SetHorizontalOffset(HorizontalOffset - GetLineDelta());
    public void LineRight() => SetHorizontalOffset(HorizontalOffset + GetLineDelta());

    public void PageUp() => SetVerticalOffset(VerticalOffset - GetPageDelta(true));
    public void PageDown() => SetVerticalOffset(VerticalOffset + GetPageDelta(true));
    public void PageLeft() => SetHorizontalOffset(HorizontalOffset - GetPageDelta(false));
    public void PageRight() => SetHorizontalOffset(HorizontalOffset + GetPageDelta(false));

    public void MouseWheelUp() => SetVerticalOffset(VerticalOffset - GetMouseWheelDelta());
    public void MouseWheelDown() => SetVerticalOffset(VerticalOffset + GetMouseWheelDelta());
    public void MouseWheelLeft() => SetHorizontalOffset(HorizontalOffset - GetMouseWheelDelta());
    public void MouseWheelRight() => SetHorizontalOffset(HorizontalOffset + GetMouseWheelDelta());

    public void SetHorizontalOffset(double offset)
    {
        double pixelOffset = Orientation == Orientation.Vertical ? ScrollToPixels(offset) : offset;
        pixelOffset = Math.Clamp(pixelOffset, 0, Math.Max(0, _extent.Width - _viewport.Width));
        if (pixelOffset == _offset.X) return;
        _offset.X = pixelOffset;
        ScrollOwner?.InvalidateScrollInfo();

        if (IsScrollWithinRealizedRange())
            InvalidateArrange();
        else
            InvalidateMeasure();
    }

    public void SetVerticalOffset(double offset)
    {
        double pixelOffset = Orientation == Orientation.Horizontal ? ScrollToPixels(offset) : offset;
        pixelOffset = Math.Clamp(pixelOffset, 0, Math.Max(0, _extent.Height - _viewport.Height));
        if (pixelOffset == _offset.Y) return;
        _offset.Y = pixelOffset;
        ScrollOwner?.InvalidateScrollInfo();

        if (IsScrollWithinRealizedRange())
            InvalidateArrange();
        else
            InvalidateMeasure();
    }

    private bool IsScrollWithinRealizedRange()
    {
        if (_firstRealizedIndex < 0 || _itemsPerLine <= 0)
            return false;

        double crossAxisItemSize = CrossAxisItemSize;
        if (crossAxisItemSize <= 0)
            return false;

        double scrollOffset = GetScrollOffset();
        double vpCrossAxis = GetSizeOnCrossAxis(_viewport);

        int firstVisibleLine = (int)(scrollOffset / crossAxisItemSize);
        int lastVisibleLine = (int)((scrollOffset + vpCrossAxis) / crossAxisItemSize);
        int neededFirst = firstVisibleLine * _itemsPerLine;
        int neededLast = (lastVisibleLine + 1) * _itemsPerLine - 1;

        return neededFirst >= _firstRealizedIndex && neededLast <= _lastRealizedIndex;
    }

    public Rect MakeVisible(Visual visual, Rect rectangle)
    {
        if (!visual.IsDescendantOf(this))
            return Rect.Empty;

        var transformedBounds = visual.TransformToAncestor(this).TransformBounds(rectangle);

        // transformedBounds is in pixels; compare against the pixel viewport, not the
        // scroll-unit ViewportWidth/Height (which are rows in Item mode). Pixel->scroll-unit
        // conversion happens only at the Set*Offset boundary below.
        double vpWidth = _viewport.Width;
        double vpHeight = _viewport.Height;

        double offsetX = 0;
        double offsetY = 0;
        double visibleX = 0;
        double visibleY = 0;
        double visibleWidth = Math.Min(rectangle.Width, vpWidth);
        double visibleHeight = Math.Min(rectangle.Height, vpHeight);

        if (transformedBounds.Left < 0)
            offsetX = transformedBounds.Left;
        else if (transformedBounds.Right > vpWidth)
        {
            offsetX = Math.Min(transformedBounds.Right - vpWidth, transformedBounds.Left);
            if (rectangle.Width > vpWidth)
                visibleX = rectangle.Width - vpWidth;
        }

        if (transformedBounds.Top < 0)
            offsetY = transformedBounds.Top;
        else if (transformedBounds.Bottom > vpHeight)
        {
            offsetY = Math.Min(transformedBounds.Bottom - vpHeight, transformedBounds.Top);
            if (rectangle.Height > vpHeight)
                visibleY = rectangle.Height - vpHeight;
        }

        double scrollDeltaX = Orientation == Orientation.Vertical ? PixelsToScroll(offsetX) : offsetX;
        double scrollDeltaY = Orientation == Orientation.Horizontal ? PixelsToScroll(offsetY) : offsetY;
        SetHorizontalOffset(HorizontalOffset + scrollDeltaX);
        SetVerticalOffset(VerticalOffset + scrollDeltaY);

        return new Rect(visibleX, visibleY, visibleWidth, visibleHeight);
    }

    #endregion
}
