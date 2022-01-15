using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using JetBrains.Annotations;
using Microsoft.Win32;
using MonBand.Core.Util.Models;

namespace MonBand.Windows.ComHost;

[SuppressMessage("ReSharper", "UnusedMember.Global")]
sealed class CSDeskBandImpl : ICSDeskBand
{
    readonly IDeskBandProvider _provider;

    readonly Dictionary<uint, DeskBandMenuAction> _contextMenuActions = new();

    IntPtr _parentWindowHandle;
    object _parentSite; // Has these interfaces: IInputObjectSite, IOleWindow, IOleCommandTarget, IBandSite
    uint _id;
    uint _menuStartId;

    // Command group id for deskband. Used for IOleCommandTarget.Exec
    Guid _deskbandCommandGroupId = new("EB0FE172-1A3A-11D0-89B3-00A0C90A90AC");

    public CSDeskBandImpl(IDeskBandProvider provider)
    {
        this._provider = provider;
        this.Options = provider.Options;
        this.Options.PropertyChanged += this.Options_PropertyChanged;
    }

    /// <summary>
    /// Occurs when the deskband is closed.
    /// </summary>
    internal event EventHandler Closed;

    /// <summary>
    /// Gets the <see cref="CSDeskBandOptions"/>.
    /// </summary>
    internal CSDeskBandOptions Options { get; }

    /// <summary>
    /// Gets the <see cref="TaskbarInfo"/>.
    /// </summary>
    internal TaskbarInfo TaskbarInfo { get; } = new();

    public int GetWindow(out IntPtr pHwnd)
    {
        pHwnd = this._provider.Handle;
        return HRESULT.S_OK;
    }

    public int ContextSensitiveHelp(bool fEnterMode)
    {
        return HRESULT.E_NOTIMPL;
    }

    public int ShowDW([In] bool fShow)
    {
        return HRESULT.S_OK;
    }

    public int CloseDW([In] uint dwReserved)
    {
        this.Closed?.Invoke(this, EventArgs.Empty);
        return HRESULT.S_OK;
    }

    public int ResizeBorderDW(RECT prcBorder, [In, MarshalAs(UnmanagedType.IUnknown)] IntPtr punkToolbarSite,
        bool fReserved)
    {
        // Must return notimpl
        return HRESULT.E_NOTIMPL;
    }

    public int GetBandInfo(uint dwBandId, DESKBANDINFO.DBIF dwViewMode, ref DESKBANDINFO pdbi)
    {
        // Sizing information is requested whenever the taskbar changes size/orientation
        this._id = dwBandId;

        if (pdbi.dwMask.HasFlag(DESKBANDINFO.DBIM.DBIM_MINSIZE))
        {
            if (dwViewMode.HasFlag(DESKBANDINFO.DBIF.DBIF_VIEWMODE_VERTICAL))
            {
                pdbi.ptMinSize.Y = this.Options.MinVerticalSize.Width;
                pdbi.ptMinSize.X = this.Options.MinVerticalSize.Height;
            }
            else
            {
                pdbi.ptMinSize.X = this.Options.MinHorizontalSize.Width;
                pdbi.ptMinSize.Y = this.Options.MinHorizontalSize.Height;
            }
        }

        // X is ignored
        if (pdbi.dwMask.HasFlag(DESKBANDINFO.DBIM.DBIM_MAXSIZE))
        {
            if (dwViewMode.HasFlag(DESKBANDINFO.DBIF.DBIF_VIEWMODE_VERTICAL))
            {
                pdbi.ptMaxSize.Y = this.Options.MaxVerticalWidth;
                pdbi.ptMaxSize.X = 0;
            }
            else
            {
                pdbi.ptMaxSize.X = 0;
                pdbi.ptMaxSize.Y = this.Options.MaxHorizontalHeight;
            }
        }

        // x member is ignored
        if (pdbi.dwMask.HasFlag(DESKBANDINFO.DBIM.DBIM_INTEGRAL))
        {
            pdbi.ptIntegral.Y = this.Options.HeightIncrement;
            pdbi.ptIntegral.X = 0;
        }

        if (pdbi.dwMask.HasFlag(DESKBANDINFO.DBIM.DBIM_ACTUAL))
        {
            if (dwViewMode.HasFlag(DESKBANDINFO.DBIF.DBIF_VIEWMODE_VERTICAL))
            {
                pdbi.ptActual.Y = this.Options.VerticalSize.Width;
                pdbi.ptActual.X = this.Options.VerticalSize.Height;
            }
            else
            {
                pdbi.ptActual.X = this.Options.HorizontalSize.Width;
                pdbi.ptActual.Y = this.Options.HorizontalSize.Height;
            }
        }

        if (pdbi.dwMask.HasFlag(DESKBANDINFO.DBIM.DBIM_TITLE))
        {
            pdbi.wszTitle = this.Options.Title;
            if (!this.Options.ShowTitle)
            {
                pdbi.dwMask &= ~DESKBANDINFO.DBIM.DBIM_TITLE;
            }
        }

        if (pdbi.dwMask.HasFlag(DESKBANDINFO.DBIM.DBIM_MODEFLAGS))
        {
            pdbi.dwModeFlags = DESKBANDINFO.DBIMF.DBIMF_NORMAL;
            pdbi.dwModeFlags |= this.Options.IsFixed
                ? DESKBANDINFO.DBIMF.DBIMF_FIXED | DESKBANDINFO.DBIMF.DBIMF_NOGRIPPER
                : 0;
            pdbi.dwModeFlags |= this.Options.HeightCanChange ? DESKBANDINFO.DBIMF.DBIMF_VARIABLEHEIGHT : 0;
            pdbi.dwModeFlags &= ~DESKBANDINFO.DBIMF.DBIMF_BKCOLOR; // Don't use background color
        }

        this.TaskbarInfo.UpdateInfo();

        return HRESULT.S_OK;
    }

    public int CanRenderComposited(out bool pfCanRenderComposited)
    {
        pfCanRenderComposited = true;
        return HRESULT.S_OK;
    }

    public int SetCompositionState(bool fCompositionEnabled)
    {
        return HRESULT.S_OK;
    }

    public int GetCompositionState(out bool pfCompositionEnabled)
    {
        pfCompositionEnabled = true;
        return HRESULT.S_OK;
    }

    public int SetSite([In, MarshalAs(UnmanagedType.IUnknown)] object pUnkSite)
    {
        // Let gc release old site
        this._parentSite = null;

        // pUnkSite null means deskband was closed
        if (pUnkSite == null)
        {
            this.Closed?.Invoke(this, EventArgs.Empty);
            return HRESULT.S_OK;
        }

        try
        {
            var oleWindow = (IOleWindow)pUnkSite;
            oleWindow.GetWindow(out this._parentWindowHandle);
            User32.SetParent(this._provider.Handle, this._parentWindowHandle);

            this._parentSite = (IInputObjectSite)pUnkSite;
            return HRESULT.S_OK;
        }
        catch
        {
            return HRESULT.E_FAIL;
        }
    }

    public int GetSite(ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out IntPtr ppvSite)
    {
        if (this._parentSite == null)
        {
            ppvSite = IntPtr.Zero;
            return HRESULT.E_FAIL;
        }

        return Marshal.QueryInterface(Marshal.GetIUnknownForObject(this._parentSite), ref riid, out ppvSite);
    }

    public int QueryContextMenu(IntPtr hMenu, uint indexMenu, uint idCmdFirst, uint idCmdLast,
        QueryContextMenuFlags uFlags)
    {
        if (uFlags.HasFlag(QueryContextMenuFlags.CMF_DEFAULTONLY))
        {
            return HRESULT.MakeHResult((uint)HRESULT.S_OK, 0, 0);
        }

        this._menuStartId = idCmdFirst;
        foreach (var item in this.Options.ContextMenuItems)
        {
            item.AddToMenu(hMenu, indexMenu++, ref idCmdFirst, this._contextMenuActions);
        }

        return HRESULT.MakeHResult((uint)HRESULT.S_OK, 0, idCmdFirst + 1); // #id of last command + 1
    }

    public int InvokeCommand(IntPtr pici)
    {
        var commandInfo = Marshal.PtrToStructure<CMINVOKECOMMANDINFO>(pici);
        var verbPtr = commandInfo!.lpVerb;

        if (commandInfo.cbSize == Marshal.SizeOf<CMINVOKECOMMANDINFOEX>())
        {
            var extended = Marshal.PtrToStructure<CMINVOKECOMMANDINFOEX>(pici);
            if (extended.fMask.HasFlag(CMINVOKECOMMANDINFOEX.CMIC.CMIC_MASK_UNICODE))
            {
                verbPtr = extended.lpVerbW;
            }
        }

        if (User32.HiWord(commandInfo.lpVerb.ToInt32()) != 0)
        {
            return HRESULT.E_FAIL;
        }

        var cmdIndex = User32.LoWord(verbPtr.ToInt32());

        if (!this._contextMenuActions.TryGetValue((uint)cmdIndex + this._menuStartId, out var action))
        {
            return HRESULT.E_FAIL;
        }

        action.DoAction();
        return HRESULT.S_OK;
    }

