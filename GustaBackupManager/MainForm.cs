using GustaBackupManager.Adapters.WinForms;
using GustaBackupManager.App.Interfaces;
using GustaBackupManager.Models.Entities;
using GustaBackupManager.Models.Interfaces;
using GustaBackupManager.Services;
using GustaBackupManager.Services.Data;
using GustaBackupManager.Services.WinForms;
using GustaBackupManager.Views;
using GustaBackupManager.Views.Helpers;
using Microsoft.AspNetCore.Components.WebView.WindowsForms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace GustaBackupManager;

public partial class MainForm : Form
{
    private readonly JsonSettingsService _settingsService;
    private ServiceProvider? _serviceProvider;

    private Panel? _loadingPanel;
    private Label? _loadingLabel;
    private ProgressBar? _loadingProgressBar;

    public MainForm()
    {
        InitializeComponent();

        _settingsService = new JsonSettingsService();
        WindowState = _settingsService.Current.WindowState switch
        {
            AppWindowState.Minimized => FormWindowState.Minimized,
            AppWindowState.Maximized => FormWindowState.Maximized,
            _ => FormWindowState.Normal
        };
        Resize += OnWindowResize;

        InitializeLoadingOverlay(_settingsService.Current.IsDarkMode);

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
        BlazorWebView.Disposed += OnBlazorWebViewDisposed;

        AppDbContext.InitializeDatabase(_serviceProvider);
        LoadCustomIconsIntoCatalog();

        BlazorWebView.Services = _serviceProvider;
        InitializeBlazorWebView();
    }

    private void LoadCustomIconsIntoCatalog()
    {
        if (_serviceProvider is null) return;
        using var scope = _serviceProvider.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        using var db = factory.CreateDbContext();
        FolderIconCatalog.SetCustomIcons(db.CustomIcons.AsNoTracking().ToList());
    }

    private void InitializeLoadingOverlay(bool isDarkMode)
    {
        var bgColor = isDarkMode
            ? Color.FromArgb(26, 26, 39)      // #1a1a27 (Dark)
            : Color.FromArgb(245, 245, 247);  // #f5f5f7 (Light)

        var textColor = isDarkMode
            ? Color.FromArgb(178, 176, 191)   // #b2b0bf (Dark)
            : Color.FromArgb(51, 51, 51);     // #333333 (Light)

        BackColor = bgColor;
        BlazorWebView.BackColor = bgColor;
        BlazorWebView.WebView.DefaultBackgroundColor = bgColor;

        _loadingPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = bgColor
        };

        var centerLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Color.Transparent
        };
        centerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        centerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        centerLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        centerLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        centerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

        _loadingLabel = new Label
        {
            Text = "Inicializando gerenciador de backups industriais...",
            ForeColor = textColor,
            Font = new Font("Segoe UI", 11F, FontStyle.Regular),
            AutoSize = true,
            Anchor = AnchorStyles.None,
            Margin = new Padding(0, 0, 0, 12)
        };

        _loadingProgressBar = new ProgressBar
        {
            Style = ProgressBarStyle.Marquee,
            MarqueeAnimationSpeed = 25,
            Size = new Size(340, 18),
            Anchor = AnchorStyles.None
        };

        centerLayout.Controls.Add(_loadingLabel, 0, 1);
        centerLayout.Controls.Add(_loadingProgressBar, 0, 2);
        _loadingPanel.Controls.Add(centerLayout);

        Controls.Add(_loadingPanel);
        _loadingPanel.BringToFront();
    }

    private void HideLoadingOverlay()
    {
        if (_loadingPanel is null) return;

        _loadingPanel.Visible = false;
        Controls.Remove(_loadingPanel);
        _loadingPanel.Dispose();
        _loadingPanel = null;
    }

    private void ConfigureServices(IServiceCollection services)
    {
        services.AddWindowsFormsBlazorWebView();
        services.AddMudServices();

        var dbPath = Path.Combine(AppContext.BaseDirectory, "backup_manager.db");
        services.AddDbContextFactory<AppDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

        // Arquitetura Component-Service-Model Direta (sem ViewModel)
        services.AddSingleton<ISettingsService>(_settingsService);
        services.AddSingleton<IAuthService, AuthService>();
        services.AddSingleton<ISmbConnectionService, WindowsSmbConnectionService>();
        services.AddScoped<INativeFileDialogService, WinFormsFileDialogService>();
        services.AddScoped<IFileTypeService, FileTypeService>();
        services.AddScoped<IFolderCategoryConfigService, FolderCategoryConfigService>();
        services.AddScoped<ICustomIconService, CustomIconService>();
        services.AddScoped<IBackupRepositoryService, BackupRepositoryService>();
    }

    private void InitializeBlazorWebView()
    {
        BlazorWebView.HostPage = "wwwroot/index.html";
        BlazorWebView.RootComponents.Add<Routes>("#app");

        BlazorWebView.WebView.CoreWebView2InitializationCompleted += (s, e) =>
        {
            if (e.IsSuccess)
            {
#if DEBUG
                BlazorWebView.WebView.CoreWebView2.Settings.AreDevToolsEnabled = true;
#else
                BlazorWebView.WebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
#endif

                if (_loadingLabel is not null)
                    _loadingLabel.Text = "Carregando interface Blazor...";

                BlazorWebView.WebView.CoreWebView2.AddHostObjectToScript(
                    "winFormHost",
                    new WinFormInterop(HideLoadingOverlayFromBlazor));
#if DEBUG
                BeginInvoke(OpenDevToolsWindow);
#endif
            }
            else
            {
                MessageBox.Show(
                    $"Falha ao inicializar o WebView2: {e.InitializationException?.Message}",
                    "Erro de Inicialização",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        };

        BlazorWebView.WebView.NavigationCompleted += (s, e) =>
        {
            if (!e.IsSuccess)
            {
                if (_loadingLabel is not null)
                    _loadingLabel.Text = $"Falha ao carregar a interface: {e.WebErrorStatus}";
                if (_loadingProgressBar is not null)
                    _loadingProgressBar.Visible = false;
            }
        };
    }

    public void HideLoadingOverlayFromBlazor()
    {
        if (InvokeRequired)
        {
            BeginInvoke(HideLoadingOverlay);
            return;
        }

        HideLoadingOverlay();
    }

    private void OpenDevToolsWindow()
    {
        try
        {
            BlazorWebView.WebView.CoreWebView2.OpenDevToolsWindow();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Não foi possível abrir as ferramentas do WebView2: {ex.Message}",
                "Erro ao abrir DevTools",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void OnWindowResize(object? sender, EventArgs e)
    {
        var state = WindowState switch
        {
            FormWindowState.Minimized => AppWindowState.Minimized,
            FormWindowState.Maximized => AppWindowState.Maximized,
            _ => AppWindowState.Normal
        };
        _settingsService.UpdateWindowState(state);
    }

    private void OnBlazorWebViewDisposed(object? sender, EventArgs e)
    {
        _serviceProvider?.Dispose();
        _serviceProvider = null;
    }

    private void MainForm_Load(object sender, EventArgs e)
    {
    }
}