    public int GetCommandString(ref uint idcmd, uint uflags, ref uint pwReserved, out string pcszName, uint cchMax)
    {
        pcszName = "";
        return HRESULT.E_NOTIMPL;
    }

    public int HandleMenuMsg(uint uMsg, IntPtr wParam, IntPtr lParam)
    {
        return this.HandleMenuMsg2(uMsg, wParam, lParam, out _);
    }

    public int HandleMenuMsg2(uint uMsg, IntPtr wParam, IntPtr lParam, out IntPtr plResult)
    {
        plResult = IntPtr.Zero;
        return HRESULT.S_OK;
    }

    public int GetClassID(out Guid pClassId)
    {
        pClassId = this._provider.Guid;
        return HRESULT.S_OK;
    }

    public int GetSizeMax(out ulong pcbSize)
    {
        pcbSize = 0;
        return HRESULT.S_OK;
    }

    public int IsDirty()
    {
        return HRESULT.S_OK;
    }

    public int Load(object pStm)
    {
        return HRESULT.S_OK;
    }

    public int Save(IntPtr pStm, bool fClearDirty)
    {
        return HRESULT.S_OK;
    }
    
    public void CloseDeskBand()
    {
        var bandSite = (IBandSite)this._parentSite;
        bandSite.RemoveBand(this._id);
    }

    public int UIActivateIO(int fActivate, ref MSG msg)
    {
        this._provider.HasFocus = fActivate != 0;
        this.UpdateFocus(this._provider.HasFocus);
        return HRESULT.S_OK;
    }

    public int HasFocusIO()
    {
        return this._provider.HasFocus ? HRESULT.S_OK : HRESULT.S_FALSE;
    }

    public int TranslateAcceleratorIO(ref MSG msg)
    {
        return HRESULT.S_OK;
    }

    /// <summary>
    /// Updates the focus on the deskband. Explorer will call <see cref="UIActivateIO(int, ref MSG)"/> for example if tabbing when the taskbar is focused. 
    /// But if focus is acquired without in other ways, then explorer isn't aware of it and <see cref="IInputObjectSite.OnFocusChangeIS(object, int)"/> needs to be called.
    /// </summary>
    /// <param name="focused">True if focused.</param>
    public void UpdateFocus(bool focused)
    {
        (this._parentSite as IInputObjectSite)?.OnFocusChangeIS(this, focused ? 1 : 0);
    }

    void Options_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (this._parentSite == null)
        {
            return;
        }

        var parent = (IOleCommandTarget)this._parentSite;

        // Set pvaln to the id that was passed in SetSite
        // When int is marshalled to variant, it is marshalled as VT_i4. See default marshalling for objects
        parent.Exec(ref this._deskbandCommandGroupId,
            (uint)tagDESKBANDCID.DBID_BANDINFOCHANGED,
            0,
            IntPtr.Zero,
            IntPtr.Zero);
    }
}

[SuppressMessage("ReSharper", "UnusedMember.Global")]
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
public sealed class CSDeskBandOptions : ObservableModelBase
{
    /// <summary>
    /// Height for a default horizontal taskbar.
    /// </summary>
    [SuppressMessage("ReSharper", "ConvertToConstant.Global")] 
    public static readonly int TaskbarHorizontalHeightLarge = 40;

    /// <summary>
    /// Height for a default horizontal taskbar with small icons.
    /// </summary>
    [SuppressMessage("ReSharper", "ConvertToConstant.Global")]
    public static readonly int TaskbarHorizontalHeightSmall = 30;

    /// <summary>
    /// Width for a default vertical taskbar. There is no small vertical taskbar.
    /// </summary>
    [SuppressMessage("ReSharper", "ConvertToConstant.Global")]
    public static readonly int TaskbarVerticalWidth = 62;

    /// <summary>
    /// Value that represents no limit for deskband size.
    /// </summary>
    /// <seealso cref="MaxHorizontalHeight"/>
    /// <seealso cref="MaxVerticalWidth"/>
    [SuppressMessage("ReSharper", "ConvertToConstant.Global")]
    public static readonly int NoLimit = -1;

    DeskBandSize _horizontalSize;
    int _maxHorizontalHeight;
    DeskBandSize _minHorizontalSize;
    DeskBandSize _verticalSize;
    int _maxVerticalWidth;
    DeskBandSize _minVerticalSize;
    string _title = "";
    bool _showTitle;
    bool _isFixed;
    int _heightIncrement = 1;
    bool _heightCanChange = true;
    ICollection<DeskBandMenuItem> _contextMenuItems = new List<DeskBandMenuItem>();

    /// <summary>
    /// Initializes a new instance of the <see cref="CSDeskBandOptions"/> class.
    /// </summary>
    public CSDeskBandOptions()
    {
        // Initialize in constructor to hook up property change events
        this.HorizontalSize = new DeskBandSize(200, TaskbarHorizontalHeightLarge);
        this.MaxHorizontalHeight = NoLimit;
        this.MinHorizontalSize = new DeskBandSize(NoLimit, NoLimit);

        this.VerticalSize = new DeskBandSize(TaskbarVerticalWidth, 200);
        this.MaxVerticalWidth = NoLimit;
        this.MinVerticalSize = new DeskBandSize(NoLimit, NoLimit);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the height of the horizontal deskband is allowed to change.
    /// Or for a deskband in the vertical orientation, if the width can change.
    /// </summary>
    public bool HeightCanChange
    {
        get => this._heightCanChange;
        set => this.Set(ref this._heightCanChange, value);
    }

    /// <summary>
    /// Gets or sets the height step size of a horizontal deskband when it is being resized.
    /// For a deskband in the vertical orientation, it will be the step size of the width.
    /// <para/>
    /// The deskband will only be resized to multiples of this value.
    /// </summary>
    /// <example>
    /// If increment is 50, then the height of the deskband can only be resized to 50, 100 ...
    /// </example>
    /// <value>
    /// The step size for resizing. This value is only used if <see cref="HeightCanChange"/> is true. If the value is less than 0, the height / width can be any size.
    /// The default value is 1.
    /// </value>
    public int HeightIncrement
    {
        get => this._heightIncrement;
        set => this.Set(ref this._heightIncrement, value);
    }

    public bool IsFixed
    {
        get => this._isFixed;
        set => this.Set(ref this._isFixed, value);
    }

    public bool ShowTitle
    {
        get => this._showTitle;
        set => this.Set(ref this._showTitle, value);
    }

    public string Title
    {
        get => this._title;
        set => this.Set(ref this._title, value ?? string.Empty);
    }

    /// <summary>
    /// Gets or sets the minimum <see cref="DeskBandSize"/> of the deskband in the vertical orientation.
    /// </summary>
    /// <seealso cref="TaskbarOrientation"/>
    /// <value>
    /// The default value is <see cref="NoLimit"/> for the width and height.
    /// </value>
    public DeskBandSize MinVerticalSize
    {
        get => this._minVerticalSize;
        set => this.Set(ref this._minVerticalSize, value);
    }

    /// <summary>
    /// Gets or sets the maximum width of the deskband in the vertical orientation.
    /// </summary>
    /// <remarks>
    /// The maximum height will have to be addressed in your code as there is no limit to the height of the deskband when vertical.
    /// </remarks>
    /// <seealso cref="TaskbarOrientation"/>
    /// <value>
    /// The default value is <see cref="NoLimit"/>.
    /// </value>
    public int MaxVerticalWidth
    {
        get => this._maxVerticalWidth;
        set => this.Set(ref this._maxVerticalWidth, value);
    }

    /// <summary>
    /// Gets or sets the ideal <see cref="DeskBandSize"/> of the deskband in the vertical orientation.
    /// There is no guarantee that the deskband will be this size.
    /// </summary>
    /// <seealso cref="TaskbarOrientation"/>
    /// <value>
    /// The default value is <see cref="TaskbarVerticalWidth"/> for the width and 200 for the height.
    /// </value>
    public DeskBandSize VerticalSize
    {
        get => this._verticalSize;
        set => this.Set(ref this._verticalSize, value);
    }

    /// <summary>
    /// Gets or sets the minimum <see cref="DeskBandSize"/> of the deskband in the horizontal orientation.
    /// </summary>
    /// <seealso cref="TaskbarOrientation"/>
    /// <value>
    /// The default value is <see cref="NoLimit"/>.
    /// </value>
    public DeskBandSize MinHorizontalSize
    {
        get => this._minHorizontalSize;
        set => this.Set(ref this._minHorizontalSize, value);
    }

    /// <summary>
    /// Gets or sets the maximum height of the deskband in the horizontal orientation.
    /// </summary>
    /// <remarks>
    /// The maximum width will have to be addressed in your code as there is no limit to the width of the deskband when horizontal.
    /// </remarks>
    /// <seealso cref="TaskbarOrientation"/>
    /// <value>
    /// The default value is <see cref="NoLimit"/>.
    /// </value>
    public int MaxHorizontalHeight
    {
        get => this._maxHorizontalHeight;
        set => this.Set(ref this._maxHorizontalHeight, value);
    }

    /// <summary>
    /// Gets or sets the ideal <see cref="DeskBandSize"/> of the deskband in the horizontal orientation.
    /// There is no guarantee that the deskband will be this size.
    /// </summary>
    /// <seealso cref="TaskbarOrientation"/>
    /// <value>
    /// The default value is 200 for the width and <see cref="TaskbarHorizontalHeightLarge"/> for the height.
    /// </value>
    public DeskBandSize HorizontalSize
    {
        get => this._horizontalSize;
        set => this.Set(ref this._horizontalSize, value);
    }

    /// <summary>
    /// Gets or sets the collection of <see cref="DeskBandMenuItem"/> that comprise the deskband's context menu.
    /// </summary>
    /// <value>
    /// A list of <see cref="DeskBandMenuItem"/> for the context menu. An empty collection indicates no context menu.
    /// </value>
    /// <remarks>
    /// These context menu items are in addition of the default ones that windows provides.
    /// The items will appear in their enumerated order.
    /// </remarks>
    public ICollection<DeskBandMenuItem> ContextMenuItems
    {
        get => this._contextMenuItems;
        set => this.Set(ref this._contextMenuItems, value);
    }
}

[AttributeUsage(AttributeTargets.Class)]
sealed class CSDeskBandRegistrationAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the name of the deskband in the toolbar menu.
    /// </summary>
    /// <value>
    /// The name is used to select the deskband from the toolbars menu.
    /// </value>
    public string Name { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether to automatically show the deskband after registration.
    /// </summary>
    /// <value>
    /// <see langword="true"/> if the deskband should be automatically shown after registration; <see langword="false"/> otherwise.
    /// </value>
    public bool ShowDeskBand { get; init; }
}

/// <summary>
/// Wpf implementation of <see cref="ICSDeskBand"/>
/// The deskband should also have these attributes <see cref="ComVisibleAttribute"/>, <see cref="GuidAttribute"/>, <see cref="CSDeskBandRegistrationAttribute"/>.
/// </summary>
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
[SuppressMessage("ReSharper", "InconsistentNaming")]
public abstract class CSDeskBandWpf : ICSDeskBand, IDeskBandProvider
{
    readonly CSDeskBandImpl _impl;
    readonly AdornerDecorator _rootVisual;

    protected CSDeskBandWpf()
    {
        this.Options.Title = RegistrationHelper.GetToolbarName(this.GetType());

        var hwndSourceParameters = new HwndSourceParameters("Deskband host for wpf")
        {
            TreatAsInputRoot = true,
            WindowStyle = unchecked((int)(WindowStyles.WS_VISIBLE | WindowStyles.WS_POPUP)),
            HwndSourceHook = this.HwndSourceHook,
        };

        this.HwndSource = new HwndSource(hwndSourceParameters);
        this.HwndSource.SizeToContent = SizeToContent.Manual;
        this._rootVisual = new AdornerDecorator();
        this.HwndSource.RootVisual = this._rootVisual;
        
        var hwndSourceCompositionTarget = this.HwndSource.CompositionTarget;
        if (hwndSourceCompositionTarget != null)
        {
            hwndSourceCompositionTarget.BackgroundColor = Colors.Transparent;
        }

        this._impl = new CSDeskBandImpl(this);
        this._impl.Closed += (_, _) => this.DeskbandOnClosed();
        this.TaskbarInfo = this._impl.TaskbarInfo;
    }

    /// <summary>
    /// The <see cref="System.Windows.Interop.HwndSourceHook"/>. for <see cref="HwndSource"/>.
    /// </summary>
    [PublicAPI]
    [SuppressMessage("ReSharper", "IdentifierTypo")]
    [SuppressMessage("ReSharper", "RedundantAssignment")]
    protected virtual IntPtr HwndSourceHook(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam, ref bool handled)
    {
        switch (msg)
        {
            // Handle hit testing against transparent areas
            case (int)WindowMessages.WM_NCHITTEST:
                var mouseX = LowWord(lparam);
                var mouseY = HighWord(lparam);
                var relativepoint = this.HwndSource.RootVisual.PointFromScreen(new Point(mouseX, mouseY));
                var result = VisualTreeHelper.HitTest(this.HwndSource.RootVisual, relativepoint);
                if (result?.VisualHit != null)
                {
                    handled = true;
                    return new IntPtr((int)HitTestMessageResults.HTCLIENT);
                }
                else
                {
                    handled = true;
                    return new IntPtr((int)HitTestMessageResults.HTTRANSPARENT);
                }
        }

        handled = false;
        return IntPtr.Zero;
    }

    protected static int LowWord(IntPtr value)
    {
        return unchecked((short)(long)value);
    }

    protected static int HighWord(IntPtr value)
    {
        return unchecked((short)((long)value >> 16));
    }

    /// <summary>
    /// Gets the <see cref="System.Windows.Interop.HwndSource"/> that hosts the wpf content.
    /// </summary>
    protected HwndSource HwndSource { get; }

    /// <summary>
    /// Gets the taskbar information
    /// </summary>
    protected TaskbarInfo TaskbarInfo { get; }

    /// <summary>
    /// Gets main UI element for the deskband.
    /// </summary>
    protected abstract UIElement UIElement { get; }

    /// <summary>
    /// Gets the options for this deskband.
    /// </summary>
    /// <seealso cref="CSDeskBandOptions"/>
    public CSDeskBandOptions Options { get; } = new();

    /// <summary>
    /// Gets the handle
    /// </summary>
    public IntPtr Handle
    {
        get
        {
            this._rootVisual.Child ??= this.UIElement;
            return this.HwndSource.Handle;
        }
    }

    /// <summary>
    /// Gets the deskband guid
    /// </summary>
    public Guid Guid => this.GetType().GUID;

    public bool HasFocus
    {
        get => this.UIElement?.IsKeyboardFocusWithin ?? false;
        set
        {
            if (value)
            {
                this.UIElement?.Focus();
            }
        }
    }

    /// <summary>
    /// Updates the focus on this deskband.
    /// </summary>
    /// <param name="focused"><see langword="true"/> if focused.</param>
    public void UpdateFocus(bool focused)
    {
        this._impl.UpdateFocus(focused);
    }

    /// <summary>
    /// Handle closing of the deskband.
    /// </summary>
    protected virtual void DeskbandOnClosed()
    {
    }

    public int GetWindow(out IntPtr pHwnd)
    {
        return this._impl.GetWindow(out pHwnd);
    }

    public int ContextSensitiveHelp(bool fEnterMode)
    {
        return this._impl.ContextSensitiveHelp(fEnterMode);
    }

    public int ShowDW([In] bool fShow)
    {
        return this._impl.ShowDW(fShow);
    }

    public int CloseDW([In] uint dwReserved)
    {
        return this._impl.CloseDW(dwReserved);
    }

    public int ResizeBorderDW(RECT prcBorder, [In, MarshalAs(UnmanagedType.IUnknown)] IntPtr punkToolbarSite,
        bool fReserved)
    {
        return this._impl.ResizeBorderDW(prcBorder, punkToolbarSite, fReserved);
    }

    public int GetBandInfo(uint dwBandID, DESKBANDINFO.DBIF dwViewMode, ref DESKBANDINFO pdbi)
    {
        return this._impl.GetBandInfo(dwBandID, dwViewMode, ref pdbi);
    }

    public int CanRenderComposited(out bool pfCanRenderComposited)
    {
        return this._impl.CanRenderComposited(out pfCanRenderComposited);
    }

    public int SetCompositionState(bool fCompositionEnabled)
    {
        return this._impl.SetCompositionState(fCompositionEnabled);
    }

    public int GetCompositionState(out bool pfCompositionEnabled)
    {
        return this._impl.GetCompositionState(out pfCompositionEnabled);
    }

    public int SetSite([In, MarshalAs(UnmanagedType.IUnknown)] object pUnkSite)
    {
        return this._impl.SetSite(pUnkSite);
    }

    public int GetSite(ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out IntPtr ppvSite)
    {
        return this._impl.GetSite(ref riid, out ppvSite);
    }

    public int QueryContextMenu(IntPtr hMenu, uint indexMenu, uint idCmdFirst, uint idCmdLast,
        QueryContextMenuFlags uFlags)
    {
        return this._impl.QueryContextMenu(hMenu, indexMenu, idCmdFirst, idCmdLast, uFlags);
    }

    public int InvokeCommand(IntPtr pici)
    {
        return this._impl.InvokeCommand(pici);
    }

    public int GetCommandString(ref uint idcmd, uint uflags, ref uint pwReserved,
        [MarshalAs(UnmanagedType.LPTStr)] out string pcszName, uint cchMax)
    {
        return this._impl.GetCommandString(ref idcmd, uflags, ref pwReserved, out pcszName, cchMax);
    }

    public int HandleMenuMsg(uint uMsg, IntPtr wParam, IntPtr lParam)
    {
        return this._impl.HandleMenuMsg(uMsg, wParam, lParam);
    }

    public int HandleMenuMsg2(uint uMsg, IntPtr wParam, IntPtr lParam, out IntPtr plResult)
    {
        return this._impl.HandleMenuMsg2(uMsg, wParam, lParam, out plResult);
    }

    public int GetClassID(out Guid pClassID)
    {
        return this._impl.GetClassID(out pClassID);
    }

    public int GetSizeMax(out ulong pcbSize)
    {
        return this._impl.GetSizeMax(out pcbSize);
    }

    public int IsDirty()
    {
        return this._impl.IsDirty();
    }

    public int Load(object pStm)
    {
        return this._impl.Load(pStm);
    }

    public int Save(IntPtr pStm, bool fClearDirty)
    {
        return this._impl.Save(pStm, fClearDirty);
    }

    public int UIActivateIO(int fActivate, ref MSG msg)
    {
        return this._impl.UIActivateIO(fActivate, ref msg);
    }

    public int HasFocusIO()
    {
        return this._impl.HasFocusIO();
    }

    public int TranslateAcceleratorIO(ref MSG msg)
    {
        return this._impl.TranslateAcceleratorIO(ref msg);
    }

    [ComRegisterFunction]
    static void Register(Type t)
    {
        RegistrationHelper.Register(t);
    }

    [ComUnregisterFunction]
    static void Unregister(Type t)
    {
        RegistrationHelper.Unregister(t);
    }
}

[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
public sealed class DeskBandSize : ObservableModelBase
{
    int _width;
    int _height;

    public DeskBandSize(int width, int height)
    {
        this.Width = width;
        this.Height = height;
    }
    
    public int Width
    {
        get => this._width;
        set => this.Set(ref this._width, value);
    }

    public int Height
    {
        get => this._height;
        set => this.Set(ref this._height, value);
    }
    
    public static implicit operator DeskBandSize(Size size)
    {
        return new DeskBandSize(Convert.ToInt32(size.Width), Convert.ToInt32(size.Height));
    }

    public static implicit operator Size(DeskBandSize size)
    {
        return new Size(size.Width, size.Height);
    }
}

public interface ICSDeskBand : IDeskBand2, IObjectWithSite, IContextMenu3, IPersistStream, IInputObject
{
}

interface IDeskBandProvider
{
    IntPtr Handle { get; }
    CSDeskBandOptions Options { get; }
    Guid Guid { get; }
    bool HasFocus { get; set; }
}

static class RegistrationHelper
{
    [ComRegisterFunction]
    public static void Register(Type t)
    {
        var guid = t.GUID.ToString("B");
        try
        {
            var registryKey = Registry.ClassesRoot.CreateSubKey($@"CLSID\{guid}");
            registryKey!.SetValue(null, GetToolbarName(t));

            var subKey = registryKey.CreateSubKey("Implemented Categories");
            subKey!.CreateSubKey(ComponentCategoryManager.CATID_DESKBAND.ToString("B"));

            Console.WriteLine($"Successfully registered deskband `{GetToolbarName(t)}` - GUID: {guid}");

            if (!ShowDeskbandAfterRegistration(t))
            {
                return;
            }
            
            Console.WriteLine("Request to show deskband.");

            // https://www.pinvoke.net/default.aspx/Interfaces.ITrayDeskband
            ITrayDeskband csDeskband = null;
            try
            {
                var trayDeskbandType = Type.GetTypeFromCLSID(new Guid("E6442437-6C68-4f52-94DD-2CFED267EFB9"));
                if (trayDeskbandType == null)
                {
                    throw new Exception("Deskband COM component is not available on this system.");
                }
                
                var deskbandGuid = t.GUID;
                csDeskband = (ITrayDeskband)Activator.CreateInstance(trayDeskbandType);
                if (csDeskband == null)
                {
                    return;
                }

                csDeskband.DeskBandRegistrationChanged();

                if (csDeskband.IsDeskBandShown(ref deskbandGuid) != HRESULT.S_FALSE)
                {
                    return;
                }

                if (csDeskband.ShowDeskBand(ref deskbandGuid) != HRESULT.S_OK)
                {
                    Console.WriteLine($"Error while trying to show deskband.");
                }

                if (csDeskband.DeskBandRegistrationChanged() == HRESULT.S_OK)
                {
                    Console.WriteLine(
                        $"The deskband was successfully shown with taskbar.{Environment.NewLine}You may see the alert notice box from explorer call.");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error while trying to show deskband: {e}");
            }
            finally
            {
                if (csDeskband != null && Marshal.IsComObject(csDeskband))
                {
                    Marshal.ReleaseComObject(csDeskband);
                }
            }
        }
        catch (Exception)
        {
            Console.Error.WriteLine($"Failed to register deskband `{GetToolbarName(t)}` - GUID: {guid}");
            throw;
        }
    }

    [ComUnregisterFunction]
    public static void Unregister(Type t)
    {
        var guid = t.GUID.ToString("B");
        try
        {
            Registry.ClassesRoot.OpenSubKey(@"CLSID", true)?.DeleteSubKeyTree(guid);
            Console.WriteLine($"Successfully unregistered deskband `{GetToolbarName(t)}` - GUID: {guid}");
        }
        catch (ArgumentException)
        {
            Console.Error.WriteLine($"Deskband `{GetToolbarName(t)}` is not registered");
        }
        catch (Exception)
        {
            Console.Error.WriteLine($"Failed to unregister deskband `{GetToolbarName(t)}` - GUID: {guid}");
            throw;
        }
    }

    internal static string GetToolbarName(Type t)
    {
        return CSDeskBandRegistration.RegistrationsByType.TryGetValue(t, out var attribute)
            ? attribute?.Name ?? t.Name
            : t.Name;
    }

    /// <summary>
    /// Gets if the deskband should be shown after registration.
    /// </summary>
    /// <param name="t">Type of the deskband.</param>
    /// <returns>The value if it should be shown.</returns>
    internal static bool ShowDeskbandAfterRegistration(Type t)
    {
        return CSDeskBandRegistration.RegistrationsByType.TryGetValue(t, out var attribute)
               && (attribute?.ShowDeskBand ?? false);
    }
}

public enum TaskbarOrientation
{
    Vertical,
    Horizontal
}

[SuppressMessage("ReSharper", "UnusedMember.Global")]
public enum Edge : uint
{
    Left,
    Top,
    Right,
    Bottom
}

[SuppressMessage("ReSharper", "EventNeverSubscribedTo.Global")]
[SuppressMessage("ReSharper", "UnusedMember.Global")]
public sealed class TaskbarInfo
{
    TaskbarOrientation _orientation = TaskbarOrientation.Horizontal;
    Edge _edge = Edge.Bottom;
    DeskBandSize _size;

    internal TaskbarInfo()
    {
        this.UpdateInfo();
    }

    public event EventHandler<TaskbarOrientationChangedEventArgs> TaskbarOrientationChanged;

    public event EventHandler<TaskbarEdgeChangedEventArgs> TaskbarEdgeChanged;

    public event EventHandler<TaskbarSizeChangedEventArgs> TaskbarSizeChanged;

    public TaskbarOrientation Orientation
    {
        get => this._orientation;
        private set
        {
            if (value == this._orientation)
            {
                return;
            }

            this._orientation = value;
            this.TaskbarOrientationChanged?.Invoke(this, new TaskbarOrientationChangedEventArgs(value));
        }
    }

    public Edge Edge
    {
        get => this._edge;
        private set
        {
            if (value == this._edge)
            {
                return;
            }

            this._edge = value;
            this.TaskbarEdgeChanged?.Invoke(this, new TaskbarEdgeChangedEventArgs(value));
        }
    }

    public DeskBandSize Size
    {
        get => this._size;
        private set
        {
            if (value.Equals(this._size))
            {
                return;
            }

            this._size = value;
            this.TaskbarSizeChanged?.Invoke(this, new TaskbarSizeChangedEventArgs(value));
        }
    }

    internal void UpdateInfo()
    {
        var data = new APPBARDATA
        {
            hWnd = IntPtr.Zero,
            cbSize = Marshal.SizeOf<APPBARDATA>()
        };

        _ = Shell32.SHAppBarMessage(APPBARMESSAGE.ABM_GETTASKBARPOS, ref data);
        var rect = data.rc;
        this.Size = new DeskBandSize(rect.right - rect.left, rect.bottom - rect.top);
        this.Edge = (Edge)data.uEdge;
        this.Orientation = this.Edge is Edge.Bottom or Edge.Top
            ? TaskbarOrientation.Horizontal
            : TaskbarOrientation.Vertical;
    }
}

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public sealed class TaskbarOrientationChangedEventArgs : EventArgs
{
    public TaskbarOrientationChangedEventArgs(TaskbarOrientation orientation)
    {
        this.Orientation = orientation;
    }

    public TaskbarOrientation Orientation { get; }
}

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public sealed class TaskbarSizeChangedEventArgs : EventArgs
{
    public TaskbarSizeChangedEventArgs(DeskBandSize size)
    {
        this.Size = size;
    }

    public DeskBandSize Size { get; }
}

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public sealed class TaskbarEdgeChangedEventArgs : EventArgs
{
    public TaskbarEdgeChangedEventArgs(Edge edge)
    {
        this.Edge = edge;
    }

    public Edge Edge { get; }
}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("4CF504B0-DE96-11D0-8B3F-00A0C911E8E5")]
[PublicAPI]
interface IBandSite
{
    [PreserveSig]
    int AddBand(ref object punk);

    [PreserveSig]
    int EnumBands(int uBand, out uint pdwBandId);

    [PreserveSig]
    int QueryBand(uint dwBandId, out IDeskBand ppstb, out BANDSITEINFO.BSSF pdwState,
        [MarshalAs(UnmanagedType.LPWStr)] out string pszName, int cchName);

    [PreserveSig]
    int SetBandState(uint dwBandId, BANDSITEINFO.BSIM dwMask, BANDSITEINFO.BSSF dwState);

    [PreserveSig]
    int RemoveBand(uint dwBandId);

    [PreserveSig]
    int GetBandObject(uint dwBandId, ref Guid riid, out IntPtr ppv);

    [PreserveSig]
    int SetBandSiteInfo([In] ref BANDSITEINFO pbsinfo);

    [PreserveSig]
    int GetBandSiteInfo([In, Out] ref BANDSITEINFO pbsinfo);
}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("012DD920-7B26-11D0-8CA9-00A0C92DBFE8")]
[PublicAPI]
public interface IDockingWindow : IOleWindow
{
    [PreserveSig]
    new int GetWindow(out IntPtr pHwnd);

    [PreserveSig]
    new int ContextSensitiveHelp(bool fEnterMode);

    [PreserveSig]
    int ShowDW(bool fShow);

    [PreserveSig]
    int CloseDW(uint dwReserved);

    [PreserveSig]
    int ResizeBorderDW(RECT prcBorder, [MarshalAs(UnmanagedType.IUnknown)] IntPtr punkToolbarSite, bool fReserved);
}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("EB0FE172-1A3A-11D0-89B3-00A0C90A90AC")]
[PublicAPI]
public interface IDeskBand : IDockingWindow
{
    [PreserveSig]
    new int GetWindow(out IntPtr pHwnd);

    [PreserveSig]
    new int ContextSensitiveHelp(bool fEnterMode);

    [PreserveSig]
    new int ShowDW(bool fShow);

    [PreserveSig]
    new int CloseDW(uint dwReserved);

    [PreserveSig]
    new int ResizeBorderDW(RECT prcBorder, [MarshalAs(UnmanagedType.IUnknown)] IntPtr punkToolbarSite,
        bool fReserved);

    [PreserveSig]
    int GetBandInfo(uint dwBandId, DESKBANDINFO.DBIF dwViewMode, ref DESKBANDINFO pdbi);
}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("79D16DE4-ABEE-4021-8D9D-9169B261D657")]
[PublicAPI]
public interface IDeskBand2 : IDeskBand
{
    [PreserveSig]
    new int GetWindow(out IntPtr pHwnd);

    [PreserveSig]
    new int ContextSensitiveHelp(bool fEnterMode);

    [PreserveSig]
    new int ShowDW(bool fShow);

    [PreserveSig]
    new int CloseDW(uint dwReserved);

    [PreserveSig]
    new int ResizeBorderDW(RECT prcBorder, [MarshalAs(UnmanagedType.IUnknown)] IntPtr punkToolbarSite,
        bool fReserved);

    [PreserveSig]
    new int GetBandInfo(uint dwBandId, DESKBANDINFO.DBIF dwViewMode, ref DESKBANDINFO pdbi);

    [PreserveSig]
    int CanRenderComposited(out bool pfCanRenderComposited);

    [PreserveSig]
    int SetCompositionState(bool fCompositionEnabled);

    [PreserveSig]
    int GetCompositionState(out bool pfCompositionEnabled);
}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("000214e4-0000-0000-c000-000000000046")]
[PublicAPI]
public interface IContextMenu
{
    [PreserveSig]
    int QueryContextMenu(IntPtr hMenu, uint indexMenu, uint idCmdFirst, uint idCmdLast,
        QueryContextMenuFlags uFlags);

    [PreserveSig]
    int InvokeCommand(IntPtr pici);

    [PreserveSig]
    int GetCommandString(ref uint idcmd, uint uflags, ref uint pwReserved,
        [MarshalAs(UnmanagedType.LPTStr)] out string pcszName, uint cchMax);
}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("000214f4-0000-0000-c000-000000000046")]
[PublicAPI]
public interface IContextMenu2 : IContextMenu
{
    [PreserveSig]
    new int QueryContextMenu(IntPtr hMenu, uint indexMenu, uint idCmdFirst, uint idCmdLast,
        QueryContextMenuFlags uFlags);

    [PreserveSig]
    new int InvokeCommand(IntPtr pici);

    [PreserveSig]
    new int GetCommandString(ref uint idcmd, uint uflags, ref uint pwReserved,
        [MarshalAs(UnmanagedType.LPTStr)] out string pcszName, uint cchMax);

    [PreserveSig]
    int HandleMenuMsg(uint uMsg, IntPtr wParam, IntPtr lParam);
}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("bcfce0a0-ec17-11d0-8d10-00a0c90f2719")]
[PublicAPI]
public interface IContextMenu3 : IContextMenu2
{
    [PreserveSig]
    new int QueryContextMenu(IntPtr hMenu, uint indexMenu, uint idCmdFirst, uint idCmdLast,
        QueryContextMenuFlags uFlags);

    [PreserveSig]
    new int InvokeCommand(IntPtr pici);

    [PreserveSig]
    new int GetCommandString(ref uint idcmd, uint uflags, ref uint pwReserved,
        [MarshalAs(UnmanagedType.LPTStr)] out string pcszName, uint cchMax);

    [PreserveSig]
    new int HandleMenuMsg(uint uMsg, IntPtr wParam, IntPtr lParam);

    [PreserveSig]
    int HandleMenuMsg2(uint uMsg, IntPtr wParam, IntPtr lParam, out IntPtr plResult);
}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("68284faa-6a48-11d0-8c78-00c04fd918b4")]
[PublicAPI]
public interface IInputObject
{
    [PreserveSig]
    int UIActivateIO(int fActivate, ref MSG msg);

    [PreserveSig]
    int HasFocusIO();

    [PreserveSig]
    int TranslateAcceleratorIO(ref MSG msg);
}

//https://msdn.microsoft.com/en-us/library/windows/desktop/bb761789(v=vs.85).aspx
[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("F1DB8392-7331-11D0-8C99-00A0C92DBFE8")]
[PublicAPI]
public interface IInputObjectSite
{
    [PreserveSig]
    int OnFocusChangeIS([MarshalAs(UnmanagedType.IUnknown)] object punkObj, int fSetFocus);
}

//https://msdn.microsoft.com/en-us/library/windows/desktop/ms693765(v=vs.85).aspx
[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("FC4801A3-2BA9-11CF-A229-00AA003D7352")]
[PublicAPI]
public interface IObjectWithSite
{
    [PreserveSig]
    int SetSite([MarshalAs(UnmanagedType.IUnknown)] object pUnkSite);

    [PreserveSig]
    int GetSite(ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out IntPtr ppvSite);
}

//https://msdn.microsoft.com/en-us/library/windows/desktop/ms683797(v=vs.85).aspx
[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("b722bccb-4e68-101b-a2bc-00aa00404770")]
[PublicAPI]
interface IOleCommandTarget
{
    [PreserveSig]
    void QueryStatus(ref Guid pguidCmdGroup, uint cCmds,
        [MarshalAs(UnmanagedType.LPArray), In, Out]
        OLECMD[] prgCmds, [In, Out] ref OLECMDTEXT pCmdText);

    [PreserveSig]
    int Exec(ref Guid pguidCmdGroup, uint nCmdId, uint nCmdExecOpt, IntPtr pvaIn, [In, Out] IntPtr pvaOut);
}

//https://msdn.microsoft.com/en-us/library/windows/desktop/ms680102(v=vs.85).aspx
[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("00000114-0000-0000-C000-000000000046")]
[PublicAPI]
public interface IOleWindow
{
    [PreserveSig]
    int GetWindow(out IntPtr pHwnd);

    [PreserveSig]
    int ContextSensitiveHelp(bool fEnterMode);
}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("0000010c-0000-0000-C000-000000000046")]
[PublicAPI]
public interface IPersist
{
    [PreserveSig]
    int GetClassID(out Guid pClassId);
}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("00000109-0000-0000-C000-000000000046")]
[PublicAPI]
public interface IPersistStream : IPersist
{
    [PreserveSig]
    new int GetClassID(out Guid pClassId);

    [PreserveSig]
    int GetSizeMax(out ulong pcbSize);

    [PreserveSig]
    int IsDirty();

    [PreserveSig]
    int Load([In, MarshalAs(UnmanagedType.Interface)] object pStm);

    [PreserveSig]
    int Save([In, MarshalAs(UnmanagedType.Interface)] IntPtr pStm, bool fClearDirty);
}

[ComImport, Guid("6D67E846-5B9C-4db8-9CBC-DDE12F4254F1"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[PublicAPI]
public interface ITrayDeskband
{
    [PreserveSig]
    int ShowDeskBand([In, MarshalAs(UnmanagedType.Struct)] ref Guid clsid);

    [PreserveSig]
    int HideDeskBand([In, MarshalAs(UnmanagedType.Struct)] ref Guid clsid);

    [PreserveSig]
    int IsDeskBandShown([In, MarshalAs(UnmanagedType.Struct)] ref Guid clsid);

    [PreserveSig]
    int DeskBandRegistrationChanged();
}

[SuppressMessage("ReSharper", "IdentifierTypo")]
static class User32
{
    [DllImport("user32.dll", SetLastError = true)]
    public static extern int SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

    [DllImport("user32.dll")]
    public static extern bool InsertMenuItem(IntPtr hMenu, uint uItem, bool fByPosition, ref MENUITEMINFO lpmii);

    [DllImport("user32.dll")]
    public static extern IntPtr CreateMenu();

    [DllImport("user32.dll")]
    public static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    public static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll")]
    public static extern bool TranslateMessage([In] ref MSG lpMsg);

    [DllImport("user32.dll")]
    public static extern IntPtr DispatchMessage([In] ref MSG lpmsg);

    public static int HiWord(int val)
    {
        return Convert.ToInt32(BitConverter.ToInt16(BitConverter.GetBytes(val), 2));
    }

    public static int LoWord(int val)
    {
        return Convert.ToInt32(BitConverter.ToInt16(BitConverter.GetBytes(val), 0));
    }
}

class Shell32
{
    [DllImport("shell32.dll")]
    public static extern IntPtr SHAppBarMessage(APPBARMESSAGE dwMessage, [In] ref APPBARDATA pData);
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[SuppressMessage("ReSharper", "IdentifierTypo")]
[PublicAPI]
enum tagDESKBANDCID
{
    DBID_BANDINFOCHANGED = 0,
    DBID_SHOWONLY = 1,
    DBID_MAXIMIZEBAND = 2,
    DBID_PUSHCHEVRON = 3
};

[SuppressMessage("ReSharper", "InconsistentNaming")]
[PublicAPI]
[StructLayout(LayoutKind.Sequential)]
public struct RECT
{
    public int left;
    public int top;
    public int right;
    public int bottom;

    public RECT(int left, int top, int right, int bottom)
    {
        this.left = left;
        this.top = top;
        this.right = right;
        this.bottom = bottom;
    }
}

[Flags]
[PublicAPI]
[SuppressMessage("ReSharper", "InconsistentNaming")]
[SuppressMessage("ReSharper", "IdentifierTypo")]
public enum QueryContextMenuFlags : uint
{
    CMF_NORMAL = 0x00000000,
    CMF_DEFAULTONLY = 0x00000001,
    CMF_VERBSONLY = 0x00000002,
    CMF_EXPLORE = 0x00000004,
    CMF_NOVERBS = 0x00000008,
    CMF_CANRENAME = 0x00000010,
    CMF_NODEFAULT = 0x00000020,
    CMF_ITEMMENU = 0x00000080,
    CMF_EXTENDEDVERBS = 0x00000100,
    CMF_DISABLEDVERBS = 0x00000200,
    CMF_ASYNCVERBSTATE = 0x00000400,
    CMF_OPTIMIZEFORINVOKE = 0x00000800,
    CMF_SYNCCASCADEMENU = 0x00001000,
    CMF_DONOTPICKDEFAULT = 0x00002000,
    CMF_RESERVED = 0xffff0000,
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
[SuppressMessage("ReSharper", "InconsistentNaming")]
public struct POINT
{
    public int X;
    public int Y;
}

[StructLayout(LayoutKind.Sequential)]
[SuppressMessage("ReSharper", "InconsistentNaming")]
[SuppressMessage("ReSharper", "IdentifierTypo")]
[SuppressMessage("ReSharper", "FieldCanBeMadeReadOnly.Global")]
struct OLECMDTEXT
{
    public uint cmdtextf;
    public uint cwActual;
    public uint cwBuf;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 1)]
    public string rgwz;
}

[StructLayout(LayoutKind.Sequential)]
[SuppressMessage("ReSharper", "InconsistentNaming")]
[SuppressMessage("ReSharper", "FieldCanBeMadeReadOnly.Global")]
[SuppressMessage("ReSharper", "IdentifierTypo")]
struct OLECMD
{
    public uint cmdID;
    public uint cmdf;
}

[StructLayout(LayoutKind.Sequential)]
[SuppressMessage("ReSharper", "InconsistentNaming")]
[SuppressMessage("ReSharper", "FieldCanBeMadeReadOnly.Global")]
public struct MSG
{
    public IntPtr hwnd;
    public uint message;
    public uint wParam;
    public int lParam;
    public uint time;
    public POINT pt;
}

[StructLayout(LayoutKind.Sequential)]
[SuppressMessage("ReSharper", "InconsistentNaming")]
[SuppressMessage("ReSharper", "IdentifierTypo")]
[SuppressMessage("ReSharper", "UnusedMember.Global")]
struct MENUITEMINFO
{
    public int cbSize;
    public MIIM fMask;
    public MFT fType;
    public MFS fState;
    public uint wID;
    public IntPtr hSubMenu;
    public IntPtr hbmpChecked;
    public IntPtr hbmpUnchecked;
    public IntPtr dwItemData;
    [MarshalAs(UnmanagedType.LPStr)] public string dwTypeData;
    public uint cch;
    public IntPtr hbmpItem;

    [Flags]
    public enum MIIM : uint
    {
        MIIM_BITMAP = 0x00000080,
        MIIM_CHECKMARKS = 0x00000008,
        MIIM_DATA = 0x00000020,
        MIIM_FTYPE = 0x00000100,
        MIIM_ID = 0x00000002,
        MIIM_STATE = 0x00000001,
        MIIM_STRING = 0x00000040,
        MIIM_SUBMENU = 0x00000004,
        MIIM_TYPE = 0x00000010
    }

    [Flags]
    public enum MFT : uint
    {
        MFT_BITMAP = 0x00000004,
        MFT_MENUBARBREAK = 0x00000020,
        MFT_MENUBREAK = 0x00000040,
        MFT_OWNERDRAW = 0x00000100,
        MFT_RADIOCHECK = 0x00000200,
        MFT_RIGHTJUSTIFY = 0x00004000,
        MFT_RIGHTORDER = 0x00002000,
        MFT_SEPARATOR = 0x00000800,
        MFT_STRING = 0x00000000,
    }

    [Flags]
    public enum MFS : uint
    {
        MFS_CHECKED = 0x00000008,
        MFS_DEFAULT = 0x00001000,
        MFS_DISABLED = 0x00000003,
        MFS_ENABLED = 0x00000000,
        MFS_GRAYED = 0x00000003,
        MFS_HILITE = 0x00000080,
        MFS_UNCHECKED = 0x00000000,
        MFS_UNHILITE = 0x00000000,
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[SuppressMessage("ReSharper", "ConvertToConstant.Global")]
[PublicAPI]
class HRESULT
{
    #pragma warning disable CS0649
    public static readonly int S_OK;
    #pragma warning restore CS0649
    public static readonly int S_FALSE = 1;
    public static readonly int E_NOTIMPL = unchecked((int)0x80004001);
    public static readonly int E_FAIL = unchecked((int)0x80004005);

    public static int MakeHResult(uint sev, uint facility, uint errorNo)
    {
        uint result = sev << 31 | facility << 16 | errorNo;
        return unchecked((int)result);
    }
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
[SuppressMessage("ReSharper", "InconsistentNaming")]
[SuppressMessage("ReSharper", "IdentifierTypo")]
[PublicAPI]
public struct DESKBANDINFO
{
    public DBIM dwMask;
    public POINT ptMinSize;
    public POINT ptMaxSize;
    public POINT ptIntegral;
    public POINT ptActual;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 255)]
    public string wszTitle;

    public DBIMF dwModeFlags;
    public COLORREF crBkgnd;

    [Flags]
    [PublicAPI]
    public enum DBIF : uint
    {
        DBIF_VIEWMODE_NORMAL = 0x0000,
        DBIF_VIEWMODE_VERTICAL = 0x0001,
        DBIF_VIEWMODE_FLOATING = 0x0002,
        DBIF_VIEWMODE_TRANSPARENT = 0x0004
    }

    [Flags]
    [PublicAPI]
    public enum DBIM : uint
    {
        DBIM_MINSIZE = 0x0001,
        DBIM_MAXSIZE = 0x0002,
        DBIM_INTEGRAL = 0x0004,
        DBIM_ACTUAL = 0x0008,
        DBIM_TITLE = 0x0010,
        DBIM_MODEFLAGS = 0x0020,
        DBIM_BKCOLOR = 0x0040
    }

    [Flags]
    [PublicAPI]
    public enum DBIMF : uint
    {
        DBIMF_NORMAL = 0x0000,
        DBIMF_FIXED = 0x0001,
        DBIMF_FIXEDBMP = 0x0004,
        DBIMF_VARIABLEHEIGHT = 0x0008,
        DBIMF_UNDELETEABLE = 0x0010,
        DBIMF_DEBOSSED = 0x0020,
        DBIMF_BKCOLOR = 0x0040,
        DBIMF_USECHEVRON = 0x0080,
        DBIMF_BREAK = 0x0100,
        DBIMF_ADDTOFRONT = 0x0200,
        DBIMF_TOPALIGN = 0x0400,
        DBIMF_NOGRIPPER = 0x0800,
        DBIMF_ALWAYSGRIPPER = 0x1000,
        DBIMF_NOMARGINS = 0x2000
    }
}

[StructLayout(LayoutKind.Sequential)]
[SuppressMessage("ReSharper", "InconsistentNaming")]
[SuppressMessage("ReSharper", "IdentifierTypo")]
[SuppressMessage("ReSharper", "FieldCanBeMadeReadOnly.Global")]
public struct COLORREF
{
    public byte R;
    public byte G;
    public byte B;
}

[StructLayout(LayoutKind.Sequential)]
[SuppressMessage("ReSharper", "InconsistentNaming")]
[SuppressMessage("ReSharper", "FieldCanBeMadeReadOnly.Global")]
[SuppressMessage("ReSharper", "IdentifierTypo")]
[PublicAPI]
struct CMINVOKECOMMANDINFOEX
{
    public uint cbSize;
    public CMIC fMask;
    public IntPtr hwnd;
    public IntPtr lpVerb;
    [MarshalAs(UnmanagedType.LPStr)] public string lpParameters;
    [MarshalAs(UnmanagedType.LPStr)] public string lpDirectory;
    public int nShow;
    public uint dwHotKey;
    public IntPtr hIcon;
    [MarshalAs(UnmanagedType.LPStr)] public string lpTitle;
    public IntPtr lpVerbW;
    [MarshalAs(UnmanagedType.LPWStr)] public string lpParametersW;
    [MarshalAs(UnmanagedType.LPWStr)] public string lpDirectoryW;
    [MarshalAs(UnmanagedType.LPWStr)] public string lpTitleW;
    public POINT ptInvoke;

    [Flags]
    [PublicAPI]
    public enum CMIC
    {
        CMIC_MASK_HOTKEY = 0x00000020,
        CMIC_MASK_ICON = 0x00000010,
        CMIC_MASK_FLAG_NO_UI = 0x00000400,
        CMIC_MASK_UNICODE = 0x00004000,
        CMIC_MASK_NO_CONSOLE = 0x00008000,
        CMIC_MASK_ASYNCOK = 0x00100000,
        CMIC_MASK_NOASYNC = 0x00000100,
        CMIC_MASK_SHIFT_DOWN = 0x10000000,
        CMIC_MASK_PTINVOKE = 0x20000000,
        CMIC_MASK_CONTROL_DOWN = 0x40000000,
        CMIC_MASK_FLAG_LOG_USAGE = 0x04000000,
        CMIC_MASK_NOZONECHECKS = 0x00800000,
    }
}

[StructLayout(LayoutKind.Sequential)]
[SuppressMessage("ReSharper", "InconsistentNaming")]
[SuppressMessage("ReSharper", "IdentifierTypo")]
[PublicAPI]
public class CMINVOKECOMMANDINFO
{
    public int cbSize;
    public CMIC fMask;
    public IntPtr hwnd;
    public IntPtr lpVerb;
    [MarshalAs(UnmanagedType.LPStr)] public string lpParameters;
    [MarshalAs(UnmanagedType.LPStr)] public string lpDirectory;
    public int nShow;
    public int dwHotKey;
    public IntPtr hIcon;

    [Flags]
    public enum CMIC
    {
        CMIC_MASK_HOTKEY = 0x00000020,
        CMIC_MASK_ICON = 0x00000010,
        CMIC_MASK_FLAG_NO_UI = 0x00000400,
        CMIC_MASK_NO_CONSOLE = 0x00008000,
        CMIC_MASK_ASYNCOK = 0x00100000,
        CMIC_MASK_NOASYNC = 0x00000100,
        CMIC_MASK_SHIFT_DOWN = 0x10000000,
        CMIC_MASK_CONTROL_DOWN = 0x40000000,
        CMIC_MASK_FLAG_LOG_USAGE = 0x04000000,
        CMIC_MASK_NOZONECHECKS = 0x00800000,
    }
}

[StructLayout(LayoutKind.Sequential)]
[SuppressMessage("ReSharper", "InconsistentNaming")]
[SuppressMessage("ReSharper", "IdentifierTypo")]
[PublicAPI]
class CATEGORYINFO
{
    public Guid catid;
    public uint lcidl;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string szDescription;
}

[StructLayout(LayoutKind.Sequential)]
[SuppressMessage("ReSharper", "InconsistentNaming")]
[SuppressMessage("ReSharper", "IdentifierTypo")]
[PublicAPI]
struct BANDSITEINFO
{
    public BSIM dwMask;
    public BSSF dwState;
    public BSIS dwStyle;

    [Flags]
    [PublicAPI]
    public enum BSIM : uint
    {
        BSIM_STATE = 0x00000001,
        BSIM_STYLE = 0x00000002,
    }

    [Flags]
    [PublicAPI]
    public enum BSSF : uint
    {
        BSSF_VISIBLE = 0x00000001,
        BSSF_NOTITLE = 0x00000002,
        BSSF_UNDELETEABLE = 0x00001000,
    }

    [Flags]
    [PublicAPI]
    public enum BSIS : uint
    {
        BSIS_AUTOGRIPPER = 0x00000000,
        BSIS_NOGRIPPER = 0x00000001,
        BSIS_ALWAYSGRIPPER = 0x00000002,
        BSIS_LEFTALIGN = 0x00000004,
        BSIS_SINGLECLICK = 0x00000008,
        BSIS_NOCONTEXTMENU = 0x00000010,
        BSIS_NODROPTARGET = 0x00000020,
        BSIS_NOCAPTION = 0x00000040,
        BSIS_PREFERNOLINEBREAK = 0x00000080,
        BSIS_LOCKED = 0x00000100,
        BSIS_PRESERVEORDERDURINGLAYOUT = 0x00000200,
        BSIS_FIXEDORDER = 0x00000400,
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[SuppressMessage("ReSharper", "IdentifierTypo")]
[PublicAPI]
enum APPBARMESSAGE : uint
{
    ABM_NEW = 0x00000000,
    ABM_REMOVE = 0x00000001,
    ABM_QUERYPOS = 0x00000002,
    ABM_SETPOS = 0x00000003,
    ABM_GETSTATE = 0x00000004,
    ABM_GETTASKBARPOS = 0x00000005,
    ABM_ACTIVATE = 0x00000006,
    ABM_GETAUTOHIDEBAR = 0x00000007,
    ABM_SETAUTOHIDEBAR = 0x00000008,
    ABM_WINDOWPOSCHANGED = 0x00000009,
    ABM_SETSTATE = 0x0000000A,
    ABM_GETAUTOHIDEBAREX = 0x0000000B,
    ABM_SETAUTOHIDEBAREX = 0x0000000C,
}

[StructLayout(LayoutKind.Sequential)]
[SuppressMessage("ReSharper", "InconsistentNaming")]
[SuppressMessage("ReSharper", "IdentifierTypo")]
[PublicAPI]
struct APPBARDATA
{
    public int cbSize;
    public IntPtr hWnd;
    public uint uCallbackMessage;
    public uint uEdge;
    public RECT rc;
    public int lParam;
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[PublicAPI]
[SuppressMessage("ReSharper", "IdentifierTypo")]
class ComponentCategoryManager
{
    public static readonly Guid CATID_DESKBAND = new("00021492-0000-0000-C000-000000000046");

    static readonly Guid _componentCategoryManager = new("0002e005-0000-0000-c000-000000000046");
    static readonly ICatRegister _catRegister;
    Guid _classId;

    static ComponentCategoryManager()
    {
        _catRegister =
            Activator.CreateInstance(Type.GetTypeFromCLSID(_componentCategoryManager, true)!) as ICatRegister;
    }

    ComponentCategoryManager(Guid classId)
    {
        this._classId = classId;
    }

    public static ComponentCategoryManager For(Guid classId)
    {
        return new ComponentCategoryManager(classId);
    }

    public void RegisterCategories(Guid[] categoryIds)
    {
        _catRegister.RegisterClassImplCategories(ref this._classId, (uint)categoryIds.Length, categoryIds);
    }

    public void UnRegisterCategories(Guid[] categoryIds)
    {
        _catRegister.UnRegisterClassImplCategories(ref this._classId, (uint)categoryIds.Length, categoryIds);
    }
}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("0002E012-0000-0000-C000-000000000046")]
[SuppressMessage("ReSharper", "InconsistentNaming")]
[SuppressMessage("ReSharper", "IdentifierTypo")]
[PublicAPI]
interface ICatRegister
{
    [PreserveSig]
    void RegisterCategories(uint cCategories, [MarshalAs(UnmanagedType.LPArray)] CATEGORYINFO[] rgCategoryInfo);

    [PreserveSig]
    void RegisterClassImplCategories([In] ref Guid rclsid, uint cCategories,
        [MarshalAs(UnmanagedType.LPArray)] Guid[] rgcatid);

    [PreserveSig]
    void RegisterClassReqCategories([In] ref Guid rclsid, uint cCategories,
        [MarshalAs(UnmanagedType.LPArray)] Guid[] rgcatid);

    [PreserveSig]
    void UnRegisterCategories(uint cCategories, [MarshalAs(UnmanagedType.LPArray)] Guid[] rgcatid);

    [PreserveSig]
    void UnRegisterClassImplCategories([In] ref Guid rclsid, uint cCategories,
        [MarshalAs(UnmanagedType.LPArray)] Guid[] rgcatid);

    [PreserveSig]
    void UnRegisterClassReqCategories([In] ref Guid rclsid, uint cCategories,
        [MarshalAs(UnmanagedType.LPArray)] Guid[] rgcatid);
}

[Flags]
[SuppressMessage("ReSharper", "InconsistentNaming")]
[SuppressMessage("ReSharper", "IdentifierTypo")]
[PublicAPI]
enum WindowStyles : uint
{
    WS_BORDER = 0x800000,
    WS_CAPTION = 0xc00000,
    WS_CHILD = 0x40000000,
    WS_CLIPCHILDREN = 0x2000000,
    WS_CLIPSIBLINGS = 0x4000000,
    WS_DISABLED = 0x8000000,
    WS_DLGFRAME = 0x400000,
    WS_GROUP = 0x20000,
    WS_HSCROLL = 0x100000,
    WS_MAXIMIZE = 0x1000000,
    WS_MAXIMIZEBOX = 0x10000,
    WS_MINIMIZE = 0x20000000,
    WS_MINIMIZEBOX = 0x20000,
    WS_OVERLAPPED = 0x0,
    WS_OVERLAPPEDWINDOW = WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_SIZEFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX,
    WS_POPUP = 0x80000000u,
    WS_POPUPWINDOW = WS_POPUP | WS_BORDER | WS_SYSMENU,
    WS_SIZEFRAME = 0x40000,
    WS_SYSMENU = 0x80000,
    WS_TABSTOP = 0x10000,
    WS_VISIBLE = 0x10000000,
    WS_VSCROLL = 0x200000
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[SuppressMessage("ReSharper", "IdentifierTypo")]
[PublicAPI]
enum WindowMessages
{
    WM_NCHITTEST = 0x0084,
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[SuppressMessage("ReSharper", "IdentifierTypo")]
[PublicAPI]
enum HitTestMessageResults
{
    HTCLIENT = 1,
    HTTRANSPARENT = -1,
}

public abstract class DeskBandMenuItem
{
    internal abstract void AddToMenu(IntPtr menu, uint itemPosition, ref uint itemId,
        Dictionary<uint, DeskBandMenuAction> callbacksByItemId);
}

[PublicAPI]
[SuppressMessage("ReSharper", "IdentifierTypo")]
sealed class DeskBandMenuSeparator : DeskBandMenuItem
{
    MENUITEMINFO _menuiteminfo;

    internal override void AddToMenu(IntPtr menu, uint itemPosition, ref uint itemId,
        Dictionary<uint, DeskBandMenuAction> callbacksByItemId)
    {
        this._menuiteminfo = new MENUITEMINFO()
        {
            cbSize = Marshal.SizeOf<MENUITEMINFO>(),
            fMask = MENUITEMINFO.MIIM.MIIM_TYPE,
            fType = MENUITEMINFO.MFT.MFT_SEPARATOR,
        };

        User32.InsertMenuItem(menu, itemPosition, true, ref this._menuiteminfo);
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[SuppressMessage("ReSharper", "IdentifierTypo")]
[PublicAPI]
sealed class DeskBandMenuAction : DeskBandMenuItem
{
    MENUITEMINFO _menuiteminfo;

    public DeskBandMenuAction(string text)
    {
        this.Text = text;
    }

    public event EventHandler Clicked;

    public bool Checked { get; set; }

    public bool Enabled { get; set; } = true;

    public string Text { get; set; }

    internal void DoAction()
    {
        this.Clicked?.Invoke(this, EventArgs.Empty);
    }

    internal override void AddToMenu(IntPtr menu, uint itemPosition, ref uint itemId,
        Dictionary<uint, DeskBandMenuAction> callbacksByItemId)
    {
        this._menuiteminfo = new MENUITEMINFO()
        {
            cbSize = Marshal.SizeOf<MENUITEMINFO>(),
            fMask = MENUITEMINFO.MIIM.MIIM_TYPE | MENUITEMINFO.MIIM.MIIM_STATE | MENUITEMINFO.MIIM.MIIM_ID,
            fType = MENUITEMINFO.MFT.MFT_STRING,
            dwTypeData = this.Text,
            cch = (uint)this.Text.Length,
            wID = itemId++,
        };

        this._menuiteminfo.fState |= this.Enabled ? MENUITEMINFO.MFS.MFS_ENABLED : MENUITEMINFO.MFS.MFS_DISABLED;
        this._menuiteminfo.fState |= this.Checked ? MENUITEMINFO.MFS.MFS_CHECKED : MENUITEMINFO.MFS.MFS_UNCHECKED;

        callbacksByItemId[this._menuiteminfo.wID] = this;

        User32.InsertMenuItem(menu, itemPosition, true, ref this._menuiteminfo);
    }
}

/// <summary>
/// A sub menu item that can contain other <see cref="DeskBandMenuItem"/>.
/// </summary>
[SuppressMessage("ReSharper", "InconsistentNaming")]
[SuppressMessage("ReSharper", "IdentifierTypo")]
[PublicAPI]
sealed class DeskBandMenu : DeskBandMenuItem
{
    IntPtr _menu;
    MENUITEMINFO _menuiteminfo;
    
    public DeskBandMenu(string text, IEnumerable<DeskBandMenuItem> items = null)
    {
        this.Text = text;
        if (items == null)
        {
            return;
        }

        foreach (var item in items)
        {
            this.Items.Add(item);
        }
    }

    ~DeskBandMenu()
    {
        this.ClearMenu();
    }

    public ICollection<DeskBandMenuItem> Items { get; } = new List<DeskBandMenuItem>();

    public bool Enabled { get; set; } = true;

    public string Text { get; set; }

    internal override void AddToMenu(IntPtr menu, uint itemPosition, ref uint itemId,
        Dictionary<uint, DeskBandMenuAction> callbacksByItemId)
    {
        this.ClearMenu();

        this._menu = User32.CreatePopupMenu();
        uint index = 0;
        foreach (var item in this.Items)
        {
            item.AddToMenu(this._menu, index++, ref itemId, callbacksByItemId);
        }

        this._menuiteminfo = new MENUITEMINFO()
        {
            cbSize = Marshal.SizeOf<MENUITEMINFO>(),
            fMask = MENUITEMINFO.MIIM.MIIM_SUBMENU | MENUITEMINFO.MIIM.MIIM_STRING | MENUITEMINFO.MIIM.MIIM_STATE,
            fType = MENUITEMINFO.MFT.MFT_MENUBREAK | MENUITEMINFO.MFT.MFT_STRING,
            fState = this.Enabled ? MENUITEMINFO.MFS.MFS_ENABLED : MENUITEMINFO.MFS.MFS_DISABLED,
            dwTypeData = this.Text,
            cch = (uint)this.Text.Length,
            hSubMenu = this._menu,
        };

        User32.InsertMenuItem(menu, itemPosition, true, ref this._menuiteminfo);
    }

    void ClearMenu()
    {
        if (this._menu != IntPtr.Zero)
        {
            User32.DestroyMenu(this._menu);
        }
    }
}
